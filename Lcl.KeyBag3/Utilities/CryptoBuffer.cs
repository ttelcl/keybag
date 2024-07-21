/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers;

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// Wraps a primitive array that is erased when disposed
/// </summary>
public class CryptoBuffer<T>: IHasMemory<T>, IDisposable where T : struct
{
  private T[]? _buffer;

  /// <summary>
  /// Create a new CryptoBuffer of the specified length
  /// </summary>
  public CryptoBuffer(int length)
  {
    _buffer = ArrayPool<T>.Shared.Rent(length);
    Length = length;
  }

  /// <summary>
  /// Create a new CryptoBuffer and copy the source as its content
  /// </summary>
  public CryptoBuffer(ReadOnlySpan<T> source)
    : this(source.Length)
  {
    source.CopyTo(_buffer!);
  }

  /// <summary>
  /// Create a new CryptoBuffer copying the content of the source
  /// subsequently clears the source buffer
  /// </summary>
  public static CryptoBuffer<T> FromSpanClear(Span<T> source)
  {
    var b = new CryptoBuffer<T>(source);
    source.Clear();
    return b;
  }

  /// <summary>
  /// Erase the buffer to all zeros
  /// </summary>
  public void Clear()
  {
    // allow even if disposed
    if(_buffer != null)
    {
      Array.Clear(_buffer);
    }
  }

  /// <summary>
  /// Shortcut for <see cref="Span"/>[range];
  /// </summary>
  public Span<T> this[Range range] { get => Span[range]; }

  /// <summary>
  /// Implements <see cref="IHasReadOnlySpan{T}.HasSpan"/> by always
  /// returning true unless disposed.
  /// </summary>
  public bool HasSpan { get => _buffer!=null; }

  /// <summary>
  /// Expose the buffer as a <see cref="Span{T}"/>
  /// (implements <see cref="IHasSpan{T}.Span"/>)
  /// </summary>
  public Span<T> Span {
    get {
      ObjectDisposedException.ThrowIf(_buffer==null, this);
      return _buffer.AsSpan(0, Length);
    }
  }

  /// <summary>
  /// Expose the buffer as a <see cref="ReadOnlySpan{T}"/>
  /// (implements <see cref="IHasReadOnlySpan{T}.ReadOnlySpan"/>)
  /// </summary>
  public ReadOnlySpan<T> ReadOnlySpan { get => Span; }

  /// <summary>
  /// Expose the buffer as a <see cref="Memory{T}"/>
  /// (implements <see cref="IHasMemory{T}.Memory"/>)
  /// </summary>
  public Memory<T> Memory {
    get {
      ObjectDisposedException.ThrowIf(_buffer==null, this);
      return _buffer.AsMemory(0, Length);
    }
  }

  /// <summary>
  /// Expose the buffer as a <see cref="ReadOnlyMemory{T}"/>
  /// (implements <see cref="IHasReadOnlyMemory{T}.ReadOnlyMemory"/>)
  /// </summary>
  public ReadOnlyMemory<T> ReadOnlyMemory { get => Memory; }

  /// <summary>
  /// The number of elements in the buffer
  /// </summary>
  public int Length { get; }

  /// <summary>
  /// True if this buffer has been disposed
  /// </summary>
  public bool Disposed { get => _buffer==null; }

  /// <summary>
  /// Test if the argument has the same content as this buffer
  /// </summary>
  public bool IsSame(ReadOnlySpan<T> other)
  {
    if(other.Length != Length)
    {
      return false;
    }
    return Span.SequenceEqual(other);
  }

  /// <summary>
  /// Clear the buffer and return it to the pool
  /// </summary>
  public void Dispose()
  {
    if(_buffer!=null)
    {
      Array.Clear(_buffer);
      var buffer = _buffer;
      _buffer = null;
      GC.SuppressFinalize(this);
      // The 'clear' flag can be false because we cleared the buffer already.
      // We cleared it because we want it cleared NOW, not at an ill defined
      // later moment (possibly never).
      ArrayPool<T>.Shared.Return(buffer, false);
    }
  }
}
