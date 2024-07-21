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
using System.Windows.Controls;
using System.Windows.Input;

using Keybag3.WpfUtilities;

using Lcl.KeyBag3.Storage;
using Lcl.KeyBag3.Crypto;
using System.Security;

namespace Keybag3.Main.Database;

public class NewKeybagViewModel: ViewModelBase<KeybagDbViewModel>, IHasViewTitle
{
  private PasswordBox? _passwordBoxPrimary;
  private PasswordBox? _passwordBoxVerify;

  public NewKeybagViewModel(
    KeybagDbViewModel host)
    : base(host)
  {
    TrySubmitCommand = new DelegateCommand(
      p => { TrySubmit(); },
      p => IsTagValid && _passwordBoxPrimary!=null && _passwordBoxVerify!=null &&
           IsPrimaryOk && IsVerifyOk);
    CancelCommand = new DelegateCommand(p => { Cancel(); });
  }

  public const int MinimumPassphraseLength = 11;

  public ICommand TrySubmitCommand { get; }

  public ICommand CancelCommand { get; }

  public string Tag {
    get => _tag;
    set {
      if(SetInstanceProperty(ref _tag, value))
      {
        IsTagValid =
          KeybagSet.IsValidTag(value)
          && !Model.IsKnownTag(value);
      }
    }
  }
  private string _tag = "";

  public bool IsTagValid {
    get => _isTagValid;
    private set {
      if(SetValueProperty(ref _isTagValid, value))
      {
        RaisePropertyChanged(nameof(IsTagInvalid));
      }
    }
  }
  private bool _isTagValid;

  public bool IsTagInvalid {
    get => !IsTagValid;
  }

  public bool IsPrimaryOk {
    get => _isPrimaryOk;
    set {
      if(SetValueProperty(ref _isPrimaryOk, value))
      {
        RaisePropertyChanged(nameof(IsPrimaryNotOk));
      }
    }
  }
  private bool _isPrimaryOk;

  public bool IsPrimaryNotOk {
    get => !IsPrimaryOk;
  }

  public int PrimaryLength {
    get => _primaryLength;
    private set {
      if(SetValueProperty(ref _primaryLength, value))
      {
      }
    }
  }
  private int _primaryLength;

  public bool IsVerifyOk {
    get => _isVerifyOk;
    set {
      if(SetValueProperty(ref _isVerifyOk, value))
      {
        RaisePropertyChanged(nameof(IsVerifyNotOk));
      }
    }
  }
  private bool _isVerifyOk;

  public bool IsVerifyNotOk {
    get => !IsVerifyOk;
  }

  private void TrySubmit()
  {
    if(!KeybagSet.IsValidTag(Tag))
    {
      MessageBox.Show(
        "The 'tag' is not valid. Make sure it is not empty, "+
        "only contains filename-safe characters, and no spaces",
        "Invalid Tag",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    if(Model.IsKnownTag(Tag))
    {
      MessageBox.Show(
        "The 'tag' is already in use. Please choose a different one",
        "Duplicate Tag",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    if(_passwordBoxPrimary == null || _passwordBoxVerify == null)
    {
      MessageBox.Show(
        "Internal error - password textbox setup failed",
        "Internal Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    using(var ss1 = _passwordBoxPrimary.SecurePassword)
    using(var ss2 = _passwordBoxVerify.SecurePassword)
    {
      if(ss1.Length != ss2.Length)
      {
        MessageBox.Show(
          "The passphrases do not match (they have different lengths)",
          "Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        return;
      }
      if(ss1.Length < MinimumPassphraseLength)
      {
        MessageBox.Show(
          "The passphrase is too short to be acceptable",
          "Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        return;
      }
      using(var ppk = PassphraseKey.TryNewFromSecureStringPair(ss1, ss2))
      {
        if(ppk == null)
        {
          MessageBox.Show(
            "The passphrases do no match",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          return;
        }
        var cryptor = new ChunkCryptor(ppk);
        try
        {
          var kbs = Model.Model.NewKeybag(Tag, cryptor);
          Model.Refresh();
        }
        catch
        {
          cryptor.Dispose();
          throw;
        }
        // clean up and switch back to the KeybackDb view
        Unbind();
        CloseView();
      }
    }
  }

  private void Cancel()
  {
    Unbind();
    CloseView();
  }

  private void CloseView()
  {
    // Model.OverlayHost.PopOverlay(this);
    Model.ViewHost.CurrentView = Model;
  }

  public string Title { get => "Create new Keybag"; }

  public void BindPrimary(PasswordBox pwb)
  {
    _passwordBoxPrimary = pwb;
  }

  public void BindVerify(PasswordBox pwb)
  {
    _passwordBoxVerify = pwb;
  }

  public void PrimaryChanged(SecureString passphrase)
  {
    IsPrimaryOk = 
      _passwordBoxPrimary!=null
      && passphrase.Length >= MinimumPassphraseLength;
    PrimaryLength = passphrase.Length;
  }

  public void VerifyChanged(SecureString passphrase)
  {
    IsVerifyOk =
      _passwordBoxVerify!=null
      && passphrase.Length >= MinimumPassphraseLength
      && IsPrimaryOk
      && passphrase.Length == PrimaryLength;
  }

  public void ClearPasswords()
  {
    _passwordBoxPrimary?.Clear();
    _passwordBoxVerify?.Clear();
  }

  public void Unbind()
  {
    ClearPasswords();
    _passwordBoxPrimary = null;
    _passwordBoxVerify = null;
  }

}
