/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// In-memory Model for a collection of locations where a Keybag is stored
/// </summary>
public class KeybagSet
{
  private readonly Dictionary<string, KeybagReference> _syncTargetMap;

  /// <summary>
  /// Create a new KeyBagSet
  /// </summary>
  /// <param name="owner">
  /// The <see cref="KeybagDb"/> this set is part of
  /// </param>
  /// <param name="tag">
  /// The file tag, which must satisfy <see cref="IsValidTag(string)"/>.
  /// If it ends with the file ID in base26 form, that file id is omitted
  /// by this constructor.
  /// </param>
  /// <param name="fileId">
  /// The file ID
  /// </param>
  /// <param name="keyGuid">
  /// The descryption key GUID
  /// </param>
  /// <param name="syncTargets">
  /// The initial list of synchronization targets
  /// </param>
  internal KeybagSet(
    KeybagDb owner,
    string tag,
    ChunkId fileId,
    Guid keyGuid,
    IEnumerable<KeybagReference> syncTargets)
  {
    _syncTargetMap = new Dictionary<string, KeybagReference>(
      StringComparer.InvariantCultureIgnoreCase);
    // patch the tag if it ends with the file ID
    var fid26 = "." + fileId.ToBase26();
    if(tag.EndsWith(fid26, StringComparison.InvariantCultureIgnoreCase))
    {
      tag = tag[..^fid26.Length];
    }
    if(!IsValidTag(tag))
    {
      throw new InvalidOperationException(
        $"Not a valid KB3 file tag: '{tag}'");
    }
    Owner = owner;
    Tag = tag;
    FileId = fileId;
    KeyGuid = keyGuid;
    foreach(var target in syncTargets)
    {
      _syncTargetMap[target.Location] = target;
    }
    DefaultShortName = tag + ".kb3";
    PrimaryFile = Path.Combine(
      KeybagDb.KeybagPrimaryFolder,
      $"{Tag}.{FileId.ToBase26()}.primary.kb3");
    ViewStateFile = Path.Combine(
      KeybagDb.KeybagPrimaryFolder,
      $"{FileId.ToBase26()}.viewstate.json");
    if(File.Exists(ViewStateFile))
    {
      ViewState = JObject.Parse(File.ReadAllText(ViewStateFile));
    }
    else
    {
      ViewState = new JObject();
    }
    // Make sure the primary file is not a sync target by silently
    // dropping such occurrences
    var primaryFid = FileIdentifier.FromPath(PrimaryFile);
    var dropList = new List<KeybagReference>();
    if(primaryFid != null)
    {
      foreach(var target in syncTargets)
      {
        if(File.Exists(target.Location))
        {
          if(primaryFid.SameAs(target.Location))
          {
            Trace.TraceWarning(
              $"Patching keybag set, dropping primary duplicated as sync target: {target.Location}");
            dropList.Add(target);
          }
        }
      }
    }
    else
    {
      if(_syncTargetMap.Count > 0)
      {
        Trace.TraceWarning(
          $"Primary keybag missing: {PrimaryFile}");
        //throw new InvalidOperationException(
        //  "Invalid keybag set: cannot have sync targets without the primary file existing");
      }
    }
  }

  /// <summary>
  /// Create a new <see cref="KeybagSet"/> from a
  /// <see cref="KeybagSetDescriptor"/>.
  /// </summary>
  /// <param name="owner">
  /// The <see cref="KeybagDb"/> this set belongs to.
  /// </param>
  /// <param name="descriptor">
  /// The descriptor to hydrate
  /// </param>
  public KeybagSet(KeybagDb owner, KeybagSetDescriptor descriptor)
    : this(owner, descriptor.Tag, descriptor.FileId, descriptor.KeyGuid, descriptor.SyncTargets)
  {
  }

  /// <summary>
  /// The <see cref="KeybagDb"/> this set is part of
  /// </summary>
  public KeybagDb Owner { get; }

  /// <summary>
  /// The file ID of the file
  /// </summary>
  public ChunkId FileId { get; }

  /// <summary>
  /// The tag used for constructing file names
  /// </summary>
  public string Tag { get; }

  /// <summary>
  /// The key GUID
  /// </summary>
  public Guid KeyGuid { get; }

  /// <summary>
  /// The default short name for keybag files in this set
  /// </summary>
  public string DefaultShortName { get; }

  /// <summary>
  /// The location of the primary KB3 file for this keybag
  /// </summary>
  public string PrimaryFile { get; }

  /// <summary>
  /// The location of the view state file for this keybag
  /// </summary>
  public string ViewStateFile { get; }

