/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.TreeMath;

/// <summary>
/// A mapping of chunks to sets of chunks within
/// a <see cref="ChunkSpace{T}"/>. This can model any directed
/// graph, including trees.
/// </summary>
public class ChunkChunkSetMap<T> where T : IKeybagChunk
{
  private readonly Dictionary<ChunkId, HashSet<ChunkId>> _map;
  private readonly HashSet<ChunkId> _rootIds;

  /// <summary>
  /// Create a new ChunkChunkSetMap
  /// </summary>
  internal ChunkChunkSetMap(ChunkSpace<T> space)
  {
    _map = [];
    _rootIds = [];
    Space = space;
    ChangeId = 1;
  }

  /// <summary>
  /// Get the set of root chunk IDs in this map. This set is
  /// NOT maintained by all methods, so it may be incomplete.
  /// Only <see cref="ConnectFull"/> and <see cref="Connect(IKeybagChunk)"/>
  /// maintain this.
  /// </summary>
  public IReadOnlySet<ChunkId> RootIds { get => _rootIds; }

  /// <summary>
  /// Get the set of child chunk IDs for a given (parent) chunk ID
  /// (returning an empty set if none are known)
  /// </summary>
  public IReadOnlySet<ChunkId> this[ChunkId chunkId] {
    get {
      if(_map.TryGetValue(chunkId, out var children))
      {
        return children;
      }
      else
      {
        return FrozenSet<ChunkId>.Empty;
      }
    }
  }

  /// <summary>
  /// Return all chunk IDs and their child IDs in this map
  /// </summary>
  public IEnumerable<KeyValuePair<ChunkId, IReadOnlySet<ChunkId>>> All {
    get {
      foreach(var pair in _map)
      {
        yield return new KeyValuePair<ChunkId, IReadOnlySet<ChunkId>>(
          pair.Key, pair.Value);
      }
    }
  }

  /// <summary>
  /// Return a new map with the parent-child connections inverted.
  /// If this set describes a tree, the inverted set will have 
  /// at most one descendent in each value set, namely the parent.
  /// </summary>
  public ChunkChunkSetMap<T> Invert()
  {
    var inverted = new ChunkChunkSetMap<T>(Space);
    foreach(var pair in _map)
    {
      foreach(var childId in pair.Value)
      {
        inverted.Connect(childId, pair.Key);
      }
    }
    return inverted;
  }

  /// <summary>
  /// Enumerate all descendents of a chunk, either in leaves-first order
  /// or in leaves-last order. This uses only the parent-child connections
  /// in this map. If there are cycles, this method will not terminate.
  /// </summary>
  public IEnumerable<ChunkId> Descendents(ChunkId chunkId, bool leavesFirst)
  {
    foreach(var childId in this[chunkId])
    {
      if(leavesFirst)
      {
        foreach(var descendentId in Descendents(childId, leavesFirst))
        {
          yield return descendentId;
        }
      }
      yield return childId;
      if(!leavesFirst)
      {
        foreach(var descendentId in Descendents(childId, leavesFirst))
        {
          yield return descendentId;
        }
      }
    }
  }

  /// <summary>
  /// Enumerate the IDs of the ancestors of a chunk in this map.
  /// </summary>
  public IEnumerable<ChunkId> Ancestors(ChunkId chunkId)
  {
    var nodeId = chunkId;
    while(_map.ContainsKey(nodeId) && Space.TryGetChunk(nodeId, out var chunk))
    {
      if(chunkId != nodeId) // exclude the chunk itself
      {
        yield return chunk.NodeId;
      }
      nodeId = chunk.ParentId;
    }
  }

  /// <summary>
  /// Add the direct children of a chunk to the given
  /// <paramref name="chunkSet"/>
  /// </summary>
  public void AddChildrenTo(ChunkId parentId, ChunkSet<T> chunkSet)
  {
    if(_map.TryGetValue(parentId, out var children))
    {
      chunkSet.AddRange(children);
    }
  }

