/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

using Microsoft.Win32;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Storage;

using Keybag3.WpfUtilities;
using System.Windows.Controls;

namespace Keybag3.Main.Database;

public class ImportConnectViewModel: ViewModelBase<KeybagDbViewModel>, IHasViewTitle
{
  public ImportConnectViewModel(
    KeybagDbViewModel model)
    : base(model)
  {
    CancelCommand = new DelegateCommand(p => { Cancel(); });
    ImportCommand = new DelegateCommand(
      p => { DoImport(); },
      p => FileRelation == KeybagRelation.NewSet && KeyKnown);
    ConnectCommand = new DelegateCommand(
      p => { DoConnect(); },
      p => FileRelation == KeybagRelation.Extern && KeyKnown);
    OpenFileCommand = new DelegateCommand(p => { OpenFile(); });
    UnlockCommand = new DelegateCommand(p => { Unlock(); });
  }

  public MainViewModel AppModel { get => Model.AppModel; }

  public string Title { get => "Import / Connect"; }

  public ICommand CancelCommand { get; }

  public ICommand ImportCommand { get; }

  public ICommand ConnectCommand { get; }

  public ICommand OpenFileCommand { get; }

  public ICommand UnlockCommand { get; }

  /// <summary>
  /// The name of the selected file. If not null,
  /// this will be a full path and <see cref="FileRelation"/>
  /// and <see cref="FileId26"/> will have been set.
  /// </summary>
  public string? FileName {
    get => _fileName;
    set {
      if(!String.IsNullOrEmpty(value))
      {
        value = Path.GetFullPath(value);
        if(!File.Exists(value))
        {
          MessageBox.Show(
            "File not found or not accessible",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          value = null;
        }
        else
        {
          var header = KeybagHeader.TryFromFile(value);
          if(header == null)
          {
            MessageBox.Show(
              "Unrecognized file format",
              "Error",
              MessageBoxButton.OK,
              MessageBoxImage.Error);
            value = null;
          }
        }
      }
      if(SetNullableInstanceProperty(ref _fileName, value))
      {
        if(!String.IsNullOrEmpty(_fileName))
        {
          var relation = Model.Model.FileRelation(_fileName, out var header);
          FileRelation = relation;
          FileHeader = header;
        }
        else
        {
          FileRelation = null;
          FileHeader = null;
        }
        RaisePropertyChanged(nameof(HasFile));
        RaisePropertyChanged(nameof(FolderName));
        RaisePropertyChanged(nameof(ShortName));
      }
    }
  }
  private string? _fileName;

  public string? FolderName {
    get => String.IsNullOrEmpty(_fileName) ? null : Path.GetDirectoryName(_fileName);
  }

  public string? ShortName {
    get => String.IsNullOrEmpty(_fileName) ? null : Path.GetFileName(_fileName);
  }

  public bool HasFile {
    get => !String.IsNullOrEmpty(_fileName);
  }

  public KeybagRelation? FileRelation {
    get => _fileRelation;
    private set {
      if(SetValueProperty(ref _fileRelation, value))
      {
        RaisePropertyChanged(nameof(FileRelationText));
        RaisePropertyChanged(nameof(FileRelationIcon));
        RaisePropertyChanged(nameof(FileOK));
        RaisePropertyChanged(nameof(FileColor));
      }
    }
  }
  private KeybagRelation? _fileRelation;

  public string FileRelationText {
    get {
      if(_fileRelation.HasValue)
      {
        return _fileRelation switch {
          KeybagRelation.Missing => "(file does not exist or is not accessible)",
          KeybagRelation.Unrecognized => "Not a recognized *.kb3 file",
          KeybagRelation.Incompatible => "Incompatible: key changes are not yet supported",
          KeybagRelation.NewSet => "Can be imported as a new Keybag",
          KeybagRelation.Extern => "Can be connected to its (known) Keybag set",
          KeybagRelation.SyncTarget => "Already present (as connected keybag file)",
          KeybagRelation.Primary => "Already present (as primary keybag file)",
          _ => "Internal error",
        };
      }
      else
      {
        return "(no file selected)";
      }
    }
  }

  public string FileRelationIcon {
    get => FileOK ? "CheckCircle" : "CloseCircle";
  }

  public bool FileOK {
    get =>
      _fileRelation.HasValue
      && (_fileRelation.Value == KeybagRelation.Extern
        || _fileRelation.Value == KeybagRelation.NewSet);
  }

  public string FileColor {
    get => FileOK ? "OK" : "Error";
  }

  public KeybagHeader? FileHeader {
    get => _fileHeader;
    set {
      if(SetNullableInstanceProperty(ref _fileHeader, value))
      {
        RaisePropertyChanged(nameof(FileId));
        RaisePropertyChanged(nameof(FileKey));
        RaisePropertyChanged(nameof(FileId26));
        RaisePropertyChanged(nameof(FileCreated));
        RaisePropertyChanged(nameof(KeyKnown));
        RaisePropertyChanged(nameof(ShowKeyEntry));
        RaisePropertyChanged(nameof(KeyStatus));
        RaisePropertyChanged(nameof(KeyStatusIcon));
        RaisePropertyChanged(nameof(KeyColor));
      }
    }
  }
  private KeybagHeader? _fileHeader;

  public ChunkId? FileId {
    get => FileHeader?.FileId;
  }

  public Guid? FileKey {
    get => FileHeader?.KeyId;
  }

  public string FileId26 {
    get => FileId?.ToBase26() ?? String.Empty;
  }

  public string FileCreated {
    get => FileId?
      .ToStamp()
      .ToLocalTime()
      .ToString("yyyy-MM-dd HH:mm:ss") ?? String.Empty;
  }

  public bool KeyKnown {
    get {
      if(FileKey.HasValue)
      {
        var guid = FileKey.Value;
        return Model.AppModel.Services.KeyRing.Find(guid) != null;
      }
      else
      {
        return false;
      }
    }
  }

  public string KeyStatus {
    get => KeyKnown ? "Key successfully entered" : "Key entry required";
  }

  public string KeyStatusIcon {
    get => KeyKnown ? "CheckCircle" : "CloseCircle";
  }

  public string KeyColor {
    get => KeyKnown ? "OK" : "Error";
  }

  private bool CanUseFile {
    get =>
      HasFile
      && _fileRelation.HasValue
      && (_fileRelation.Value == KeybagRelation.Extern
        || _fileRelation.Value == KeybagRelation.NewSet);
  }

  public bool ShowKeyEntry {
    get => CanUseFile && !KeyKnown;
  }

  private PasswordBox? _passwordBox = null;

  public void BindPassBox(PasswordBox? passwordBox)
  {
    _passwordBox = passwordBox;
  }

  private void ClearPasswords()
  {
    _passwordBox?.Clear();
  }

  private void Unbind()
  {
    ClearPasswords();
    _passwordBox = null;
  }

  private void Cancel()
  {
    Unbind();
    CloseView();
  }

  private void Unlock()
  {
    if(_passwordBox == null || FileHeader == null)
    {
      throw new InvalidOperationException(
        "Internal error");
    }
    var keyRing = Model.AppModel.Services.KeyRing;
    if(keyRing.Find(FileHeader.KeyId) == null)
    {
      // Otherwise: key already known - we should not have been here, but nevermind
      using(var passphrase = _passwordBox.SecurePassword)
      {
        var cryptor = FileHeader.TryMakeCryptor(passphrase);
        if(cryptor == null)
        {
          MessageBox.Show(
            "Incorrect passphrase",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          return;
        }
        keyRing.Put(cryptor);
        Model.KeyStatusChanged(cryptor.KeyId);
      }
    }
    RaisePropertyChanged(nameof(KeyKnown));
    RaisePropertyChanged(nameof(ShowKeyEntry));
    RaisePropertyChanged(nameof(KeyStatus));
    RaisePropertyChanged(nameof(KeyStatusIcon));
    RaisePropertyChanged(nameof(KeyColor));
  }

  private void OpenFile()
  {
    var dialog = new OpenFileDialog() {
      Filter = "Keybag3 files (*.kb3)|*.kb3",
      CheckFileExists = true,
      DefaultExt = ".kb3",
    };
    if(dialog.ShowDialog() == true)
    {
      FileName = dialog.FileName;
    }
    else
    {
      FileName = null;
    }
  }

  private void DoImport()
  {
    // Note that Import() and Connect() are equal - CreateOrImportFrom()
    // decides which one it is.
    DoImportConnect();
  }

  private void DoConnect()
  {
    // Note that Import() and Connect() are equal - CreateOrImportFrom()
    // decides which one it is.
    DoImportConnect();
  }

  private void DoImportConnect()
  {
    // Note that Import() and Connect() are equal - CreateOrImportFrom()
    // decides which one it is.
    if(!KeyKnown)
    {
      // Note that the key isn't used for anything, it just feels wrong
      // to be able to import without verifying it ...
      MessageBox.Show(
        "Please unlock the file first, by entering its key");
      return;
    }
    if(FileRelation != KeybagRelation.NewSet || String.IsNullOrEmpty(FileName))
    {
      throw new InvalidOperationException(
        "Internal error - incorrect state");
    }
    Model.Model.CreateOrImportFrom(FileName);
    Model.Refresh();
    Unbind();
    CloseView();
  }

  private void CloseView()
  {
    //Model.OverlayHost.PopOverlay(this);
    Model.ViewHost.CurrentView = Model;
  }

}