  /// <summary>
  /// The persisted view state for this keybag on this computer.
  /// This is losely typed to improve forward and backward compatibility.
  /// </summary>
  public JObject ViewState { get; }

  /// <summary>
  /// Save the view state to the view state file
  /// </summary>
  public void SaveViewState()
  {
    using(var trx = new FileWriteTransaction(ViewStateFile))
    {
      using(var writer = new StreamWriter(trx.Target))
      {
        writer.WriteLine(
          JsonConvert.SerializeObject(ViewState, Formatting.Indented));
      }
      trx.Commit();
    }
  }

  /// <summary>
  /// Get a single section state from the view state
  /// </summary>
  public bool GetSectionState(string sectionName)
  {
    if(ViewState["sections"] is JObject sectionStates)
    {
      var sectionValue = sectionStates[sectionName];
      if(
        sectionValue != null
        && sectionValue is JValue jv
        && jv.Type == JTokenType.Boolean
        && jv.Value is bool b)
      {
        return b;
      }
      else
      {
        return true;
      }
    }
    else
    {
      sectionStates = new JObject();
      ViewState["sections"] = sectionStates;
      SaveViewState();
      return true;
    }
  }

  /// <summary>
  /// Set a single section state in the view state
  /// (and save it if changed)
  /// </summary>
  public void SetSectionState(string sectionName, bool value)
  {
    if(ViewState["sections"] is JObject sectionStates)
    {
      var sectionValue = sectionStates[sectionName];
      if(
        sectionValue != null
        && sectionValue is JValue jv
        && jv.Type == JTokenType.Boolean
        && jv.Value is bool b
        && b == value)
      {
        return; // no change
      }
      else
      {
        sectionStates[sectionName] = value;
        SaveViewState();
      }
    }
    else
    {
      sectionStates = new JObject();
      ViewState["sections"] = sectionStates;
      sectionStates[sectionName] = value;
      SaveViewState();
    }
  }

  /// <summary>
  /// The collection of files to synchronize with
  /// </summary>
  public IReadOnlyCollection<KeybagReference> SyncFiles {
    get => _syncTargetMap.Values;
  }

  /// <summary>
  /// Get the key descriptor. This is cached after the first read
  /// from the primary file.
  /// </summary>
  /// <returns>
  /// The key descriptor, if available
  /// </returns>
  public KeyData? GetKeyDescriptor()
  {
    if(_keyDescriptor == null)
    {
      if(File.Exists(PrimaryFile))
      {
        var header = KeybagHeader.TryFromFile(PrimaryFile);
        if(header != null)
        {
          _keyDescriptor = header.KeyDescriptor;
        }
      }
    }
    return _keyDescriptor;
  }

  private KeyData? _keyDescriptor;

  /// <summary>
  /// Describe the relation of the given file to this
  /// <see cref="KeybagSet"/>
  /// </summary>
  /// <param name="kb3file">
  /// The file to inspect
  /// </param>
  /// <returns>
  /// One of the <see cref="KeybagRelation"/> codes
  /// </returns>
  public KeybagRelation FileRelation(string kb3file)
  {
    var fid = FileIdentifier.FromPath(kb3file);
    if(fid == null)
    {
      return KeybagRelation.Missing;
    }
    var header = KeybagHeader.TryFromFile(kb3file);
    if(header == null)
    {
      return KeybagRelation.Unrecognized;
    }
    return FileRelationPart2(fid, header);
  }

  /// <summary>
  /// (Implementatation part shared between <see cref="FileRelation(string)"/>
  /// and <see cref="KeybagDb.FileRelation(string, out KeybagHeader?)"/>)
  /// </summary>
  internal KeybagRelation FileRelationPart2(
    FileIdentifier fid,
    KeybagHeader header)
  {
    if(header.FileId.Value != FileId.Value)
    {
      return KeybagRelation.NewSet;
    }
    if(header.KeyId != KeyGuid)
    {
      return KeybagRelation.Incompatible;
    }
    if(fid.SameAs(PrimaryFile))
    {
      return KeybagRelation.Primary;
    }
    foreach(var f in SyncFiles)
    {
      if(fid.SameAs(f.Location))
      {
        return KeybagRelation.SyncTarget;
      }
    }
    return KeybagRelation.Extern;
  }

