/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.TreeMath;

/// <summary>
/// Description of ChunkSpace
/// </summary>
public class ChunkSpace<T> where T : IKeybagChunk
{
  private readonly Dictionary<ChunkId, T> _chunks;
  private ChunkChunkSetMap<T>? _allChildMap;
  private ChunkMapping<ChunkId>? _parentMap;
  private int _childMapChangeId;

  /// <summary>
  /// Create a new ChunkSpace
  /// </summary>
  public ChunkSpace()
  {
    _chunks = [];
    InvalidateChildMap();
  }

  /// <summary>
  /// Get an existing chunk by its ID
  /// </summary>
  public T this[ChunkId chunkId] {
    get => _chunks[chunkId];
  }

  /// <summary>
  /// Verify that a chunk ID is valid in this space, returning
  /// it if it is, or throwing an <see cref="ArgumentException"/>
  /// if it isn't.
  /// </summary>
  public ChunkId CheckValid(ChunkId testId)
  {
    return _chunks.ContainsKey(testId)
      ? testId
      : throw new ArgumentException(
        $"Chunk ID {testId.ToBase26()} not found in this space");
  }

  /// <summary>
  /// Get the chunk with the same ID as the given chunk
  /// </summary>
  public T this[IKeybagChunk chunk] {
    get => _chunks[chunk.NodeId];
  }

  /// <summary>
  /// Enumerate the chunks with the given IDs. It is an error
  /// if any of the IDs are not known in this space.
  /// </summary>
  /// <param name="chunkIds">
  /// The chunk IDs to look up (note that <see cref="ChunkSet{T}"/>
  /// is just a special, but common case of this)
  /// </param>
  /// <returns>
  /// The chunk objects corresponding to the given IDs
  /// </returns>
  public IEnumerable<T> this[IEnumerable<ChunkId> chunkIds] {
    get => chunkIds.Select(cid => _chunks[cid]);
  }

  /// <summary>
  /// Test if a chunk ID is known in this space
  /// </summary>
  public bool Contains(ChunkId chunkId)
  {
    return _chunks.ContainsKey(chunkId);
  }

  /// <summary>
  /// Test if the chunk that corresponds to the given descriptor
  /// is present in this space.
  /// </summary>
  public bool Contains(IKeybagChunk chunkDescriptor)
  {
    return _chunks.ContainsKey(chunkDescriptor.NodeId);
  }

  /// <summary>
  /// Test if the chunk that corresponds to the given descriptor's
  /// parent is present in this space.
  /// </summary>
  public bool ContainsParent(IKeybagChunk chunkDescriptor)
  {
    return _chunks.ContainsKey(chunkDescriptor.ParentId);
  }

  /// <summary>
  /// Find a chunk in this space by its ID, returning null if not found
  /// </summary>
  public T? Find(ChunkId chunkId)
  {
    return _chunks.TryGetValue(chunkId, out var chunk)
      ? chunk : default;
  }

  /// <summary>
  /// Find a chunk in this space via any descriptor for it,
  /// returning null if not found
  /// </summary>
  public T? Find(IKeybagChunk chunkDescriptor)
  {
    return _chunks.TryGetValue(chunkDescriptor.NodeId, out var chunk)
      ? chunk : default;
  }

  /// <summary>
  /// Find the parent of a chunk in this space (given as any descriptor for it),
  /// returning null if not found
  /// </summary>
  public T? FindParent(IKeybagChunk chunkDescriptor)
  {
    return _chunks.TryGetValue(chunkDescriptor.ParentId, out var chunk)
      ? chunk : default;
  }

  /// <summary>
  /// Find the parent of a chunk in this space (given as ID),
  /// returning null if not found
  /// </summary>
  public T? FindParent(ChunkId chunkId)
  {
    var node = Find(chunkId);
    return node != null ? Find(node.ParentId) : default;
  }

  /// <summary>
  /// Get all chunks in this space, in no particular order
  /// </summary>
  public IReadOnlyCollection<T> All { get => _chunks.Values; }

  /// <summary>
  /// Get all chunks in this space, in leaves-first or roots-first
  /// order. Siblings are not sorted in any specific order.
  /// </summary>
  /// <param name="leavesFirst">
  /// If true: return leaves first, roots last
  /// If false: return roots first, leaves last
  /// </param>
  public IEnumerable<ChunkId> AllIdsTopological(bool leavesFirst)
  {
    foreach(var chunkId in AllChildMap.RootIds)
    { 
      if(!leavesFirst)
      {
        yield return chunkId;
      }
      foreach(var childId in AllChildMap.Descendents(chunkId, leavesFirst))
      {
        yield return childId;
      }
      if(leavesFirst)
      {
        yield return chunkId;
      }
    }
  }

