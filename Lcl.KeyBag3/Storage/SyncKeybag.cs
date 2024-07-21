/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Model;

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// Represents a keybag in a <see cref="KeybagSet"/> that
/// is to be synchronized with the primary keybag in that set
/// during the synchronization process.
/// </summary>
public class SyncKeybag
{
  /// <summary>
  /// Create a new SyncKeybag
  /// </summary>
  public SyncKeybag(
    KeybagSet host,
    KeybagReference target)
  {
    Host = host;
    Target = target;
    IsAvailable = target.IsAvailable();
    DonorChunkCount = 0;
    RecipientChunkCount = 0;
    if(!IsAvailable)
    {
      Error = "Keybag file not available";
    }
  }

  /// <summary>
  /// The host keybag set that contains this sync target.
  /// </summary>
  public KeybagSet Host { get; }

  /// <summary>
  /// Identifies the target keybag file.
  /// </summary>
  public KeybagReference Target { get; }

  /// <summary>
  /// True if this keybag file is available for synchronization,
  /// false if it should be ignored. Also changed to false if
  /// the keybag failed to load.
  /// </summary>
  public bool IsAvailable { get; private set; }

  /// <summary>
  /// If not null: a description of the problem preventing proper
  /// synchronization.
  /// </summary>
  public string? Error { get; private set; }

  /// <summary>
  /// The number of chunks this keybag file donated to the primary keybag.
  /// </summary>
  public int DonorChunkCount { get; private set; }

  /// <summary>
  /// The number of chunks this keybag file received from the primary keybag.
  /// </summary>
  public int RecipientChunkCount { get; private set; }

  /// <summary>
  /// The keybag holding this target's content.
  /// </summary>
  public Keybag? TargetKeybag { get; private set; }

  /// <summary>
  /// Try loading the keybag file for this target. If this fails,
  /// <see cref="IsAvailable"/> is cleared to false.
  /// </summary>
  /// <param name="cryptor">
  /// The key to validate the keybag content and seals.
  /// </param>
  /// <returns>
  /// True if the keybag is now available.
  /// </returns>
  public bool TryLoad(ChunkCryptor cryptor)
  {
    if(!IsAvailable)
    {
      return false;
    }
    if(TargetKeybag != null)
    {
      return true;
    }
    Keybag kbg;
    try
    {
      kbg = Keybag.FromFile(Target.Location);
      kbg.ValidateSeals(cryptor);
    }
    catch(Exception ex)
    {
      Error = "Load Error: " + ex.Message;
      IsAvailable = false;
      return false;
    }
    if(!kbg.IsSealValidated)
    {
      // unlikely to happen, but just in case (this would already throw above)
      Error = "Seal validation failed";
      IsAvailable = false;
      return false;
    }
    TargetKeybag = kbg;
    return true;
  }

  /// <summary>
  /// Insert chunks from this sync target into the primary keybag
  /// that are not already present or are newer than the primary's.
  /// After this call <see cref="DonorChunkCount"/> is valid.
  /// </summary>
  /// <param name="primary">
  /// The primary keybag to import into
  /// </param>
  public void Donate(
    Keybag primary)
  {
    if(!IsAvailable)
    {
      return;
    }
    if(TargetKeybag == null)
    {
      throw new InvalidOperationException(
        "Expecting sync keybag to have been loaded already");
    }
    foreach(var syncChunk in TargetKeybag.Chunks.CurrentChunks)
    {
      if(syncChunk.Kind != ChunkKind.File)
      {
        var primaryChunk = primary.Chunks.FindChunk(syncChunk.NodeId);
        if(primaryChunk == null
          || primaryChunk.EditId.Value < syncChunk.EditId.Value)
        {
          // only import if the primary does not have a newer or same version
          primary.Chunks.PutChunk(syncChunk.Clone());
          DonorChunkCount++;
        }
      }
      else
      {
        if(primary.FileChunk.AuthCode != syncChunk.AuthCode)
        {
          Trace.TraceError(
            $"File header does not match primary: {Target.Location}. " +
            "Aborting Donate phase.");
          Error = "File header does match. Import aborted.";
          IsAvailable = false;
          return;
        }
      }
    }
  }

  /// <summary>
  /// Insert chunks from the primary keybag into this sync target
  /// </summary>
  /// <param name="primary">
  /// The primary keybag to receive from
  /// </param>
  public void Receive(
    Keybag primary)
  {
    if(!IsAvailable)
    {
      return;
    }
    if(TargetKeybag == null)
    {
      throw new InvalidOperationException(
        "Expecting sync keybag to have been loaded already");
    }
    foreach(var primaryChunk in primary.Chunks.CurrentChunks)
    {
      if(primaryChunk.Kind != ChunkKind.File)
      {
        var syncChunk = TargetKeybag.Chunks.FindChunk(primaryChunk.NodeId);
        if(syncChunk == null
          || syncChunk.EditId.Value < primaryChunk.EditId.Value)
        {
          // only import if the sync does not have a newer or same version
          TargetKeybag.Chunks.PutChunk(primaryChunk.Clone());
          RecipientChunkCount++;
        }
      }
    }
  }

  // --
}
