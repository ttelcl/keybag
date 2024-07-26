/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

using Lcl.KeyBag3.Storage;

using Keybag3.WpfUtilities;
using Keybag3.Main.KeybagContent;
using Keybag3.Main.Database;

namespace Keybag3.Main.Synchronization;

public class SynchronizationViewModel: ViewModelBase
{
  private SynchronizationViewModel(
    KeybagViewModel target)
  {
    SetModel = target.Owner;
    SyncModel = target.CreateSynchronizer();
    SyncTargets = new ObservableCollection<SyncTargetViewModel>();
    foreach(var syncBag in SyncModel.Targets)
    {
      SyncTargets.Add(new SyncTargetViewModel(this, syncBag));
    }
    DoneCommand = new DelegateCommand(p => { PopMe(); });
    StepCommand = new DelegateCommand(
      p => { Step(); },
      p => StepEnabled);
    ConnectExistingCommand = new DelegateCommand(
      p => { ConnectExisting(); },
      p => Stage == SynchronizationStage.NotStarted
        || Stage == SynchronizationStage.Done);
    ExportAsTargetCommand = new DelegateCommand(
      p => { ExportAsTarget(); },
      p => Stage == SynchronizationStage.NotStarted
        || Stage == SynchronizationStage.Done);
  }

  public static bool TryPushOverlay(
    KeybagViewModel target)
  {
    if(!target.Decoded)
    {
      MessageBox.Show(
        "Cannot synchronize a keybag without its key.",
        "Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return false;
    }
    if(target.HasUnsavedChunks)
    {
      MessageBox.Show(
        "Cannot synchronize a keybag while it has unsaved changes.",
        "Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return false;
    }
    if(target.SearchFilter.SearchText.Length > 0)
    {
      // reset search
      target.SearchFilter.SearchText = "";
      target.SearchFilter.SearchKind = SearchKind.Tag;
      target.RecalculateMatches();
    }
    else
    {
      target.SearchFilter.SearchKind = SearchKind.Tag;
    }

    var syncModel = new SynchronizationViewModel(target);
    syncModel.PushMe();
    return true;
  }

  public ICommand DoneCommand { get; }

  public ICommand StepCommand { get; }

  public ICommand ConnectExistingCommand { get; }

  public ICommand ExportAsTargetCommand { get; }

  public KeybagSetViewModel SetModel { get; }

  public KeybagDbViewModel DbModel { get => SetModel.Owner; }

  public KeybagSynchronizer SyncModel { get; }

  public ObservableCollection<SyncTargetViewModel> SyncTargets { get; }

  public int PrimaryImportSourceCount { get => SyncModel.PrimaryImportSourceCount; }

  public int PrimaryChangedChunkCount { get => SyncModel.PrimaryChangedChunkCount; }

  public int PrimaryExportTargetCount { get => SyncModel.PrimaryExportTargetCount; }

  public SynchronizationStage Stage {
    get => _stage;
    set {
      if(SetValueProperty(ref _stage, value))
      {
        RaisePropertyChanged(nameof(NextStepText));
        RaisePropertyChanged(nameof(StepEnabled));
        RaisePropertyChanged(nameof(IsInhaled));
        RaisePropertyChanged(nameof(InhaledCountColor));
      }
    }
  }
  private SynchronizationStage _stage = SynchronizationStage.NotStarted;

  public string NextStepText {
    get => Stage switch {
      SynchronizationStage.NotStarted => "Start Synchronizing!",
      SynchronizationStage.Loading => "(loading...)",
      SynchronizationStage.Loaded => "Inhale",
      SynchronizationStage.Inhaling => "(importing)",
      SynchronizationStage.Inhaled => "Exhale",
      SynchronizationStage.Exhaling => "(exporting)",
      SynchronizationStage.Exhaled => "Save",
      SynchronizationStage.Saving => "(saving...)",
      SynchronizationStage.Done => "Completed",
      SynchronizationStage.Error => "Aborted",
      _ => throw new InvalidOperationException("Invalid stage"),
    };
  }

  public bool StepEnabled {
    get => Stage switch {
      SynchronizationStage.NotStarted => true,
      SynchronizationStage.Loaded => true,
      SynchronizationStage.Inhaled => true,
      SynchronizationStage.Exhaled => true,
      SynchronizationStage.Done => false,
      SynchronizationStage.Error => false,
      _ => false,
    };
  }

  public bool IsInhaled =>
    Stage >= SynchronizationStage.Inhaled;

  public string InhaledCountColor {
    get => SyncModel.PrimaryChangedChunkCount == 0
      ? "LightGray"
      : Stage > SynchronizationStage.Saving ? "OK" : "Changed";
  }

  public void Step()
  {
    try
    {
      Mouse.OverrideCursor = Cursors.Wait;
      switch(Stage)
      {
        case SynchronizationStage.NotStarted:
          Load();
          break;
        case SynchronizationStage.Loaded:
          Inhale();
          break;
        case SynchronizationStage.Inhaled:
          Exhale();
          break;
        case SynchronizationStage.Exhaled:
          Save();
          break;
        case SynchronizationStage.Done:
          break;
        case SynchronizationStage.Error:
          MessageBox.Show(
            "Synchronization has been aborted.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          break;
        default:
          throw new InvalidOperationException("Invalid stage");
      }
    }
    catch(Exception)
    {
      Stage = SynchronizationStage.Error;
      throw;
    }
    finally
    {
      Mouse.OverrideCursor = null;
    }
  }

  private void StartStage(
    SynchronizationStage expected,
    SynchronizationStage newStage)
  {
    if(Stage != expected)
    {
      Stage = SynchronizationStage.Error;
      throw new InvalidOperationException(
        $"Expected stage {expected}, but was {Stage}");
    }
    Stage = newStage;
  }

  private void CompleteStage(
    SynchronizationStage expected,
    SynchronizationStage newStage)
  {
    if(Stage == SynchronizationStage.Error)
    {
      return;
    }
    if(Stage != expected)
    {
      Stage = SynchronizationStage.Error;
      throw new InvalidOperationException(
        $"Expected stage {expected}, but was {Stage}");
    }
    Stage = newStage;
  }

  private void Load()
  {
    var key = SetModel.FindKey();
    if(key == null)
    {
      MessageBox.Show(
        "Internal error - keybag is not unlocked",
        "Internal error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      Stage = SynchronizationStage.Error;
      return;
    }
    StartStage(SynchronizationStage.NotStarted, SynchronizationStage.Loading);
    foreach(var target in SyncTargets)
    {
      target.Target.TryLoad(key);
      target.Refresh();
    }
    CompleteStage(SynchronizationStage.Loading, SynchronizationStage.Loaded);
  }

  private void Inhale()
  {
    StartStage(SynchronizationStage.Loaded, SynchronizationStage.Inhaling);
    SyncModel.Inhale();
    CompleteStage(SynchronizationStage.Inhaling, SynchronizationStage.Inhaled);
    foreach(var target in SyncTargets)
    {
      target.Refresh();
    }
    RaisePropertyChanged(nameof(PrimaryImportSourceCount));
    RaisePropertyChanged(nameof(PrimaryChangedChunkCount));
  }

  private void Exhale()
  {
    StartStage(SynchronizationStage.Inhaled, SynchronizationStage.Exhaling);
    SyncModel.Exhale();
    CompleteStage(SynchronizationStage.Exhaling, SynchronizationStage.Exhaled);
    foreach(var target in SyncTargets)
    {
      target.Refresh();
    }
    RaisePropertyChanged(nameof(PrimaryExportTargetCount));
  }

  private void Save()
  {
    StartStage(SynchronizationStage.Exhaled, SynchronizationStage.Saving);
    var key = SetModel.FindKey();
    if(key == null)
    {
      MessageBox.Show(
        "Internal error - keybag is not unlocked",
        "Internal error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      Stage = SynchronizationStage.Error;
      return;
    }
    var primary = SyncModel.Primary;
    if(primary.HasUnsavedChunks())
    {
      Trace.TraceInformation(
        $"Saving primary {SetModel.Model.PrimaryFile}");
      // We need to use a low-level save here, because "Target" is
      // not kept updated during synchronization
      primary.WriteFull(SetModel.Model.PrimaryFile, key, true);
    }
    else
    {
      Trace.TraceInformation(
        $"No changes in primary {SetModel.Model.PrimaryFile}");
    }
    foreach(var target in SyncTargets)
    {
      target.HasUnsavedChanges = !target.IsReadOnly && target.Target.HasUnsaved();
      if(target.HasUnsavedChanges && !target.IsReadOnly)
      {
        Trace.TraceInformation(
          $"Saving target {target.TargetFullFile}");
        target.Target.TrySave(key);
      }
      target.Refresh();
    }

    CompleteStage(SynchronizationStage.Saving, SynchronizationStage.Done);
  }

  private void ConnectExisting()
  {
    var result = MessageBox.Show(
      "Connecting existing keybags is done from the main database view.\n" +
      "Go there now?",
      "Redirect",
      MessageBoxButton.YesNo,
      MessageBoxImage.Question);
    if(result == MessageBoxResult.Yes)
    {
      PopMe();
      DbModel.StartImport();
    }
  }

  private void ExportAsTarget()
  {
    MessageBox.Show(
      "Exporting keybags as new sync targets is not implemented yet",
      "Under Development",
      MessageBoxButton.OK,
      MessageBoxImage.Warning);
  }

  private void PushMe()
  {
    DbModel.AppModel.PushOverlay(this);
  }

  private void PopMe()
  {
    if(Stage < SynchronizationStage.NotStarted
      || Stage > SynchronizationStage.Loaded)
    {
      // Anything might have happened to the primary keybag,
      // so reload it just to be safe. This can only be safely
      // skipped if we didn't even get to the inhale phase and
      // there were no errors.
      SetModel.Reload(true);
    }
    DbModel.AppModel.PopOverlay(this);
  }

}
