/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// An identifier for a key bag node. It has multiple interpretations.
/// For most purposes this is an opaque 6 byte integer (a 44 bit integer,
/// really). It can be interpreted as a timestamp (milliseconds since the
/// epoch), but may have been adjusted to guarantee uniqueness.
/// </summary>
public readonly struct ChunkId: IEquatable<ChunkId>
{
  /// <summary>
  /// Create a new NodeId
  /// </summary>
  public ChunkId(long value, bool allowZero = false)
  {
    if(!LooksValid(value, allowZero))
    {
      throw new ArgumentOutOfRangeException(nameof(value));
    }
    Value = value;
  }

  /// <summary>
  /// Get the zero valued NodeId
  /// </summary>
  public static ChunkId Zero { get; } = new ChunkId(0, true);

  /// <summary>
  /// The numerical value. At most 43 bits are used, so this guaranteed
  /// to fit in 6 bytes.
  /// </summary>
  public long Value { get; }

  /// <summary>
  /// Implements <see cref="IEquatable{NodeId}"/>
  /// </summary>
  public bool Equals(ChunkId other)
  {
    return Value == other.Value;
  }

  /// <summary>
  /// Overrides Object.Equals to ensure consistent equality behaviour
  /// </summary>
  public override bool Equals([NotNullWhen(true)] object? obj)
  {
    if(obj != null && obj is ChunkId other)
    {
      return Equals(other);
    }
    return false;
  }

  /// <summary>
  /// Equality operator
  /// </summary>
  public static bool operator ==(ChunkId left, ChunkId right)
  {
    return left.Value == right.Value;
  }

  /// <summary>
  /// Inequality operator
  /// </summary>
  public static bool operator !=(ChunkId left, ChunkId right)
  {
    return left.Value != right.Value;
  }

  /// <summary>
  /// Defers to this.Value.GetHashCode()
  /// </summary>
  public override int GetHashCode()
  {
    return Value.GetHashCode();
  }

  /// <summary>
  /// Render this NodeId as a 12 character hexadecimal string
  /// </summary>
  public override string ToString()
  {
    return ToBase26(); //  Value.ToString("X012");
  }

  /// <summary>
  /// Render this NodeId as a 9 character base 26 string
  /// </summary>
  public string ToBase26()
  {
    return IdToBase26(Value);
  }

  /// <summary>
  /// Return the time stamp corresponding to this <see cref="ChunkId"/>
  /// </summary>
  public DateTimeOffset ToStamp()
  {
    return IdToStamp(Value);
  }

  /// <summary>
  /// Return a textual representation of the timestamp corresponding
  /// to this ID in "yyyyMMdd-HHmmss-fff" format.
  /// </summary>
  public string ToStampText()
  {
    return
      IdToStamp(Value)
      .ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
  }

  /// <summary>
  /// Convert a DateTimeOffset to a NodeId
  /// </summary>
  public static ChunkId FromStamp(DateTimeOffset value)
  {
    return new ChunkId(value.ToUnixTimeMilliseconds());
  }

  /// <summary>
  /// Convert a 9 character base 26 string to a chunk ID
  /// </summary>
  public static ChunkId FromBase26(string base26)
  {
    return new ChunkId(Base26ToId(base26));
  }

  /// <summary>
  /// Check if a node ID looks like it may be valid. A node ID
  /// is valid if it corresponds to a time in a reasonably wide time
  /// range, between 2000-01-01 and more than a century into the future.
  /// Optionally the value 0 may be considered valid too.
  /// </summary>
  /// <param name="nodeIdValue">
  /// The node ID to check
  /// </param>
  /// <param name="allowZero">
  /// True to consider 0 a valid node ID (default false)
  /// </param>
  /// <returns>
  /// True if the id is considered valid
  /// </returns>
  /// <remarks>
  /// The lower cutoff was derived from observed keybag1 / keybag2 time
  /// stamps, whose earliest values come from a conversion process that
  /// ran on 2003-03-12
  /// </remarks>
  public static bool LooksValid(long nodeIdValue, bool allowZero = false)
  {
    return
      (allowZero && nodeIdValue == 0) // valid marker value
      || (
        nodeIdValue >= 0x000000DC6ACFAC00 // 2000-01-01, well before keybag1 existed
        && nodeIdValue < 0x000004F027A359FF); // far in the future (2142-01-20, "ZZZZZZZZZ" in base26)
  }

  /// <summary>
  /// Convert a node ID to a base26 string.
  /// </summary>
  public static string IdToBase26(long nodeId)
  {
    if(!LooksValid(nodeId, true))
    {
      throw new InvalidOperationException("Invalid node ID");
    }
    Span<char> chars = stackalloc char[9];
    for(int i = 0; i < chars.Length; i++)
    {
      var v = nodeId % 26;
      nodeId /= 26;
      chars[i] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[(int)v];
    }
    chars.Reverse();
    return new String(chars);
  }

  /// <summary>
  /// Convert a 9 character base26 string to a value
  /// </summary>
  public static long Base26ToId(string b26)
  {
    if(b26.Length != 9)
    {
      throw new InvalidOperationException(
        "Expecting a string of length 9");
    }
    b26 = b26.ToUpper();
    var id = 0L;
    foreach(var ch in b26)
    {
      var v = (ch-'A');
      if(v < 0 || v >= 26)
      {
        throw new InvalidOperationException(
          $"Expecting only ASCII letters but got '{ch}'");
      }
      id = id * 26L + v;
    }
    return id;
  }

  /// <summary>
  /// Convert a Node ID to its corresponding time stamp
  /// </summary>
  public static DateTimeOffset IdToStamp(long id)
  {
    return DateTimeOffset.FromUnixTimeMilliseconds(id);
  }

}
