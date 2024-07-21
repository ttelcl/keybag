/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Storage;

namespace Keybag3.Services;

/// <summary>
/// The collection of service objects that live throughout the
/// application's lifetime
/// </summary>
public class KeybagServices: IDisposable
{
  private bool _disposed;

  public KeybagServices()
  {
    _disposed = false;
    KeyRing = new KeyRing();
    KeybagDatabase = new KeybagDb();
    ThemeHelper = new ThemeColorHelper();
  }

  public KeyRing KeyRing { get; }

  public KeybagDb KeybagDatabase { get; }

  public ThemeColorHelper ThemeHelper { get; }

  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      KeyRing.Dispose();
      GC.SuppressFinalize(this);
    }
  }
}