  /// <summary>
  /// Return the collection of all chunk IDs in this space in no particular order
  /// </summary>
  public IReadOnlyCollection<ChunkId> AllIds { get => _chunks.Keys; }

  /// <summary>
  /// Return the chunks whose parent is not in this space
  /// (most likely because it has no parent)
  /// </summary>
  public IEnumerable<T> Roots {
    get => _chunks.Values.Where(t => !_chunks.ContainsKey(t.ParentId));
  }

  /// <summary>
  /// Return a cached map of all chunks to their children
  /// (automatically recached on demand)
  /// </summary>
  public ChunkChunkSetMap<T> AllChildMap {
    get {
      if(_allChildMap == null
        || _childMapChangeId != _allChildMap.ChangeId
        || _parentMap == null)
      {
        ResetChildrenAndParentCache();
      }
      return _allChildMap!;
    }
  }

  /// <summary>
  /// Mapping from chunk IDs to their parent IDs.
  /// Chunk IDs without parent map to the default <see cref="ChunkId.Zero"/>.
  /// This mapping is cached and is invalidated by <see cref="InvalidateChildMap"/>.
  /// </summary>
  public ChunkMapping<ChunkId> ParentMap {
    get {
      if(_allChildMap == null
        || _childMapChangeId != _allChildMap.ChangeId
        || _parentMap == null)
      {
        ResetChildrenAndParentCache();
      }
      return _parentMap!;
    }
  }

  private void ResetChildrenAndParentCache()
  {
    _allChildMap = CreateMap(true);
    _childMapChangeId = _allChildMap.ChangeId;
    _parentMap = new ChunkMapping<ChunkId>(ChunkId.Zero);
    foreach(var chunk in _chunks.Values)
    {
      if(ContainsParent(chunk))
      {
        _parentMap[chunk.NodeId] = chunk.ParentId;
      }
    }
  }

  /// <summary>
  /// Invalidate <see cref="AllChildMap"/> and <see cref="ParentMap"/>,
  /// causing them to be recalculated upon next invocation.
  /// </summary>
  public void InvalidateChildMap()
  {
    _allChildMap = null;
    _childMapChangeId = -1;
  }

  /// <summary>
  /// Enumerate the ancestors of a chunk in this space
  /// </summary>
  /// <param name="start">
  /// The chunk to start with (it does not need to be in this space
  /// itself)
  /// </param>
  /// <param name="subset">
  /// Optional: the subset to constrain the search to.
  /// </param>
  /// <returns>
  /// The ancestors of the chunk, starting with the parent of the
  /// start chunk and going toward the root from there.
  /// </returns>
  public IEnumerable<T> Ancestors(
    IKeybagChunk start, ChunkSet<T>? subset = null)
  {
    var node = start;
    while(_chunks.TryGetValue(node.ParentId, out var parent))
    {
      if(subset != null && !subset[parent.NodeId])
      {
        break;
      }
      yield return parent;
      node = parent;
    }
  }

  /// <summary>
  /// Enumerate ancestor IDs of a chunk in this space, starting with
  /// the parent of <paramref name="chunkId"/> and going toward the root.
  /// </summary>
  /// <param name="chunkId">
  /// The chunk to find ancestors of
  /// </param>
  public IEnumerable<ChunkId> AncestorIds(ChunkId chunkId)
  {
    var nodeId = chunkId;
    while(ParentMap.Mapping.TryGetValue(nodeId, out var parentId))
    {
      yield return parentId;
      nodeId = parentId;
    }
  }

  /// <summary>
  /// Enumerate decendant IDs of a chunk in this space. Shorthand
  /// for <see cref="ChunkChunkSetMap{T}.Descendents"/> on
  /// <see cref="AllChildMap"/>.
  /// </summary>
  /// <param name="chunkId">
  /// The ID of the chunk to find descendents of.
  /// </param>
  /// <param name="leavesFirst">
  /// If true, enumerate the deepest descendents first and go toward
  /// <paramref name="chunkId"/>.
  /// If false, enumerate the children of <paramref name="chunkId"/> first
  /// and then work toward the leaves.
  /// </param>
  public IEnumerable<ChunkId> DescendentIds(ChunkId chunkId, bool leavesFirst=false)
  {
    return AllChildMap.Descendents(chunkId, leavesFirst);
  }

