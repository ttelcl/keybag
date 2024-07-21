/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Lcl.KeyBag3.Storage;

using Keybag3.WpfUtilities;
using System.IO;

namespace Keybag3.Main.Synchronization;

public class SyncTargetViewModel: ViewModelBase
{
  public SyncTargetViewModel(
    SynchronizationViewModel owner,
    SyncKeybag target)
  {
    Owner = owner;
    Target = target;
    TargetFullFile = target.Target.Location;
  }

  public SynchronizationViewModel Owner { get; }

  public SyncKeybag Target { get; }

  public string TargetFullFile { get; }

  public bool IsAvailable => Target.IsAvailable;

  public string? Error => Target.Error;

  public bool HasError => !String.IsNullOrEmpty(Error);

  public int DonorChunkCount => Target.DonorChunkCount;

  public int RecipientChunkCount => Target.RecipientChunkCount;

  internal void Refresh()
  {
    RaisePropertyChanged(nameof(IsAvailable));
    RaisePropertyChanged(nameof(Error));
    RaisePropertyChanged(nameof(HasError));
    RaisePropertyChanged(nameof(DonorChunkCount));
    RaisePropertyChanged(nameof(RecipientChunkCount));
  }
}