  /// <summary>
  /// Try to attach the file to this set if possible and not already
  /// present. If no primary file is present yet, this is also copied
  /// as the primary file.
  /// </summary>
  /// <param name="kb3file">
  /// The file to connect
  /// </param>
  /// <param name="newRef">
  /// The new reference to the file added to this set upon return
  /// if one was added, or null otherwise.
  /// </param>
  /// <returns>
  /// True if the file is part of this set upon return (including the
  /// case where it already was part), false if it could not be attached. 
  /// </returns>
  public bool TryConnect(string kb3file, out KeybagReference? newRef)
  {
    newRef = null;
    kb3file = Path.GetFullPath(kb3file);
    var fid = FileIdentifier.FromPath(kb3file);
    if(fid == null)
    {
      return false;
    }
    var status = FileRelation(kb3file);
    switch(status)
    {
      case KeybagRelation.Missing:
      case KeybagRelation.Unrecognized:
      case KeybagRelation.Incompatible:
      case KeybagRelation.NewSet:
        return false;
      case KeybagRelation.Primary:
      case KeybagRelation.SyncTarget:
        return true;
      case KeybagRelation.Extern:
        break;
      default:
        throw new InvalidOperationException(
          $"Unexpected File Status {status}");
    }
    if(!File.Exists(PrimaryFile))
    {
      File.Copy(kb3file, PrimaryFile);
    }
    var st = new KeybagReference(kb3file, fid.VolumeSerial);
    _syncTargetMap[st.Location] = st;
    Save();
    newRef = st;
    return true;
  }

  /// <summary>
  /// Try to disconnect the given file from this set
  /// </summary>
  public bool TryDisconnect(KeybagReference target)
  {
    if(_syncTargetMap.Remove(target.Location))
    {
      Save();
      return true;
    }
    return false;
  }

  /// <summary>
  /// Export this set to a new synchronization file
  /// </summary>
  /// <param name="kb3file">
  /// The name for the new file (must not exist yet).
  /// </param>
  public KeybagReference ExportAndConnect(string kb3file)
  {
    kb3file = Path.GetFullPath(kb3file);
    if(File.Exists(kb3file))
    {
      throw new InvalidOperationException(
        $"File already exists: {kb3file}");
    }
    if(!File.Exists(PrimaryFile))
    {
      throw new InvalidOperationException(
        "There is no primary file in this keybag set yet");
    }
    File.Copy(PrimaryFile, kb3file);
    TryConnect(kb3file, out var newRef);
    if(newRef == null)
    {
      throw new InvalidOperationException(
        "Failed to connect the new file");
    }
    return newRef;
  }

  /// <summary>
  /// Create a new Descriptor for this KeybagSet
  /// </summary>
  public KeybagSetDescriptor ToDescriptor()
  {
    return new KeybagSetDescriptor(
      Tag,
      FileId.ToBase26(),
      KeyGuid.ToString(),
      SyncFiles);
  }

  /// <summary>
  /// Save the descriptor for this set to the database folder
  /// </summary>
  public void Save()
  {
    var descriptor = ToDescriptor();
    var metaName = KeybagDb.GetMetaName(descriptor.FileId);
    var json = JsonConvert.SerializeObject(descriptor, Formatting.Indented);
    using(var trx = new FileWriteTransaction(metaName))
    {
      using(var writer = new StreamWriter(trx.Target))
      {
        writer.WriteLine(json);
      }
      trx.Commit();
    }
  }

  /// <summary>
  /// Check if the given string is valid as a file tag
  /// </summary>
  public static bool IsValidTag(string tag)
  {
    return Regex.IsMatch(
      tag,
      @"^[-_.\p{L}0-9()$!]+$");
  }

}

/// <summary>
/// Describes the relation of a keybag file and a <see cref="KeybagSet"/>
/// or a <see cref="KeybagDb"/>.
/// </summary>
public enum KeybagRelation
{
  /// <summary>
  /// The file does not exist or is not accessible
  /// </summary>
  Missing,

  /// <summary>
  /// The file exists but is not a keybag file (or in an unrecognized format)
  /// </summary>
  Unrecognized,

  /// <summary>
  /// The file is incompatible: a keybag file has a different key.
  /// </summary>
  Incompatible,

  /// <summary>
  /// The file belongs to a set that is not yet known to the DB (or
  /// just "the wrong Set", when comparing a file with a Set)
  /// </summary>
  NewSet,

  /// <summary>
  /// The file belongs to the set, but is not part of it (yet)
  /// </summary>
  Extern,

  /// <summary>
  /// The file is part of the set as synchronization target
  /// </summary>
  SyncTarget,

  /// <summary>
  /// The file is the primary keybag file for this set
  /// </summary>
  Primary,
}
