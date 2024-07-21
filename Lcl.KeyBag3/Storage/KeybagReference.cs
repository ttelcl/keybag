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

using Newtonsoft.Json;

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// Points to a copy of the keybag file that may or may not exist
/// (e.g. be on a USB drive that is not plugged in)
/// </summary>
public class KeybagReference
{
  /// <summary>
  /// Deserialization constructor.
  /// </summary>
  public KeybagReference(
    string location,
    string volume)
  {
    Location=location;
    Volume=volume;
  }

  /// <summary>
  /// Create a new KeybagReference
  /// </summary>
  public static KeybagReference CreateNew(
    string location)
  {
    location = Path.GetFullPath(location);
    var fid = FileIdentifier.FromPath(location);
    if(fid==null)
    {
      throw new FileNotFoundException(
        $"File not found", location);
    }
    return new KeybagReference(location, fid.VolumeSerial);
  }

  /// <summary>
  /// Create a new KeybagReference
  /// </summary>
  public static KeybagReference? TryCreateNew(
    string location)
  {
    location = Path.GetFullPath(location);
    var fid = FileIdentifier.FromPath(location);
    if(fid==null)
    {
      return null;
    }
    return new KeybagReference(location, fid.VolumeSerial);
  }

  /// <summary>
  /// The fully qualified path where to expect the file.
  /// </summary>
  [JsonProperty("location")]
  public string Location { get; }

  /// <summary>
  /// The volume serial number of the drive the file is on
  /// (as an 8 character hex number). This is the serial number
  /// of the drive the file actually is on, even if the path
  /// includes junctions.
  /// </summary>
  [JsonProperty("volume")]
  public string Volume { get; }

  /// <summary>
  /// Test if the file is available on its expected volume.
  /// </summary>
  public bool IsAvailable()
  {
    return FileIdentifier.FileAvailable(Location, Volume);
  }
}
