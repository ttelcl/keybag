/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Crypto;

/// <summary>
/// Collection of <see cref="ChunkCryptor"/> instances kept
/// for reuse
/// </summary>
public class KeyRing: IDisposable
{
  private bool _disposed = false;
  private readonly Dictionary<Guid, ChunkCryptor> _keys;

  /// <summary>
  /// Create a new KeyRing
  /// </summary>
  public KeyRing()
  {
    _keys = [];
  }

  /// <summary>
  /// Get the cryptor for the given key id if available,
  /// returning null if not
  /// </summary>
  public ChunkCryptor? Find(Guid keyId)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    return _keys.TryGetValue(keyId, out var key) ? key : null;
  }

  /// <summary>
  /// Insert a key / cryptor in this key ring. Attempting to insert
  /// a key that is already present will raise an exception.
  /// </summary>
  /// <param name="cryptor">
  /// The key to insert.
  /// </param>
  /// <exception cref="InvalidOperationException">
  /// Thrown if a cryptor for the same key is already present
  /// </exception>
  public void Put(ChunkCryptor cryptor)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if(_keys.ContainsKey(cryptor.KeyId))
    {
      // Allowing this would open too many opportunities for cryptors
      // either not being disposed or being disposed too early.
      // Disallowing this case is most foolproof.
      throw new InvalidOperationException(
        $"Attempt to track same key twice: {cryptor.KeyId}");
    }
    _keys[cryptor.KeyId] = cryptor;
  }

  /// <summary>
  /// Dispose all stored keys and mark this class as disposed
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      var keys = _keys.Values;
      _keys.Clear();
      foreach(var key in keys)
      {
        key.Dispose();
      }
      GC.SuppressFinalize(this);
    }
  }
}
