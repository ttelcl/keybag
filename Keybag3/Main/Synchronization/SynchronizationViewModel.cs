/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

using Keybag3.WpfUtilities;
using Keybag3.Main.KeybagContent;
using Keybag3.Main.Database;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Lcl.KeyBag3.Storage;

namespace Keybag3.Main.Synchronization;

public class SynchronizationViewModel: ViewModelBase
{
  private SynchronizationViewModel(
    KeybagViewModel target)
  {
    Target = target;
    SyncModel = Target.CreateSynchronizer();
    SyncTargets = new ObservableCollection<SyncTargetViewModel>();
    foreach(var syncBag in SyncModel.Targets)
    {
      SyncTargets.Add(new SyncTargetViewModel(this, syncBag));
    }
    DoneCommand = new DelegateCommand(p => { PopMe(); });
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

  public KeybagViewModel Target { get; }

  public KeybagSetViewModel SetModel { get => Target.Owner; }

  public KeybagDbViewModel DbModel { get => SetModel.Owner; }

  public KeybagSynchronizer SyncModel { get; }

  public ObservableCollection<SyncTargetViewModel> SyncTargets { get; }

  public int PrimaryImportSourceCount { get => SyncModel.PrimaryImportSourceCount; }

  public int PrimaryChangedChunkCount { get => SyncModel.PrimaryChangedChunkCount; }

  public int PrimaryExportTargetCount { get => SyncModel.PrimaryExportTargetCount; }

  public void LoadAndDonateToPrimary()
  {
    var key = SetModel.FindKey();
    if(key == null)
    {
      MessageBox.Show(
        "Internal error - keybag is not unlocked",
        "Internal error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    SyncModel.LoadAndDonateToPrimary(key);
    RaisePropertyChanged(nameof(PrimaryImportSourceCount));
    RaisePropertyChanged(nameof(PrimaryChangedChunkCount));
  }


  private void PushMe()
  {
    DbModel.AppModel.PushOverlay(this);
  }

  private void PopMe()
  {
    DbModel.AppModel.PopOverlay(this);
  }

}
