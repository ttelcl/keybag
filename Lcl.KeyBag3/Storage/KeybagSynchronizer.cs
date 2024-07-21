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
    KbSet = kbs;
    Primary = primary;
    _targets = new List<SyncKeybag>();
    foreach(var target in kbs.SyncFiles)
    {
      _targets.Add(new SyncKeybag(kbs, target));
    }
    _targets.Sort((kbg1, kbg2) =>
      String.Compare(
        kbg1.Target.Location,
        kbg2.Target.Location,
        StringComparison.InvariantCultureIgnoreCase));
    Targets = _targets.AsReadOnly();
  }

  /// <summary>
  /// The keybag set to synchronize
  /// </summary>
  public KeybagSet KbSet { get; }

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
  /// Donation phase: copy new and modified chunks from all
  /// targets into the primary keybag. But don't forget to load
  /// the target keybags first.
  /// </summary>
  /// <param name="cryptor">
  /// The key to validate correct loading of the target keybags.
  /// </param>
  /// <returns>
  /// The number of target keybags that donated any chunks at all.
  /// </returns>
  public int LoadAndDonateToPrimary(
    ChunkCryptor cryptor)
  {
    foreach(var target in Targets)
    {
      if(target.TryLoad(cryptor))
      {
        target.Donate(Primary);
      }
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
  public int ReceiveFromPrimary()
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