  /// <summary>
  /// Add the descendents of a chunk to the given
  /// <paramref name="chunkSet"/>
  /// </summary>
  public void AddDescendentsTo(ChunkId parentId, ChunkSet<T> chunkSet)
  {
    if(_map.TryGetValue(parentId, out var children))
    {
      foreach(var childId in children)
      {
        chunkSet.Add(childId);
        AddDescendentsTo(childId, chunkSet);
      }
    }
  }

  /// <summary>
  /// The space of valid chunks
  /// </summary>
  public ChunkSpace<T> Space { get; }

  internal int ChangeId { get; private set; }

  /// <summary>
  /// Connect a known parent chunk to a known child chunk
  /// by their IDs
  /// </summary>
  /// <returns>
  /// True if the connection was made, false if it already existed
  /// </returns>
  public bool Connect(ChunkId parentId, ChunkId childId)
  {
    if(!Space.Contains(parentId))
    {
      throw new ArgumentException(
        $"Parent chunk not found: {parentId.ToBase26()}");
    }
    if(!Space.Contains(childId))
    {
      throw new ArgumentException(
        $"Child chunk not found: {childId.ToBase26()}");
    }
    if(!_map.TryGetValue(parentId, out var children))
    {
      children = new HashSet<ChunkId>();
      _map[parentId] = children;
    }
    var added = children.Add(childId);
    if(added)
    {
      ChangeId++;
    }
    return added;
  }

  /// <summary>
  /// Disconnect a known parent chunk from a known child chunk
  /// by their IDs
  /// </summary>
  /// <returns>
  /// True if the connection was broken, false if it didn't exist
  /// </returns>
  public bool Disconnect(ChunkId parentId, ChunkId childId)
  {
    if(!Space.Contains(parentId))
    {
      throw new ArgumentException(
        $"Parent chunk not found: {parentId.ToBase26()}");
    }
    if(!Space.Contains(childId))
    {
      throw new ArgumentException(
        $"Child chunk not found: {childId.ToBase26()}");
    }
    if(!_map.TryGetValue(parentId, out var children))
    {
      return false;
    }
    var changed = children.Remove(childId);
    if(changed)
    {
      ChangeId++;
    }
    return changed;
  }

  /// <summary>
  /// Connect the two chunks. If necessary register them
  /// into this map's space first.
  /// </summary>
  public bool Connect(T parent, T child)
  {
    return Connect(
      Space.RegisterIfUnknown(parent).NodeId,
      Space.RegisterIfUnknown(child).NodeId);
  }

  /// <summary>
  /// Disconnect the two chunks. If necessary register them
  /// into this map's space first.
  /// </summary>
  public bool Disconnect(T parent, T child)
  {
    return Disconnect(
      Space.RegisterIfUnknown(parent).NodeId,
      Space.RegisterIfUnknown(child).NodeId);
  }

  /// <summary>
  /// Connect a child chunk to its parent chunk, if that
  /// parent exists in the space. Returns true if the
  /// connection was made or if it already was present,
  /// false if the parent wasn't found. Note that this
  /// return value has different semantics than the other
  /// Connect methods.
  /// </summary>
  /// <param name="childDescription">
  /// The child chunk descriptor to connect. The child node
  /// must already exist in the space, and can be equal or
  /// different from this descriptor.
  /// </param>
  /// <returns></returns>
  public bool Connect(IKeybagChunk childDescription)
  {
    if(childDescription.ParentId.Value != 0L
      && Space.Contains(childDescription.ParentId))
    {
      Connect(childDescription.ParentId, childDescription.NodeId);
      return true;
    }
    else
    {
      _rootIds.Add(childDescription.NodeId);
      return false;
    }
  }

  /// <summary>
  /// Connect all chunks in <see cref="Space"/> to their parents,
  /// if the parent exists in the space.
  /// </summary>
  public void ConnectFull()
  {
    foreach(var chunk in Space.All)
    {
      if(Space.Contains(chunk.ParentId))
      {
        Connect(chunk.ParentId, chunk.NodeId);
        _rootIds.Remove(chunk.NodeId);
      }
      else
      {
        _rootIds.Add(chunk.NodeId);
      }
    }
  }

  //
}
