/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Utilities;

using Newtonsoft.Json;

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// The database of all <see cref="KeybagSet"/>s, indexed by file id
/// </summary>
public class KeybagDb
{
  private readonly Dictionary<ChunkId, KeybagSet> _sets;

  /// <summary>
  /// Create a new KeybagDb
  /// </summary>
  public KeybagDb(
    string? dbFolder = null)
  {
    dbFolder ??= KeybagPrimaryFolder;
    _sets = [];
    DbFolder = Path.GetFullPath(dbFolder);
    if(!Directory.Exists(DbFolder))
    {
      Directory.CreateDirectory(DbFolder);
    }
    Reload();
  }

  /// <summary>
  /// The directory where this DB lives.
  /// Defaults to <see cref="KeybagPrimaryFolder"/>.
  /// </summary>
  public string DbFolder { get; }

  /// <summary>
  /// The collection of all keybagsets
  /// </summary>
  public IReadOnlyCollection<KeybagSet> KeybagSets { get => _sets.Values; }

  /// <summary>
  /// Get the keybagset metadata for the keybag set with
  /// the given <paramref name="fileId"/>, returning null if not available
  /// </summary>
  /// <param name="fileId">
  /// The file ID to look for
  /// </param>
  /// <returns>
  /// The <see cref="KeybagSetDescriptor"/> if found, or null if not.
  /// </returns>
  public KeybagSet? Find(ChunkId fileId)
  {
    return _sets.TryGetValue(fileId, out var descriptor) ? descriptor : null;
  }

  /// <summary>
  /// Store the given keybag set descriptor in this db and save it.
  /// </summary>
  public void Put(KeybagSet kbs)
  {
    _sets[kbs.FileId] = kbs;
    kbs.Save();
  }

  /// <summary>
  /// Reload all keybagsets from the metadata files (clearing their previous states)
  /// </summary>
  public void Reload()
  {
    var descriptors = new Dictionary<ChunkId, KeybagSetDescriptor>();
    var files = Directory.GetFiles(DbFolder, "*.kb3meta.json");
    foreach(var file in files)
    {
      var json = File.ReadAllText(file);
      var descriptor = JsonConvert.DeserializeObject<KeybagSetDescriptor>(json)!;
      var nameTest = GetMetaName(descriptor.FileId);
      if(FileIdentifier.AreSame(nameTest, file))
      {
        // Otherwise "file" is masquerading for what should be "nameTest"
        // and should be ignored
        descriptors[descriptor.FileId] = descriptor;
      }
    }
    var sets =
      descriptors.Values.Select(d => new KeybagSet(this, d));
    // only now install the newly loaded sets
    _sets.Clear();
    foreach(var s in sets)
    {
      _sets[s.FileId] = s;
    }
  }

  /// <summary>
  /// Create a completely new keybag and a new keybag set.
  /// The keybag created is saved as the primary keybag file of the new set.
  /// </summary>
  /// <param name="tag">
  /// The keybag file tag (should meet <see cref="KeybagSet.IsValidTag(string)"/>)
  /// </param>
  /// <param name="cryptor">
  /// The encryptor containg the key. This method does not guard against
  /// reusing key with multiple keybags.
  /// </param>
  /// <returns>
  /// </returns>
  public KeybagSet NewKeybag(
    string tag,
    ChunkCryptor cryptor)
  {
    if(!KeybagSet.IsValidTag(tag))
    {
      throw new InvalidOperationException(
        $"Invalid keybag tag '{tag}'.");
    }
    var kbh = KeybagHeader.InitNew(cryptor);
    var descriptor = new KeybagSetDescriptor(
      tag,
      kbh.FileId.ToBase26(),
      kbh.KeyId.ToString(),
      []);
    var kbs = new KeybagSet(this, descriptor);
    var kbf = new Keybag(kbh);
    kbf.WriteFull(kbs.PrimaryFile, cryptor, true);
    Put(kbs);
    return kbs;
  }

  /// <summary>
  /// Determine the relation between this keybag database and
  /// the file <paramref name="kb3file"/>
  /// </summary>
  /// <param name="kb3file">
  /// The file to check
  /// </param>
  /// <param name="header">
  /// On return: the header describing <paramref name="kb3file"/> if known
  /// </param>
  /// <returns>
  /// A code describing the relation of the file to this database
  /// </returns>
  public KeybagRelation FileRelation(
    string kb3file,
    out KeybagHeader? header)
  {
    header = null;
    var fid = FileIdentifier.FromPath(kb3file);
    if(fid == null)
    {
      return KeybagRelation.Missing;
    }
    header = KeybagHeader.TryFromFile(kb3file);
    if(header == null)
    {
      return KeybagRelation.Unrecognized;
    }
    var knownSet = Find(header.FileId);
    if(knownSet != null)
    {
      return knownSet.FileRelationPart2(fid, header);
    }
    return KeybagRelation.NewSet;
  }

  /// <summary>
  /// Return a <see cref="KeybagSet"/> for the given file.
  /// If that set does not exist yet, create it. If the set
  /// does not contain the given file yet, connect it.
  /// </summary>
  /// <param name="kb3file">
  /// The file to find / import / connect
  /// </param>
  /// <returns>
  /// The existing or new keybag.
  /// </returns>
  public KeybagSet CreateOrImportFrom(
    string kb3file)
  {
    kb3file = Path.GetFullPath(kb3file);
    var hdr = KeybagHeader.FromFile(kb3file);
    var kbs = Find(hdr.FileId);
    if(kbs == null)
    {
      // create new KBS
      var tag = Path.GetFileNameWithoutExtension(kb3file);
      if(!KeybagSet.IsValidTag(tag))
      {
        throw new InvalidOperationException(
          $"Not a valid keybag file tag: '{tag}'");
      }
      var kbh = KeybagHeader.FromFile(kb3file);
      // Do not yet include kb3file, because it could
      // potentially be equal to the primary file
      var descriptor = new KeybagSetDescriptor(
        tag, kbh.FileId.ToBase26(), kbh.KeyId.ToString(), []);
      kbs = new KeybagSet(this, descriptor);
      kbs.TryConnect(kb3file, out var _);
      Put(kbs); // also saves the descriptor
      return kbs;
    }
    else
    {
      kbs.TryConnect(kb3file, out var _);
      return kbs;
    }
  }

  /// <summary>
  /// Get the name for the metadata file persisting the metadata
  /// for the <see cref="KeybagSet"/> for the given <paramref name="fileId"/>.
  /// </summary>
  /// <param name="fileId">
  /// The file id of the keybag(s) in this set
  /// </param>
  /// <returns>
  /// The name of the metadata file
  /// </returns>
  public string GetMetaName(ChunkId fileId)
  {
    return Path.Combine(
      DbFolder,
      $"{fileId.ToBase26()}.kb3meta.json");
  }

  /// <summary>
  /// The default local data folder for KB3 data
  /// </summary>
  public static string KeybagPrimaryFolder { get; } =
    Path.Combine(
        Environment.GetFolderPath(
          Environment.SpecialFolder.LocalApplicationData),
        "KeyBag3");

  static KeybagDb()
  {
    if(!Directory.Exists(KeybagPrimaryFolder))
    {
      Directory.CreateDirectory(KeybagPrimaryFolder);
    }
  }
}
