/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// Cursor-like utilty class for reading binary values from a span of bytes.
/// Unless specified otherwise values are read in Little Endian order.
/// The calls support a fluent style. To enable that, the values
/// read are returned as output parameters.
/// </summary>
/// <remarks>
/// <para>
/// This class keeps track of the read position.
/// It of course can not keep track of the span to read from, so that
/// has to be passed to each call.
/// </para>
/// </remarks>
public class SpanReader
{
  /// <summary>
  /// Create a new SpanReader
  /// </summary>
  public SpanReader()
  {
  }

  /// <summary>
  /// The current position
  /// </summary>
  public int Position { get; set; }

  /// <summary>
  /// The number of bytes left in the span considering this reader's
  /// <see cref="Position"/>
  /// </summary>
  public int BytesLeft(ReadOnlySpan<byte> span)
  {
    var left = span.Length - Position;
    if(left < 0)
    {
      throw new InvalidOperationException(
        "That span does not match this reader");
    }
    return left;
  }

  /// <summary>
  /// Read a byte
  /// </summary>
  public SpanReader ReadByte(ReadOnlySpan<byte> span, out byte b)
  {
    b = span[Position];
    Position += 1;
    return this;
  }

  /// <summary>
  /// Read an unsigned 16 bit integer
  /// </summary>
  public SpanReader ReadU16(ReadOnlySpan<byte> span, out ushort u16)
  {
    u16 = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(Position, 2));
    Position += 2;
    return this;
  }

  /// <summary>
  /// Read an unsigned 32 bit integer
  /// </summary>
  public SpanReader ReadU32(ReadOnlySpan<byte> span, out uint u32)
  {
    u32 = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(Position, 4));
    Position += 4;
    return this;
  }

  /// <summary>
  /// Read an unsigned 64 bit integer
  /// </summary>
  public SpanReader ReadU64(ReadOnlySpan<byte> span, out ulong u64)
  {
    u64 = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(Position, 8));
    Position += 8;
    return this;
  }

  /// <summary>
  /// Read an unsigned 128 bit integer
  /// </summary>
  public SpanReader ReadUI128(ReadOnlySpan<byte> span, out UInt128 u128)
  {
    u128 = BinaryPrimitives.ReadUInt128LittleEndian(span.Slice(Position, 16));
    Position += 16;
    return this;
  }

  /// <summary>
  /// Read a signed 16 bit integer
  /// </summary>
  public SpanReader ReadI16(ReadOnlySpan<byte> span, out short i16)
  {
    i16 = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(Position, 2));
    Position += 2;
    return this;
  }

  /// <summary>
  /// Read a signed 32 bit integer
  /// </summary>
  public SpanReader ReadI32(ReadOnlySpan<byte> span, out int i32)
  {
    i32 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(Position, 4));
    Position += 4;
    return this;
  }

  /// <summary>
  /// Read a signed 64 bit integer
  /// </summary>
  public SpanReader ReadI64(ReadOnlySpan<byte> span, out long i64)
  {
    i64 = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(Position, 8));
    Position += 8;
    return this;
  }

  /// <summary>
  /// Read a signed 128 bit integer
  /// </summary>
  public SpanReader ReadI128(ReadOnlySpan<byte> span, out Int128 i128)
  {
    i128 = BinaryPrimitives.ReadInt128LittleEndian(span.Slice(Position, 16));
    Position += 16;
    return this;
  }

  /// <summary>
  /// Read a node id (6 byte value)
  /// </summary>
  public SpanReader ReadChunkId(ReadOnlySpan<byte> span, out long chunkId)
  {
    Span<byte> scratch = stackalloc byte[8];
    ReadSpan(span, scratch[..6]);
    chunkId = BinaryPrimitives.ReadInt64LittleEndian(scratch);
    return this;
  }

  /// <summary>
  /// Read a (128 bit) GUID
  /// </summary>
  public SpanReader ReadGuid(ReadOnlySpan<byte> span, out Guid guid)
  {
    guid = new Guid(span.Slice(Position, 16));
    Position += 16;
    return this;
  }

  /// <summary>
  /// Fill the destination span with bytes from <paramref name="span"/>
  /// at the current position (and advance the postion by the length of 
  /// <paramref name="destination"/>)
  /// </summary>
  public SpanReader ReadSpan(ReadOnlySpan<byte> span, Span<byte> destination)
  {
    span.Slice(Position, destination.Length).CopyTo(destination);
    Position += destination.Length;
    return this;
  }

  /// <summary>
  /// Return a span slice that contains the next <paramref name="length"/>
  /// bytes.
  /// </summary>
  public SpanReader ReadSlice(
    ReadOnlySpan<byte> span, int length, out ReadOnlySpan<byte> slice)
  {
    slice = span.Slice(Position, length);
    Position += length;
    return this;
  }

  /// <summary>
  /// Read a variable length encoded integer
  /// </summary>
  public SpanReader ReadVarInt(ReadOnlySpan<byte> span, out long varint)
  {
    long v = 0;
    int shift = 0;
    while(true)
    {
      ReadByte(span, out var b);
      v += (b & 0x7F) << shift;
      shift += 7;
      if((b & 0x80)==0)
      {
        varint = v;
        return this;
      }
      if(shift >= 35)
      {
        throw new InvalidDataException(
          "Invalid VarInt sequence");
      }
    }
  }

  /// <summary>
  /// Return the next <paramref name="count"/> bytes as a slice
  /// (and advance the position by that many bytes)
  /// </summary>
  public ReadOnlySpan<byte> TakeSlice(ReadOnlySpan<byte> span, int count)
  {
    var slice = span.Slice(Position, count);
    Position += count;
    return slice;
  }

  /// <summary>
  /// Check that there are no more bytes in <paramref name="span"/>:
  /// that the length of <paramref name="span"/> equals <see cref="Position"/>.
  /// An <see cref="ArgumentOutOfRangeException"/> is thrown otherwise
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException">
  /// Thrown if the span length does not exactly match the current position
  /// </exception>
  public void CheckEmpty(ReadOnlySpan<byte> span)
  {
    if(Position == span.Length)
    {
      return;
    }
    if(Position < span.Length)
    {
      throw new ArgumentOutOfRangeException(nameof(span), $"There are {span.Length - Position} bytes remaining in the span");
    }
    if(Position > span.Length)
    {
      // Unlikely to happen, unless you pass the wrong span
      throw new ArgumentOutOfRangeException(nameof(span), $"The reader overshot the span capacity by {span.Length - Position} bytes");
    }
  }
}

