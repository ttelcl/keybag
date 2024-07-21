/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.TreeMath;

/// <summary>
/// Maps chunks to some other type of value, to be used
/// as a flyweight property store.
/// </summary>
public class ChunkMapping<TValue>
{
  private readonly Dictionary<ChunkId, TValue> _map;

  /// <summary>
  /// Create a new ChunkMapping
  /// </summary>
  /// <param name="defaultValue">
  /// The value to return for any chunk not explicitly mapped
  /// </param>
  public ChunkMapping(
    TValue defaultValue)
  {
    _map = [];
    DefaultValue = defaultValue;
  }

  /// <summary>
  /// The value to return for any chunk not explicitly mapped
  /// </summary>
  public TValue DefaultValue { get; }

  /// <summary>
  /// All key-value pairs in this mapping (with keys being
  /// the chunk's <see cref="IKeybagChunk.NodeId"/>)
  /// </summary>
  public IReadOnlyDictionary<ChunkId, TValue> Mapping {
    get => _map;
  }

  /// <summary>
  /// Get or set the value for a given chunk ID.
  /// Getting the value for an unknown key returns <see cref="DefaultValue"/>.
  /// </summary>
  public TValue this[ChunkId chunkId] {
    get => _map.TryGetValue(chunkId, out var value)
      ? value
      : DefaultValue;
    set => _map[chunkId] = value;
  }

  /// <summary>
  /// Get or set the value for the given chunk's ID.
  /// </summary>
  public TValue this[IKeybagChunk chunk] {
    get => this[chunk.NodeId];
    set => this[chunk.NodeId] = value;
  }

  /// <summary>
  /// Set the value for all chunk IDs in the set to <paramref name="value"/>.
  /// </summary>
  /// <param name="chunks">
  /// The set of chunks to set the value for
  /// </param>
  /// <param name="value">
  /// The value to set
  /// </param>
  public void SetAll(IEnumerable<ChunkId> chunks, TValue value)
  {
    foreach(var chunk in chunks)
    {
      this[chunk] = value;
    }
  }

  /// <summary>
  /// Return a new <see cref="ChunkSet{T}"/> containing all chunk IDs
  /// for which the value in this mapping matches the given condition.
  /// </summary>
  /// <typeparam name="TSet">
  /// The chunk set's chunk type
  /// </typeparam>
  /// <param name="space">
  /// The space backing the resulting chunk set
  /// </param>
  /// <param name="condition">
  /// The function that determines whether a value matches
  /// </param>
  /// <returns>
  /// A new <see cref="ChunkSet{T}"/>
  /// </returns>
  public ChunkSet<TSet> WhereValue<TSet>(
    ChunkSpace<TSet> space,
    Func<TValue, bool> condition) where TSet : IKeybagChunk
  {
    return space.CreateSet(WhereValue(condition));
  }

  /// <summary>
  /// Enumerate all chunk IDs for which the value in this mapping
  /// matches the given condition.
  /// </summary>
  /// <param name="condition">
  /// The condition to match
  /// </param>
  /// <returns>
  /// An enumeration of chunk IDs
  /// </returns>
  public IEnumerable<ChunkId> WhereValue(Func<TValue, bool> condition)
  {
    return _map.Where(kvp => condition(kvp.Value)).Select(kvp => kvp.Key);
  }

  /// <summary>
  /// Remove the mapping for the given chunk ID.
  /// </summary>
  public bool Remove(ChunkId chunkId)
  {
    return _map.Remove(chunkId);
  }

}