  /// <summary>
  /// Create a new empty <see cref="ChunkSet{T}"/> for this space
  /// </summary>
  public ChunkSet<T> CreateSet()
  {
    return new ChunkSet<T>(this);
  }

  /// <summary>
  /// Create a new <see cref="ChunkSet{T}"/> for this space
  /// initialized to contain the given chunk IDs
  /// </summary>
  public ChunkSet<T> CreateSet(IEnumerable<ChunkId> chunkIds)
  {
    return new ChunkSet<T>(this, chunkIds);
  }

  /// <summary>
  /// Create a new <see cref="ChunkSet{T}"/> for this space, optionally
  /// adding all known entry IDs to it
  /// </summary>
  /// <param name="fill">
  /// If true, add all entries in the set
  /// </param>
  public ChunkSet<T> CreateSet(bool fill)
  {
    return fill ? CreateSet(AllIds) : CreateSet();
  }

  /// <summary>
  /// Create a new <see cref="ChunkChunkSetMap{T}"/> for this space
  /// </summary>
  /// <param name="fillChildren">
  /// If false the map is initially empty, otherwise it is
  /// prepopulated with the children of each chunk in this space
  /// </param>
  public ChunkChunkSetMap<T> CreateMap(
    bool fillChildren)
  {
    var map = new ChunkChunkSetMap<T>(this);
    if(fillChildren)
    {
      map.ConnectFull();
    }
    return map;
  }

  /// <summary>
  /// Look up a chunk by its ID
  /// </summary>
  public bool TryGetChunk(ChunkId chunkId, [NotNullWhen(true)] out T chunk)
  {
    var ret = _chunks.TryGetValue(chunkId, out var chunk0);
    if(ret && chunk0 != null)
    {
      chunk = chunk0;
      return true;
    }
    chunk = default!;
    return false;
  }

  /// <summary>
  /// Register a chunk in this space
  /// </summary>
  public void Register(T chunk)
  {
    _chunks[chunk.NodeId] = chunk;
    InvalidateChildMap();
  }

  /// <summary>
  /// Return the argument chunk. If the chunk was not registered
  /// yet, do so. If the chunk ID was registered to another instance
  /// throw a <see cref="ArgumentException"/>
  /// </summary>
  /// <param name="chunk">
  /// The chunk to register if not yet done so
  /// </param>
  /// <returns>
  /// <paramref name="chunk"/>
  /// </returns>
  /// <exception cref="ArgumentException">
  /// Thrown if another chunk with the same ID was already registered
  /// </exception>
  public T RegisterIfUnknown(T chunk)
  {
    if(!_chunks.TryGetValue(chunk.NodeId, out var existing))
    {
      _chunks[chunk.NodeId] = chunk;
      InvalidateChildMap();
      return chunk;
    }
    else if(!Object.ReferenceEquals(chunk, existing))
    {
      throw new ArgumentException(
        $"Chunk {chunk.NodeId.ToBase26()} already registered with a different instance");
    }
    else
    {
      return existing;
    }
  }

  /// <summary>
  /// Create a new <see cref="ChunkSet{T}"/> containing the union
  /// of the argument sets.
  /// </summary>
  public ChunkSet<T> Union(params IEnumerable<ChunkId>[] sets)
  {
    var union = CreateSet();
    foreach(var set in sets)
    {
      union.AddRange(set);
    }
    return union;
  }

  /// <summary>
  /// Create a new set containing only the chunk IDs that are
  /// present in all of the argument sets.
  /// </summary>
  public ChunkSet<T> Intersection(params ChunkSet<T>[] sets)
  {
    var intersection = CreateSet();
    if(sets.Length > 0)
    {
      foreach(var id in sets[0])
      {
        if(sets.All(s => s.Contains(id)))
        {
          intersection.Add(id);
        }
      }
    }
    return intersection;
  }

  /// <summary>
  /// Create a new set containing only the chunk IDs that are
  /// present in all of the argument sets.
  /// </summary>
  public ChunkSet<T> Intersection(params IReadOnlySet<ChunkId>[] sets)
  {
    var intersection = CreateSet();
    if(sets.Length > 0)
    {
      foreach(var id in sets[0])
      {
        if(sets.All(s => s.Contains(id)))
        {
          intersection.Add(id);
        }
      }
    }
    return intersection;
  }

  // ---
}
