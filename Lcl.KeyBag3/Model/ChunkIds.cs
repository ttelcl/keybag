/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// Node ID generator and translation of Node IDs to time stamps
/// </summary>
public class ChunkIds
{
  private long _lastStamp;

  /// <summary>
  /// Create a new NodeIds instance (the singleton)
  /// </summary>
  private ChunkIds()
  {
    _lastStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1L;
  }

  /// <summary>
  /// The singleton instance
  /// </summary>
  public static ChunkIds Default { get; } = new ChunkIds();

  /// <summary>
  /// Generate the next ID using the singleton instance
  /// </summary>
  public static ChunkId Next() => Default.NextId();

  /// <summary>
  /// Generate the next ID
  /// </summary>
  public ChunkId NextId()
  {
    // Assume there is no need for multithreading support.
    _lastStamp = NewStampAfter(_lastStamp);
    return new ChunkId(_lastStamp);
  }

  private static long NewStampAfter(long previous)
  {
    var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return (stamp <= previous) ? previous + 1L : stamp;
  }
}
