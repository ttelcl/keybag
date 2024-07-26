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

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// An immutable record that describes the status of a keybag
/// in a consistent view.
/// </summary>
/// <param name="IsAvailable">
/// True if the keybag may be available for synchronization.
/// False if it definitely is not available and should be ignored.
/// </param>
/// <param name="Error">
/// If not null: a description of the problem preventing proper
/// loading.
/// </param>
/// <param name="TargetKeybag">
/// If not null: the successfully loaded keybag.
/// </param>
public record SyncKeybagStatus(
  bool IsAvailable,
  string? Error,
  Keybag? TargetKeybag)
{
  /// <summary>
  /// True if the keybag has been loaded successfully.
  /// Implies <see cref="IsAvailable"/> == true and
  /// <see cref="TargetKeybag"/> != null.
  /// </summary>
  public bool IsLoaded => IsAvailable && TargetKeybag != null;
}

/// <summary>
/// Represents a keybag in a <see cref="KeybagSet"/> that
/// is to be synchronized with the primary keybag in that set
/// during the synchronization process.
/// </summary>
public class SyncKeybag
{
  private object _lock = new object();

  /// <summary>
  /// Create a new SyncKeybag
  /// </summary>
  public SyncKeybag(
    KeybagReference target)
  {
    Target = target;
    IsAvailable = target.IsAvailable();
    DonorChunkCount = 0;
    RecipientChunkCount = 0;
    if(!IsAvailable)
    {
      Error = "Target file not available";
    }
  }

  /// <summary>
  /// Identifies the target keybag file.
  /// </summary>
  public KeybagReference Target { get; }

  /// <summary>
  /// True if this keybag file is available for synchronization,
  /// false if it should be ignored. Also changed to false if
  /// the keybag failed to load.
  /// (must only be set through <see cref="SetStatus(bool, string?, Keybag?)"/>)
  /// </summary>
  public bool IsAvailable { get; private set; }

  /// <summary>
  /// True if the keybag has been loaded successfully.
  /// </summary>
  public bool IsLoaded => GetStatus().IsLoaded;

  /// <summary>
  /// If not null: a description of the problem preventing proper
  /// synchronization.
  /// (must only be set through <see cref="SetStatus(bool, string?, Keybag?)"/>)
  /// </summary>
  public string? Error { get; private set; }

  /// <summary>
  /// The keybag holding this target's content.
  /// (must only be set through <see cref="SetStatus(bool, string?, Keybag?)"/>)
  /// </summary>
  public Keybag? TargetKeybag { get; private set; }

  /// <summary>
  /// The number of chunks this keybag file donated to the primary keybag.
  /// </summary>
  public int DonorChunkCount { get; private set; }

  /// <summary>
  /// The number of chunks this keybag file received from the primary keybag.
  /// </summary>
  public int RecipientChunkCount { get; private set; }

  /// <summary>
  /// Return a thread-safe snapshot of the current status fields.
  /// </summary>
  public SyncKeybagStatus GetStatus()
  {
    lock(_lock)
    {
      return new SyncKeybagStatus(IsAvailable, Error, TargetKeybag);
    }
  }

  /// <summary>
  /// Set the status fields together, in a thread-safe way.
  /// </summary>
  public void SetStatus(
    bool isAvailable,
    string? error,
    Keybag? targetKeybag)
  {
    if(!String.IsNullOrEmpty(error) && isAvailable)
    {
      throw new ArgumentException(
        "If an error is provided, 'isAvailable' should be false");
    }
    if(targetKeybag != null && !isAvailable)
    {
      throw new ArgumentException(
        "If a keybag is provided, 'isAvailable' should be true");
    }
    lock(_lock)
    {
      IsAvailable = isAvailable;
      Error = error;
      TargetKeybag = targetKeybag;
    }
  }

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
    var status = GetStatus();
    if(!status.IsAvailable)
    {
      return false;
    }
    if(status.TargetKeybag != null)
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
      SetStatus(false, "Load Error: " + ex.Message, null);
      return false;
    }
    if(!kbg.IsSealValidated)
    {
      // unlikely to happen, but just in case (this would already throw above)
      SetStatus(false, "Seal validation failed", null);
      return false;
    }
    SetStatus(true, null, kbg);
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
    var status = GetStatus();
    if(!status.IsAvailable)
    {
      return;
    }
    if(!status.IsLoaded)
    {
      throw new InvalidOperationException(
        "Expecting sync keybag to have been loaded already");
    }
    foreach(var syncChunk in TargetKeybag!.Chunks.CurrentChunks)
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
          SetStatus(false, "File header mismatch", null);
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
    var status = GetStatus();
    if(!status.IsAvailable)
    {
      return;
    }
    if(!status.IsLoaded)
    {
      throw new InvalidOperationException(
        "Expecting sync keybag to have been loaded already");
    }
    foreach(var primaryChunk in primary.Chunks.CurrentChunks)
    {
      if(primaryChunk.Kind != ChunkKind.File)
      {
        var syncChunk = TargetKeybag!.Chunks.FindChunk(primaryChunk.NodeId);
        if(syncChunk == null
          || syncChunk.EditId.Value < primaryChunk.EditId.Value)
        {
          // Only import if the target does not have a newer or same version
          // (it should never have a newer version, but same is quite likely)
          TargetKeybag!.Chunks.PutChunk(primaryChunk.Clone());
          RecipientChunkCount++;
        }
      }
    }
  }

  /// <summary>
  /// Returns true if the keybag has been loaded and has unsaved changes.
  /// </summary>
  public bool HasUnsaved()
  {
    var status = GetStatus();
    if(!status.IsAvailable)
    {
      return false;
    }
    if(!status.IsLoaded)
    {
      return false;
    }
    return TargetKeybag!.HasUnsavedChunks();
  }

  /// <summary>
  /// Try to save the target keybag. If the keybag is not available,
  /// not in the right state, or has no changes, it is not saved.
  /// If the target file exists but is flagged as readonly it is not saved
  /// either.
  /// </summary>
  public void TrySave(
    ChunkCryptor cryptor)
  {
    var status = GetStatus();
    if(status.IsAvailable && status.IsLoaded && TargetKeybag != null)
    {
      if(TargetKeybag.HasUnsavedChunks())
      {
        var fileName = Target.Location;
        var fileInfo = new FileInfo(fileName);
        if(fileInfo.Exists && fileInfo.IsReadOnly)
        {
          Trace.TraceWarning(
            $"Not overwriting 'read only' file '{fileName}'");
          return;
        }
        Trace.TraceInformation(
          $"Saving sync target {fileName}");
        TargetKeybag.WriteFull(
          fileName,
          cryptor,
          true);
      }
    }
  }


  // --
}
