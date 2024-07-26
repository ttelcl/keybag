/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Storage;

using Keybag3.WpfUtilities;

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

  public bool IsReadOnly => Target.IsReadOnly;

  public bool IsInhaled =>
    Target.IsAvailable && Owner.Stage >= SynchronizationStage.Inhaled;

  public bool IsExhaled =>
    Target.IsAvailable && Owner.Stage >= SynchronizationStage.Exhaled;

  public bool HasUnsavedChanges {
    get => _hasUnsavedChanges;
    set {
      if(SetValueProperty(ref _hasUnsavedChanges, value))
      {
        RaisePropertyChanged(nameof(FileIconColor));
      }
    }
  }
  private bool _hasUnsavedChanges;

  public string FileIconColor {
    get {
      if(HasError)
      {
        return "Error";
      }
      if(HasUnsavedChanges)
      {
        return "Changed";
      }
      if(Owner.Stage == SynchronizationStage.NotStarted)
      {
        return "MidGray";
      }
      if(Target.IsLoaded && Owner.Stage >= SynchronizationStage.Exhaled)
      {
        // Also covers the "Done" stage - the HasUnsavedChanges
        // check above will take care of the Exhaled case where there
        // are unsaved changes.
        if(DonorChunkCount == 0 && RecipientChunkCount == 0)
        {
          return "Neutral";
        }
        else
        {
          return "OK";
        }
      }
      if(Target.IsLoaded)
      {
        return "Neutral";
      }
      return "Warning"; // ???
    }
  }

  public string FileIcon {
    get {
      if(HasError)
      {
        return "FileCancelOutline";
      }
      if(HasUnsavedChanges)
      {
        return "FileStarOutline";
      }
      if(Owner.Stage == SynchronizationStage.NotStarted)
      {
        return "FileQuestionOutline";
      }
      if(Target.IsLoaded && Owner.Stage >= SynchronizationStage.Exhaled)
      {
        // Also covers the "Done" stage - the HasUnsavedChanges
        // check above will take care of the Exhaled case where there
        // are unsaved changes.
        return "FileCheckOutline";
      }
      if(Target.IsLoaded)
      {
        return "FileOutline";
      }
      return "FileQuestionOutline";
    }
  }

  public string FileNameColor {
    get {
      if(HasError)
      {
        return "MidGray";
      }
      return FileIconColor;
    }
  }

  public string ExhaleCountColor {
    get {
      if(RecipientChunkCount == 0)
      {
        return "Neutral";
      }
      if(HasUnsavedChanges)
      {
        return "Changed";
      }
      else
      {
        return "OK";
      }
    }
  }

  public string InhaleCountColor {
    get {
      if(DonorChunkCount == 0)
      {
        return "Neutral";
      }
      return "OK";
    }
  }

  internal void Refresh()
  {
    HasUnsavedChanges = Target.HasUnsaved();
    RaisePropertyChanged(nameof(IsAvailable));
    RaisePropertyChanged(nameof(Error));
    RaisePropertyChanged(nameof(HasError));
    RaisePropertyChanged(nameof(DonorChunkCount));
    RaisePropertyChanged(nameof(RecipientChunkCount));
    RaisePropertyChanged(nameof(IsInhaled));
    RaisePropertyChanged(nameof(IsExhaled));
    RaisePropertyChanged(nameof(FileIconColor));
    RaisePropertyChanged(nameof(FileIcon));
    RaisePropertyChanged(nameof(FileNameColor));
    RaisePropertyChanged(nameof(ExhaleCountColor));
    RaisePropertyChanged(nameof(InhaleCountColor));
    RaisePropertyChanged(nameof(IsReadOnly));
  }
}
