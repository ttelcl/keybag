/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model;

using Newtonsoft.Json;

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// JSON persisted model describing a Keybag and its copies
/// </summary>
public class KeybagSetDescriptor
{
  /// <summary>
  /// Create a new KeybagSetDescriptor
  /// </summary>
  public KeybagSetDescriptor(
    string tag,
    string fileId,
    string key,
    IEnumerable<KeybagReference> syncTargets)
  {
    Tag = tag;
    FileId = ChunkId.FromBase26(fileId);
    SyncTargets = syncTargets.ToList().AsReadOnly();
    if(!KeybagSet.IsValidTag(tag))
    {
      throw new InvalidOperationException(
        $"Not a valid KB3 file tag: '{tag}'");
    }
    KeyGuid = Guid.Parse(key);
  }

  /// <summary>
  /// The tag used for constructing file names
  /// (in the forms {tag}.{fileId26}.kb3 and {tag}.kb3)
  /// </summary>
  [JsonProperty("tag")]
  public string Tag { get; }

  /// <summary>
  /// The file ID in Base26 format
  /// </summary>
  [JsonProperty("fileId")]
  public string FileId26 { get => FileId.ToBase26(); }

  /// <summary>
  /// The key identifier (a GUID in string form)
  /// </summary>
  [JsonProperty("key")]
  public string Key { get => KeyGuid.ToString(); }

  /// <summary>
  /// The list of files to synchronize to (does not include
  /// the primary file)
  /// </summary>
  [JsonProperty("syncTargets")]
  public IReadOnlyList<KeybagReference> SyncTargets { get; }

  /// <summary>
  /// The file ID in binary format
  /// </summary>
  [JsonIgnore]
  public ChunkId FileId { get; }

  /// <summary>
  /// The key GUID
  /// </summary>
  [JsonIgnore]
  public Guid KeyGuid { get; }

}
