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
/// The stages of synchronization.
/// </summary>
public enum SynchronizationStage
{
  /// <summary>
  /// An error occurred during synchronization and it was aborted.
  /// </summary>
  Error = -1,

  /// <summary>
  /// Synchronization is not started yet.
  /// </summary>
  NotStarted = 0,

  /// <summary>
  /// Target keybags are being loaded. This is potentially a long
  /// operation, since it may involve the operating system connecting
  /// to remote storage or spinning up disks that are in power-saving
  /// mode.
  /// </summary>
  Loading,

  /// <summary>
  /// Target keybags have been loaded or are found to be unavailable
  /// or incompatible.
  /// </summary>
  Loaded,

  /// <summary>
  /// The primary keybag is being updated with new and modified chunks
  /// from the target keybags.
  /// </summary>
  Inhaling,

  /// <summary>
  /// The primary keybag has been updated with new and modified chunks
  /// </summary>
  Inhaled,

  /// <summary>
  /// The target keybags are being updated with new and modified chunks.
  /// </summary>
  Exhaling,

  /// <summary>
  /// The target keybags have been updated with new and modified chunks.
  /// </summary>
  Exhaled,

  /// <summary>
  /// The primary keybag and target keybags are being saved.
  /// </summary>
  Saving,

  /// <summary>
  /// The primary keybag and target keybags have been saved.
  /// </summary>
  Done,
}

/// <summary>
/// Stateful class that synchronizes the synchronization
/// targets in a <see cref="KeybagSet"/> with that set's
/// primary keybag.
/// </summary>
public class KeybagSynchronizer
{
  private List<SyncKeybag> _targets;

  /// <summary>
  /// Create a new KeybagSynchronizer
  /// </summary>
  public KeybagSynchronizer(
    KeybagSet kbs,
    Keybag primary)
  {
    Primary = primary;
    _targets = new List<SyncKeybag>();
    foreach(var target in kbs.SyncFiles)
    {
      _targets.Add(new SyncKeybag(target));
    }
    _targets.Sort((kbg1, kbg2) =>
      String.Compare(
        kbg1.Target.Location,
        kbg2.Target.Location,
        StringComparison.InvariantCultureIgnoreCase));
    Targets = _targets.AsReadOnly();
  }

  /// <summary>
  /// The loaded primary keybag in the set
  /// </summary>
  public Keybag Primary { get; }

  /// <summary>
  /// The synchronization targets
  /// </summary>
  public IReadOnlyList<SyncKeybag> Targets { get; }

  /// <summary>
  /// The number of synchronization targets that chunks
  /// were imported from during the Donation phase.
  /// </summary>
  public int PrimaryImportSourceCount { get; private set; }

  /// <summary>
  /// The total number of chunks that were changed in the
  /// Donation phase. If this is 0 there is no need to
  /// re-save the primary keybag.
  /// </summary>
  public int PrimaryChangedChunkCount { get; private set; }

  /// <summary>
  /// The number of synchronization targets that chunks
  /// were exported to during the Receiving phase.
  /// </summary>
  public int PrimaryExportTargetCount { get; private set; }

  /// <summary>
  /// Try loading each target keybag. After this call, the
  /// target keybags are either loaded or have an error message.
  /// </summary>
  public void TryLoadTargets(ChunkCryptor cryptor) 
  {
    foreach(var target in Targets)
    {
      target.TryLoad(cryptor);
    }
  }

  /// <summary>
  /// Donation phase: copy new and modified chunks from all
  /// targets into the primary keybag. But don't forget to load
  /// the target keybags first.
  /// </summary>
  /// <returns>
  /// The number of target keybags that donated any chunks at all.
  /// </returns>
  public int Inhale()
  {
    foreach(var target in Targets)
    {
      var status = target.GetStatus();
      if(!status.IsAvailable)
      {
        continue;
      }
      if(target.TargetKeybag == null)
      {
        throw new InvalidOperationException(
          "Missing call to TryLoad().");
      }
      target.Donate(Primary);
    }
    var count = Targets.Count(t => t.DonorChunkCount>0);
    PrimaryImportSourceCount = count;
    PrimaryChangedChunkCount =
      Primary.Chunks.CurrentChunks.Count(c => c.FileOffset == null);
    return count;
  }

  /// <summary>
  /// Receiving phase: copy new and modified chunks from the primary
  /// keybag into all available targets.
  /// </summary>
  /// <returns>
  /// The number of target keybags that received any chunks at all.
  /// </returns>
  public int Exhale()
  {
    foreach(var target in Targets)
    {
      if(target.IsAvailable)
      {
        target.Receive(Primary);
      }
    }
    var count = Targets.Count(t => t.RecipientChunkCount>0);
    PrimaryExportTargetCount = count;
    return count;
  }

}
