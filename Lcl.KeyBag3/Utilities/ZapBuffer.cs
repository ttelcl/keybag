/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// A resizable buffer that erases itself when disposed
/// </summary>
public class ZapBuffer<T>: IHasSpan<T>, IDisposable where T : struct
{
  private T[]? _buffer;
  private int _lockCount;

  /// <summary>
  /// Create a new empty ZapBuffer
  /// </summary>
  /// <param name="capacity">
  /// Initial capacity (default 1024)
  /// </param>
  public ZapBuffer(int capacity = 1024)
  {
    _buffer = ArrayPool<T>.Shared.Rent(capacity);
    Size = 0;
  }

  /// <summary>
  /// The current size
  /// </summary>
  public int Size { get; private set; }

  /// <summary>
  /// Get this buffer's current capacity.
  /// You can explicitly increase this using <see cref="Reserve(int)"/>.
  /// It may also be increased implicitly by other calls.
  /// </summary>
  public int Capacity { get => _buffer?.Length ?? 0; }

  /// <summary>
  /// Get the full currently allocated size.
  /// </summary>
  public Span<T> All {
    get {
      ObjectDisposedException.ThrowIf(_buffer==null, this);
      return _buffer.AsSpan(0, Size);
    }
  }

  /// <summary>
  /// True to indicate that <see cref="Span"/> and <see cref="ReadOnlySpan"/>
  /// are safe to access. This requires that the buffer has been locked
  /// with <see cref="LockBuffer"/>.
  /// </summary>
  public bool HasSpan { get { return _lockCount > 0; } }

  /// <summary>
  /// Retrieve the <see cref="Span{T}"/> covering the current content
  /// of the locked buffer. Fails if the buffer is not locked.
  /// </summary>
  public Span<T> Span {
    get {
      ObjectDisposedException.ThrowIf(_buffer == null, this);
      if(_lockCount == 0)
      {
        throw new InvalidOperationException(
          "The buffer's span is only accessible while locked");
      }
      return _buffer.AsSpan(0, Size);
    }
  }

  /// <summary>
  /// Retrieve the <see cref="ReadOnlySpan{T}"/> covering the current content
  /// of the locked buffer. Fails if the buffer is not locked.
  /// </summary>
  public ReadOnlySpan<T> ReadOnlySpan { get => Span; }

  /// <summary>
  /// Ensure that this buffer's capacity is at least the given value.
  /// This method may reallocate the buffer.
  /// </summary>
  /// <param name="capacity">
  /// The minimum required capacity
  /// </param>
  public void Reserve(int capacity)
  {
    ObjectDisposedException.ThrowIf(_buffer == null, this);
    if(_lockCount > 0)
    {
      throw new InvalidOperationException(
        "Cannot change ZapBuffer capacity while it is locked");
    }
    if(_buffer.Length < capacity)
    {
      // Avoid reallocating a lot if Reserve() is called with small
      // increments: allocate more than requested in that case
      var nextmincapacity = _buffer.Length + _buffer.Length/2;
      if(capacity < nextmincapacity)
      {
        capacity = nextmincapacity;
      }
      var newbuffer = ArrayPool<T>.Shared.Rent(capacity);
      var oldbuffer = _buffer;
      oldbuffer.CopyTo(newbuffer, 0);
      _buffer = newbuffer;
      Array.Clear(oldbuffer);
      ArrayPool<T>.Shared.Return(oldbuffer);
    }
  }

  /// <summary>
  /// Get the span covering the indicated range (relative to the current
  /// <see cref="Size"/> in case the range has relative-to-end indexes).
  /// The buffer capacity is increased to ensure the range is in the buffer.
  /// If the end of the range is beyond the current size, the current size
  /// is increased.
  /// </summary>
  public Span<T> this[Range range] {
    get {
      ObjectDisposedException.ThrowIf(_buffer == null, this);
      var (offset, length) = range.GetOffsetAndLength(Size);
      var end = offset + length;
      Reserve(end);
      if(end > Size)
      {
        if(_lockCount > 0)
        {
          throw new InvalidOperationException(
            "Cannot change ZapBuffer size while it is locked");
        }
        Size = end;
      }
      return _buffer.AsSpan(offset, length);
    }
  }

  /// <summary>
  /// Lock this buffer, preventing size and capacity changes (and
  /// re-allocations) until the returned <see cref="Lock"/> object
  /// is disposed.
  /// </summary>
  public Lock LockBuffer()
  {
    ObjectDisposedException.ThrowIf(_buffer == null, this);
    return new Lock(this);
  }

  /// <summary>
  /// True if there is an active lock on this buffer (or multiple)
  /// </summary>
  public bool IsLocked { get => _lockCount > 0; }

