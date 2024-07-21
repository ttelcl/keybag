﻿/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Crypto;

/// <summary>
/// Holds an AES-GCM 128 key.
/// </summary>
public class KeyBuffer: IDisposable
{
  private readonly CryptoBuffer<byte> _buffer;
  private HashResult? _hashResult;
  private bool _disposed;

  /// <summary>
  /// Create a new KeyBuffer, copying the source span into an equal-sized
  /// key buffer. Once created the key buffer is immutable until disposed.
  /// </summary>
  /// <param name="source">
  /// The source for the 32 bytes to copy into this KeyBuffer.
  /// </param>
  public KeyBuffer(ReadOnlySpan<byte> source)
  {
    _buffer = new CryptoBuffer<byte>(source.Length);
    source.CopyTo(_buffer.Span);
    if(source.Length != 32)
    {
      throw new ArgumentOutOfRangeException(
        nameof(source), "Expecting a 32 byte (256 bit) buffer as argument");
    }
  }

  /// <summary>
  /// Return a view on the stored key bytes
  /// </summary>
  public ReadOnlySpan<byte> Bytes { get => _buffer.Span; } // _buffer.Span checks disposal

  /// <summary>
  /// Return the result of hashing the key bytes with SHA256.
  /// The result is calculated and cached upon first invocation.
  /// </summary>
  public HashResult GetSha256()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if(_hashResult == null)
    {
      _hashResult = HashResult.FromSha256(_buffer);
    }
    return _hashResult;
  }

  /// <summary>
  /// Get a GUID for the stored key bytes calculated via the
  /// SHA256 hash of the key bytes.
  /// </summary>
  public Guid GetId()
  {
    return GetSha256().AsGuid;
  }

  /// <summary>
  /// Erase the buffer
  /// </summary>
  public virtual void Dispose()
  {
    _disposed = true;
    _buffer.Dispose();
    GC.SuppressFinalize(this);
  }

}
