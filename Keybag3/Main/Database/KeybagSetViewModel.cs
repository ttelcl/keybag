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
using System.Windows;
using System.Windows.Input;

using Microsoft.Win32;

using Lcl.KeyBag3.Storage;
using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Model;

using Keybag3.Main.KeybagContent;
using Keybag3.Main.Synchronization;
using Keybag3.WpfUtilities;

namespace Keybag3.Main.Database;

/// <summary>
/// Dual purpose view model for a keybag set: this is used both
/// as ViewModel for the list entries in the main keybag database view,
/// and as outer ViewModel for the keybag view itself (which can be
/// locked or unlocked).
/// </summary>
public class KeybagSetViewModel:
  ViewModelBase<KeybagSet>, IRefreshable, IHasViewTitle
{
  public KeybagSetViewModel(
    KeybagDbViewModel owner,
    KeybagSet kbset)
    : base(kbset)
  {
    Owner = owner;
    TryUnlockCommand = new DelegateCommand(
      p => { TryStartPassphraseOverlay(); },
      p => !KeyKnown);
    ViewSetCommand = new DelegateCommand(p => { ViewThisSet(); });
    ViewDatabaseCommand = new DelegateCommand(
      p => { BackToDatabase(); });
    ToggleContentCommand = new DelegateCommand(
      p => { ShowingContent = !ShowingContent; });
    SaveContentCommand = new DelegateCommand(
      p => {
        if(KeybagModel != null)
        {
          KeybagModel.Save();
        }
        else
        {
          Trace.TraceError("SaveContentCommand: KeybagModel is null");
        }
      },
      p => KeybagModel != null
        && KeybagModel.Decoded
        && KeybagModel.HasUnsavedChunks);
    ShowSyncOverlayCommand = new DelegateCommand(
      p => {
        if(KeybagModel!= null)
        {
          SynchronizationViewModel.TryPushOverlay(
            KeybagModel,
            EnableAutoSync);
        }
      },
      p => KeybagModel!= null
        && KeybagModel.Decoded
        && !KeybagModel.HasUnsavedChunks);
    DiscardCommand = new DelegateCommand(
      p => { Discard(); },
      p => KeyKnownAndShowing
        && KeybagModel != null
        && KeybagModel.Decoded
        && KeybagModel.HasUnsavedChunks);
    EjectCommand = new DelegateCommand(
      p => { Eject(); },
      p => KeyKnown
        && KeybagModel != null
        && !KeybagModel.HasUnsavedChunks);
    Refresh();
  }

  public ICommand TryUnlockCommand { get; }

  /// <summary>
  /// Switch top level view to this keybag set's main view.
  /// </summary>
  public ICommand ViewSetCommand { get; }

  /// <summary>
  /// Switch top level view to the keybag database view.
  /// </summary>
  public ICommand ViewDatabaseCommand { get; }

  public ICommand ToggleContentCommand { get; }

  public ICommand SaveContentCommand { get; }

  public ICommand DiscardCommand { get; }

  public ICommand ShowSyncOverlayCommand { get; }

  public ICommand EjectCommand { get; }

  private void BackToDatabase()
  {
    if(KeybagModel!= null
      && KeybagModel.HasUnsavedChunks
      && KeybagModel.Decoded)
    {
      var result = MessageBox.Show(
        "There are unsaved changes in this keybag. \n" +
        "Do you want to save these now (answer 'No' to decide later)",
        "Confirm",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);
      if(result == MessageBoxResult.Yes)
      {
        KeybagModel.Save();
      }
    }
    Owner.AppModel.CurrentView = Owner;
  }

  /// <summary>
  /// Tells the app that this keybag has pending changes.
  /// </summary>
  public bool SavePending {
    get => KeybagModel != null
      && KeybagModel.Decoded
      && KeybagModel.HasUnsavedChunks;
  }

  public bool EnableAutoSync {
    get => _enableAutoSync;
    set {
      if(SetValueProperty(ref _enableAutoSync, value))
      {
      }
    }
  }
  private bool _enableAutoSync = true;

  /// <summary>
  /// Save pending changes if there were any.
  /// For use by the app when the user is closing the app.
  /// </summary>
  public void Save()
  {
    if(SavePending)
    {
      KeybagModel?.Save();
    }
  }

  private void ViewThisSet()
  {
    ShowingContent = true; // includes InitKeybagModel
    Owner.AppModel.CurrentView = this;
    if(!KeyKnown)
    {
      TryStartPassphraseOverlay();
    }
  }

  public KeybagDbViewModel Owner { get; }

  public string Tag { get => Model.Tag; }

  public string Id26 { get => Model.FileId.ToBase26(); }

  public string Created {
    get => Model.FileId.ToStamp().ToLocalTime()
      .ToString("yyyy-MM-dd HH:mm:ss");
  }

  public ChunkId FileId { get => Model.FileId; }

  public bool KeyKnown {
    get => _keyKnown;
    set {
      if(SetValueProperty(ref _keyKnown, value))
      {
        RaisePropertyChanged(nameof(LockStatus));
        RaisePropertyChanged(nameof(LockIcon));
        RaisePropertyChanged(nameof(KeyKnownAndShowing));
      }
    }
  }
  private bool _keyKnown = false;

  public bool KeyKnownAndShowing {
    get => KeyKnown && ShowingContent;
  }

  public string LockStatus {
    get => KeyKnown ? "Unlocked" : "Locked";
  }

  public string LockIcon {
    get => KeyKnown ? "DatabaseCheck" : "DatabaseLock";
  }

  public ChunkId EditId {
    get => _editId;
    set {
      if(SetValueProperty(ref _editId, value))
      {
        if(Owner.SortOrder == KeybagSortOrder.ByLastModified)
        {
          Owner.SortKeybags();
        }
      }
    }
  }
  private ChunkId _editId;

  public string LastChanged {
    get => EditId.ToStamp().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
  }

  public string Title { get => Tag; }

  public ChunkCryptor? FindKey()
  {
    return Owner.AppModel.Services.KeyRing.Find(Model.KeyGuid);
  }

  public void Refresh()
  {
    var key = FindKey();
    KeyKnown = key != null;
    var editId = EditId;
    var header = KeybagHeader.TryFromFile(Model.PrimaryFile);
    if(header != null)
    {
      editId = header.FileEdit;
    }
    if(KeyKnown && KeybagModel!=null)
    {
      editId = KeybagModel.GetNewestChunkEdit();
    }
    EditId = editId;
  }

  public bool TryStartPassphraseOverlay()
  {
    if(!KeyKnown)
    {
      var keyDescriptor = Model.GetKeyDescriptor();
      if(keyDescriptor != null)
      {
        Owner.OverlayHost.PushOverlay(
          new UnlockKeyOverlayViewModel(
            Owner.OverlayHost,
            Owner.AppModel.Services.KeyRing,
            keyDescriptor,
            Tag,
            success => {
              Refresh();
              InitKeybagModel();
            })
        );
        return true;
      }
      else
      {
        MessageBox.Show(
          "Error opening keybag file",
          "Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
      }
    }
    return false;
  }

  public bool ShowingContent {
    get => _showingContent;
    set {
      if(value)
      {
        InitKeybagModel();
      }
      if(SetValueProperty(ref _showingContent, value))
      {
        RaisePropertyChanged(nameof(ShowText));
        RaisePropertyChanged(nameof(ShowIcon));
        RaisePropertyChanged(nameof(KeyKnownAndShowing));
      }
    }
  }
  private bool _showingContent = true;

  public void Discard()
  {
    if(KeybagModel != null && KeybagModel.HasUnsavedChunks)
    {
      var result = MessageBox.Show(
        $"Are you sure you want to discard ALL unsaved changes to '{Model.Tag}'?",
        "Confirm",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);
      if(result != MessageBoxResult.Yes)
      {
        return;
      }
    }
    Reload(true);
  }

  public void Reload(bool allowDiscard)
  {
    if(KeybagModel != null)
    {
      if(!allowDiscard && KeybagModel.HasUnsavedChunks)
      {
        throw new InvalidOperationException(
          "Expecting the keybag to have been saved already");
      }
      KeybagModel = null;
    }
    InitKeybagModel();
    if(ShowingContent)
    {
      RaisePropertyChanged(nameof(ShowingContent));
      RaisePropertyChanged(nameof(ShowText));
      RaisePropertyChanged(nameof(ShowIcon));
      RaisePropertyChanged(nameof(KeyKnownAndShowing));
    }
  }

  private void InitKeybagModel()
  {
    if(_keybagModel == null)
    {
      var key = FindKey();
      if(File.Exists(Model.PrimaryFile)
        && key!=null)
      {
        KeybagModel = new KeybagViewModel(this);
      }
      else
      {
        Trace.TraceWarning($"Key not found or file not found for '{Tag}'");
      }
    }
  }

  public KeybagViewModel? KeybagModel {
    get {
      return _keybagModel;
    }
    set {
      if(SetNullableInstanceProperty(ref _keybagModel, value))
      {
      }
    }
  }
  private KeybagViewModel? _keybagModel;

  public string ShowText {
    get => ShowingContent ? "Hide" : "Show";
  }

  public string ShowIcon {
    get => ShowingContent ? "EyeOff" : "Eye";
  }

  public void ExportKeybag()
  {
    var key = FindKey();
    if(key != null && KeybagModel != null)
    {
      // Check KeybagModel just to be sure the keybag exists
      // and is valid and unlocked.
      var dialog = new SaveFileDialog() {
        Filter = "Keybag 3 files|*.kb3",
        FileName = $"{Tag}.kb3",
        Title = "Export and Connect keybag",
        AddExtension = true,
        DefaultExt = ".kb3",
        OverwritePrompt = false,
        CheckPathExists = true,
      };
      var result = dialog.ShowDialog();
      if(result == true)
      {
        var targetFile = dialog.FileName;
        Trace.TraceInformation($"Exporting keybag to '{targetFile}'");
        if(File.Exists(targetFile))
        {
          var confirm = MessageBox.Show(
            $"The target file '{targetFile}' already exists.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          return;
        }
        File.Copy(Model.PrimaryFile, targetFile);
        Model.TryConnect(targetFile, out var kbr);
        Refresh();
      }
    }
  }

  private void Eject()
  {
    KeybagModel = null;
    var keyRing = Owner.AppModel.Services.KeyRing;
    keyRing.Remove(Model.KeyGuid);
    KeyKnown = false;
    if(ShowingContent)
    {
      RaisePropertyChanged(nameof(ShowingContent));
      RaisePropertyChanged(nameof(ShowText));
      RaisePropertyChanged(nameof(ShowIcon));
    }
    Owner.AppModel.CurrentView = Owner;
  }
}