  /// <summary>
  /// Append <paramref name="length"/> elements to this buffer
  /// and return the span of elements appended
  /// </summary>
  public Span<T> AppendToSlice(int length)
  {
    ObjectDisposedException.ThrowIf(_buffer == null, this);
    if(length < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(length));
    }
    if(IsLocked)
    {
      // Make this check explicit for a better error message
      // and to force treating even AppendToSlice(0) as an error
      // while locked.
      throw new InvalidOperationException(
        "Cannot append to a locked ZapBuffer");
    }
    var start = Size;
    Size += length;
    return this[start..Size];
  }

  /// <summary>
  /// Append the given slice at the end of the buffer
  /// (and increase the size)
  /// </summary>
  public void AppendSlice(ReadOnlySpan<T> slice)
  {
    if(!slice.IsEmpty)
    {
      slice.CopyTo(AppendToSlice(slice.Length));
    }
  }

  /// <summary>
  /// Resize the buffer. This can grow or shrink the buffer
  /// </summary>
  public void Resize(int newSize)
  {
    ObjectDisposedException.ThrowIf(_buffer==null, this);
    if(newSize < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(newSize));
    }
    if(IsLocked)
    {
      throw new InvalidOperationException(
        "Cannot resize a locked ZapBuffer");
    }
    if(newSize > Size)
    {
      Reserve(newSize);
      Size = newSize;
    }
    else if(newSize < Size)
    {
      // Other APIs rely implicitly on the unused part of the buffer
      // being cleared to zeroes
      var removed = this[newSize..Size];
      removed.Clear();
      Size = newSize;
    }
  }

  /// <summary>
  /// Copy the used part of the buffer to a new <see cref="CryptoBuffer{T}"/>
  /// </summary>
  public CryptoBuffer<T> Snapshot()
  {
    return new CryptoBuffer<T>(All);
  }

  /// <summary>
  /// Clear the entire buffer and reset the size to 0.
  /// </summary>
  public void Clear()
  {
    ObjectDisposedException.ThrowIf(_buffer==null, this);
    Size = 0;
    Array.Clear(_buffer);
  }

  /// <summary>
  /// Returns true after this buffer has been disposed
  /// </summary>
  public bool Disposed { get => _buffer==null; }

  /// <summary>
  /// Erase the buffer an mark this object as disposed
  /// </summary>
  public void Dispose()
  {
    if(_buffer!=null)
    {
      var buffer = _buffer;
      Array.Clear(_buffer);
      GC.SuppressFinalize(this);
      Size = 0;
      _buffer = null;
      ArrayPool<T>.Shared.Return(buffer, false);
      if(_lockCount > 0)
      {
        throw new InvalidOperationException(
          "Internal error: attempt to dispose a ZapBuffer while it is locked");
      }
    }
  }

  /// <summary>
  /// Helper class representing a locked <see cref="ZapBuffer{T}"/>.
  /// Until this is disposed, the buffer's size and capacity are locked.
  /// </summary>
  public class Lock: IHasMemory<T>, IHasSpan<T>, IDisposable
  {
    private readonly ZapBuffer<T> _owner;
    private bool _disposed;

    internal Lock(ZapBuffer<T> owner)
    {
      _owner=owner;
      _owner._lockCount++;
    }

    /// <summary>
    /// Returns true after disposal
    /// </summary>
    public bool IsDisposed { get => _disposed; }

    /// <summary>
    /// Check if the owner's <see cref="ZapBuffer{T}.Span"/> and
    /// <see cref="ZapBuffer{T}.ReadOnlySpan"/> properties are available
    /// (which should be the case if this lock has not been disposed)
    /// </summary>
    public bool HasSpan { get => _owner.HasSpan; }

    /// <summary>
    /// Get the owner's <see cref="Span{T}"/>
    /// </summary>
    public Span<T> Span { get => _owner.Span; }

    /// <summary>
    /// Get the owner's <see cref="ReadOnlySpan{T}"/>
    /// </summary>
    public ReadOnlySpan<T> ReadOnlySpan { get => _owner.Span /* skip the middle man! */; }

    /// <summary>
    /// Get the owner's <see cref="Memory{T}"/>
    /// </summary>
    public Memory<T> Memory { get => _owner._buffer.AsMemory(.._owner.Size); }

    /// <summary>
    /// Get the owner's <see cref="ReadOnlyMemory{T}"/>
    /// </summary>
    public ReadOnlyMemory<T> ReadOnlyMemory { get => Memory; }

    /// <summary>
    /// Clean up: release the "locked" state of the owner
    /// </summary>
    public void Dispose()
    {
      if(!_disposed)
      {
        _disposed = true;
        _owner._lockCount--;
      }
    }
  }
}
