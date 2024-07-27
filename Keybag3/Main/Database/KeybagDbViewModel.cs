/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Lcl.KeyBag3.Storage;
using Lcl.KeyBag3.Model;

using Keybag3.WpfUtilities;

namespace Keybag3.Main.Database;

public enum KeybagSortOrder
{
  ByTag,
  ByLastModified,
}

public class KeybagDbViewModel:
  ViewModelBase<KeybagDb>, IRefreshable, IHasViewTitle
{
  public KeybagDbViewModel(
    MainViewModel appModel)
    : base(appModel.Services.KeybagDatabase)
  {
    AppModel = appModel;
    KeybagSets = new ObservableCollection<KeybagSetViewModel>();
    NewKeybagCommand = new DelegateCommand(p => { StartNewKeybag(); });
    ImportConnectCommand = new DelegateCommand(p => { StartImport(); });

    OverlayHost = appModel;

    var orderSettings = Model.AppStateView.GetEnumView(
      KeybagSortOrder.ByTag);
    if(orderSettings.IsPropertyKnown(__sortOrderKey))
    {
      _sortOrder = orderSettings[__sortOrderKey];
    }
    else
    { 
      _sortOrder = orderSettings.DefaultValue;
      orderSettings[__sortOrderKey] = _sortOrder;
      Model.AppStateStore.Save();
    }

    _defaultKeybagId = Model.AppStateView.Strings[__defaultKeybagKey];

    // TEMPORARY
    TestOverlayTestCommand = new DelegateCommand(p => {
      OverlayHost.PushOverlay(new TestOverlayViewModel(OverlayHost));
    });
    TestEmptyCommand = new DelegateCommand(p => {
      IsEmpty = IsEmpty ? KeybagSets.Count == 0 : true;
    });

    Refresh();
  }

  public ICommand NewKeybagCommand { get; }

  public ICommand ImportConnectCommand { get; }

  public ICommand TestOverlayTestCommand { get; }

  public ICommand TestEmptyCommand { get; }

  public MainViewModel AppModel { get; }

  public ISupportsOverlay OverlayHost { get; }

  public IHasCurrentView ViewHost { get => AppModel; }

  /// <summary>
  /// The application is terminating (due to user request).
  /// Last chance to save data.
  /// Returns true on success, false if the user canceled the
  /// termination.
  /// </summary>
  /// <returns>
  /// Returns true on success, false if the user canceled the
  /// termination.
  /// </returns>
  public bool AppTerminating()
  {
    var unsavedSets = KeybagSets
      .Where(kbs => kbs.SavePending)
      .ToList();
    if(unsavedSets.Count > 0)
    {
      var unsavedNames = string.Join(
        "', '",
        unsavedSets.Select(kbs => kbs.Tag));
      var msg =
        unsavedSets.Count > 1
        ? $"The following keybag sets have unsaved changes: \n'{unsavedNames}'. \n" +
          "Do you want to save them all before exiting?"
        : $"The following keybag set has unsaved changes: \n'{unsavedNames}'. \n" +
          "Do you want to save it before exiting?";
      var result = MessageBox.Show(
        msg,
        "Unsaved Changes",
        MessageBoxButton.YesNoCancel,
        MessageBoxImage.Question);
      switch(result)
      {
        case MessageBoxResult.Yes:
          foreach(var kbs in unsavedSets)
          {
            kbs.Save();
          }
          return true;
        case MessageBoxResult.No:
          return true;
        default:
          return false;
      }
    }
    else
    {
      return true;
    }
  }

  public ObservableCollection<KeybagSetViewModel> KeybagSets { get; }

  public bool IsKnownTag(string tag)
  {
    return KeybagSets.Any(
      kbs => kbs.Tag.Equals(tag, StringComparison.InvariantCultureIgnoreCase));
  }

  public string Title { get => "Keybag Database"; }

  public bool IsEmpty {
    get => _isEmpty;
    set {
      if(SetValueProperty(ref _isEmpty, value))
      {
        RaisePropertyChanged(nameof(NotEmpty));
      }
    }
  }
  private bool _isEmpty = true;

  public bool NotEmpty {
    get => !_isEmpty;
  }

  public void StartNewKeybag()
  {
    ViewHost.CurrentView = new NewKeybagViewModel(this);
    //OverlayHost.PushOverlay(new NewKeybagViewModel(this));
  }

  public void StartImport()
  {
    ViewHost.CurrentView = new ImportConnectViewModel(this);
    //OverlayHost.PushOverlay(new ImportConnectViewModel(this));
  }

  public void Refresh()
  {
    var modelIndex = Model.KeybagSets.ToDictionary(kbs => kbs.FileId);
    var missing =
      KeybagSets
      .Where(kbsvm => !modelIndex.ContainsKey(kbsvm.FileId))
      .ToList();
    foreach(var kbsvm in missing)
    {
      KeybagSets.Remove(kbsvm);
    }
    var knownIndex = KeybagSets.ToDictionary(kbsvm => kbsvm.FileId);
    var newSets =
      Model.KeybagSets
      .Where(kbs => !knownIndex.ContainsKey(kbs.FileId))
      .ToList();
    foreach(var newSet in newSets)
    {
      var vm = new KeybagSetViewModel(this, newSet);
      KeybagSets.Add(vm);
    }
    IsEmpty = KeybagSets.Count == 0;
    UpdateDefaultKeybag();
    SortKeybags();
  }

  public KeybagSortOrder SortOrder {
    get => _sortOrder;
    set {
      if(SetValueProperty(ref _sortOrder, value))
      {
        SortKeybags();
        var settingView = Model.AppStateView.GetEnumView(
          KeybagSortOrder.ByTag);
        settingView[__sortOrderKey] = value;
        Model.AppStateStore.Save();
      }
    }
  }
  private KeybagSortOrder _sortOrder = KeybagSortOrder.ByTag;
  private const string __sortOrderKey = "keybag_sortorder";

  public string? DefaultKeybagId {
    get => _defaultKeybagId;
    set {
      if(SetValueProperty(ref _defaultKeybagId, value))
      {
        UpdateDefaultKeybag();
        Model.AppStateView.Strings[__defaultKeybagKey] = value;
        Model.AppStateStore.Save();
      }
    }
  }
  private string? _defaultKeybagId = null;
  private const string __defaultKeybagKey = "default_keybag";

  public KeybagSetViewModel? DefaultKeybag {
    get => _defaultKeybag;
    private set {
      // Must only be called from UpdateDefaultKeybag
      // To change, set DefaultKeybagId instead or
      // call SetDefaultKeybag
      var old = _defaultKeybag;
      if(SetNullableInstanceProperty(ref _defaultKeybag, value))
      {
        if(old!=null)
        {
          old.IsDefault = false;
        }
        if(value!=null)
        {
          value.IsDefault = true;
        }
      }
    }
  }
  private KeybagSetViewModel? _defaultKeybag = null;

  public void UpdateDefaultKeybag()
  {
    if(DefaultKeybagId != null)
    {
      Trace.TraceInformation(
        $"Updating default keybag being {DefaultKeybagId}");
      DefaultKeybag = KeybagSets.FirstOrDefault(
        kbs => kbs.Id26 == DefaultKeybagId);
      if(DefaultKeybag == null)
      {
        Trace.TraceError(
          $"There is no keybag matching the default keybag ID {DefaultKeybagId}");
      }
    }
    else
    {
      Trace.TraceInformation(
        "Updating default keybag to be none");
      DefaultKeybag = null;
    }
  }

  public void SetDefaultKeybag(KeybagSetViewModel? kbs)
  {
    DefaultKeybagId = kbs?.Id26;
    SortKeybags();
  }

  public void SortKeybags()
  {
    var target = KeybagSets.ToList();
    var cmp = StringComparer.InvariantCultureIgnoreCase;
    switch(SortOrder)
    {
      case KeybagSortOrder.ByTag:
        target.Sort(
          (a, b) => cmp.Compare(a.Tag, b.Tag));
        break;
      case KeybagSortOrder.ByLastModified:
        target.Sort((a, b) => -a.EditId.Value.CompareTo(b.EditId.Value));
        break;
    }
    // Move default keybag (if any) to top
    if(DefaultKeybagId != null)
    {
      var defIndex = target.FindIndex(
        kbs => kbs.Id26 == DefaultKeybagId);
      if(defIndex >= 0)
      {
        var defKbs = target[defIndex];
        target.RemoveAt(defIndex);
        target.Insert(0, defKbs);
      }
    }
    var inOrder = target.SequenceEqual(KeybagSets);
    if(!inOrder)
    {
      Trace.TraceInformation("Reordering keybags");
      KeybagSets.Clear();
      foreach(var kbs in target)
      {
        KeybagSets.Add(kbs);
      }
    }
  }

  /// <summary>
  /// The status of a key may have changed
  /// </summary>
  public void KeyStatusChanged(Guid keyId)
  {
    foreach(var kbs in KeybagSets)
    {
      if(kbs.Model.KeyGuid == keyId)
      {
        kbs.Refresh();
      }
    }
  }
}
