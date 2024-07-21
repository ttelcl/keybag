/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Controls;

using Lcl.KeyBag3.Crypto;

using Keybag3.WpfUtilities;
using System.Security;

namespace Keybag3.Main.Database;

public class UnlockKeyOverlayViewModel: ViewModelBase
{
  private Action<bool>? _completed;

  public UnlockKeyOverlayViewModel(
    ISupportsOverlay host,
    KeyRing keyRing,
    KeyData keyDescriptor,
    string? keyLabel = null,
    Action<bool>? completed = null)
  {
    Host = host;
    KeyRing = keyRing;
    KeyDescriptor = keyDescriptor;
    KeyLabel = keyLabel;
    _completed = completed;
    CancelCommand = new DelegateCommand(p => { Host.PopOverlay(this); });
    TryUnlockCommand = new DelegateCommand(
        p => { TryUnlock(); },
        p => IsPassphraseLegal && String.IsNullOrEmpty(PassphraseError)
      );
  }

  public ICommand CancelCommand { get; }

  public ICommand TryUnlockCommand { get; }

  public ISupportsOverlay Host { get; }

  public KeyRing KeyRing { get; }

  public KeyData KeyDescriptor { get; }

  public string? KeyLabel { get; }

  public void Cancel()
  {
    Host.PopOverlay(this);
    _completed?.Invoke(false);
    Unbind();
  }

  public void TryUnlock()
  {
    if(KeyRing.Find(KeyDescriptor.KeyId) != null)
    {
      // Nothing to unlock - its there already!
      Host.PopOverlay(this);
      _completed?.Invoke(true);
      Unbind();
    }
    else
    {
      using(var passphrase = TestAndGetPassphrase())
      {
        if(IsPassphraseLegal && passphrase != null)
        {
          var cryptor = ChunkCryptor.TryFromPassphrase(
            KeyDescriptor, passphrase);
          if(cryptor == null)
          {
            PassphraseError = "Incorrect passphrase";
          }
          else
          {
            KeyRing.Put(cryptor);
            Host.PopOverlay(this);
            _completed?.Invoke(true);
            Unbind();
          }
        }
      }
    }
  }

  public string PassphraseError {
    get => _passphraseError;
    private set {
      if(SetValueProperty(ref _passphraseError, value))
      {
        RaisePropertyChanged(nameof(PassphraseHasError));
        RaisePropertyChanged(nameof(StatusColor));
      }
    }
  }
  private string _passphraseError = "Too short";

  public string StatusColor {
    get => String.IsNullOrEmpty(PassphraseError) ? "Good" : "Bad";
  }

  public bool PassphraseHasError {
    get => !String.IsNullOrEmpty(PassphraseError);
  }

  public bool IsPassphraseLegal {
    get => _isPassphraseLegal;
    private set {
      if(SetValueProperty(ref _isPassphraseLegal, value))
      {
      }
    }
  }
  private bool _isPassphraseLegal = false;

  private PasswordBox? _passwordBox = null;

  internal void BindPassBox(PasswordBox? passwordBox)
  {
    _passwordBox = passwordBox;
  }

  internal void OnPassphraseChanged()
  {
    using(var passphrase = TestAndGetPassphrase())
    {
    }
  }

  private SecureString? TestAndGetPassphrase()
  {
    if(KeyRing.Find(KeyDescriptor.KeyId) != null)
    {
      PassphraseError = "Key already known";
      IsPassphraseLegal = true;
      return null;
    }
    if(_passwordBox == null)
    {
      PassphraseError = "Internal error";
      IsPassphraseLegal = false;
      return null;
    }
    else
    {
      var passphrase = _passwordBox.SecurePassword;
      if(passphrase.Length < NewKeybagViewModel.MinimumPassphraseLength)
      {
        PassphraseError = "Too short";
        IsPassphraseLegal = false;
      }
      else
      {
        PassphraseError = "";
        IsPassphraseLegal = true;
      }
      return passphrase;
    }
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

}
