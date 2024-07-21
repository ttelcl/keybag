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
/// Tracks <see cref="ChunkId"/> values that are in use and
/// can generate new values for scenarios like conversion to KB3
/// </summary>
public class ChunkIdDb
{
  private readonly HashSet<long> _ids;

  /// <summary>
  /// Create a new ChunkIdDb
  /// </summary>
  public ChunkIdDb()
  {
    _ids = [];
  }

  /// <summary>
  /// Register a <see cref="ChunkId"/> in this database.
  /// The ID must not be present yet, otherwise an exception is thrown.
  /// </summary>
  /// <param name="id">
  /// The ID to register
  /// </param>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the ID is already present
  /// </exception>
  public void Register(ChunkId id)
  {
    if(_ids.Contains(id.Value))
    {
      throw new InvalidOperationException(
        $"ID Conflict: ID {id.ToBase26()} ({id.ToStampText()}) was already present");
    }
    _ids.Add(id.Value);
  }

  /// <summary>
  /// Merge the ids from another <see cref="ChunkIdDb"/> into this one
  /// </summary>
  public void Merge(ChunkIdDb other)
  {
    _ids.UnionWith(other._ids);
  }

  /// <summary>
  /// Register a <see cref="ChunkId"/> in this database if
  /// it is missing
  /// </summary>
  /// <param name="id">
  /// The ID to register
  /// </param>
  /// <returns>
  /// True if added, false if already present
  /// </returns>
  public bool RegisterIfMissing(ChunkId id)
  {
    return _ids.Add(id.Value);
  }

  /// <summary>
  /// Checks if the given ID is already known
  /// </summary>
  public bool Has(ChunkId id)
  {
    return _ids.Contains(id.Value);
  }

  /// <summary>
  /// Find a previously unused <see cref="ChunkId"/> near the given
  /// <paramref name="millis"/>.
  /// </summary>
  /// <param name="millis">
  /// The chunk id value (Unix milliseconds timestamp) to use as
  /// starting point.
  /// </param>
  /// <param name="future">
  /// If true searching for a new value increases <paramref name="millis"/>
  /// until an unused value is found. If false it decreases instead.
  /// </param>
  /// <returns>
  /// The previously unused and now registered chunk ID
  /// </returns>
  public ChunkId FindNew(long millis, bool future)
  {
    while(_ids.Contains(millis))
    {
      if(future)
      {
        millis++;
      }
      else
      {
        millis--;
      }
    }
    _ids.Add(millis);
    return new ChunkId(millis);
  }

  /// <summary>
  /// Find a previously unused <see cref="ChunkId"/> near the given
  /// <paramref name="refid"/>.
  /// </summary>
  /// <param name="refid">
  /// The chunk ID to use as start point reference
  /// </param>
  /// <param name="future">
  /// If true searching for a new value increases the start value
  /// until an unused value is found. If false it decreases instead.
  /// </param>
  /// <returns>
  /// The previously unused and now registered chunk ID
  /// </returns>
  public ChunkId FindNew(ChunkId refid, bool future)
  {
    return FindNew(refid.Value, future);
  }

  /// <summary>
  /// Find a previously unused <see cref="ChunkId"/> near the given
  /// <paramref name="dto"/>.
  /// </summary>
  /// <param name="dto">
  /// The time to start searching from
  /// </param>
  /// <param name="future">
  /// If true searching for a new value increases the start value
  /// until an unused value is found. If false it decreases instead.
  /// </param>
  /// <returns>
  /// The previously unused and now registered chunk ID
  /// </returns>
  public ChunkId FindNew(DateTimeOffset dto, bool future)
  {
    return FindNew(ChunkId.FromStamp(dto), future);
  }

  /// <summary>
  /// Find a previously unused <see cref="ChunkId"/> near the given
  /// <paramref name="ticks"/>.
  /// </summary>
  /// <param name="ticks">
  /// The time in UTC ticks to start searching from
  /// </param>
  /// <param name="future">
  /// If true searching for a new value increases the start value
  /// until an unused value is found. If false it decreases instead.
  /// </param>
  /// <returns>
  /// The previously unused and now registered chunk ID
  /// </returns>
  public ChunkId FindNewFromTicks(long ticks, bool future)
  {
    return FindNew(new DateTimeOffset(ticks, TimeSpan.Zero), future);
  }
}
