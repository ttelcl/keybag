/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.TreeMath;

/// <summary>
/// A subset of the chunks within a <see cref="ChunkSpace{T}"/>.
/// </summary>
public class ChunkSet<T>: IEnumerable<ChunkId> where T : IKeybagChunk
{
  private readonly HashSet<ChunkId> _chunks;

  /// <summary>
  /// Create a new ChunkSet
  /// </summary>
  internal ChunkSet(ChunkSpace<T> space)
  {
    Space = space;
    _chunks = [];
  }

  /// <summary>
  /// Create a new ChunkSet and add the given initial
  /// set of chunks to it
  /// </summary>
  internal ChunkSet(ChunkSpace<T> space, IEnumerable<ChunkId> initial)
    : this(space)
  {
    AddRange(initial);
  }

  /// <summary>
  /// The space this set is a subset of
  /// </summary>
  public ChunkSpace<T> Space { get; }

  /// <summary>
  /// Get or set the presence of a chunk in this set
  /// </summary>
  public bool this[ChunkId chunkId] {
    get {
      if(!Space.Contains(chunkId))
      {
        throw new ArgumentException(
          $"Chunk ID {chunkId} is not known in this space");
      }
      return _chunks.Contains(chunkId);
    }
    set {
      if(!Space.Contains(chunkId))
      {
        throw new ArgumentException(
          $"Chunk ID {chunkId} is not known in this space");
      }
      if(value)
      {
        _chunks.Add(chunkId);
      }
      else
      {
        _chunks.Remove(chunkId);
      }
    }
  }

  /// <summary>
  /// Get or set the presence of the chunk corresponding to
  /// the given descriptor
  /// </summary>
  public bool this[IKeybagChunk chunk] {
    get => this[chunk.NodeId];
    set => this[chunk.NodeId] = value;
  }

  /// <summary>
  /// The underlying set of chunk IDs
  /// </summary>
  public IReadOnlySet<ChunkId> ChunkIds => _chunks;

  /// <summary>
  /// Returns true if the given ID is in this set
  /// </summary>
  public bool Contains(ChunkId chunkId)
  {
    return _chunks.Contains(chunkId);
  }

  /// <summary>
  /// Add a single ChunkId to this set
  /// </summary>
  public bool Add(ChunkId chunkId)
  {
    if(!Space.Contains(chunkId))
    {
      throw new ArgumentException(
        $"Chunk ID {chunkId} is not known in this space");
    }
    return _chunks.Add(chunkId);
  }

  /// <summary>
  /// Remove a single ChunkId from this set
  /// </summary>
  public bool Remove(ChunkId chunkId)
  {
    if(!Space.Contains(chunkId))
    {
      throw new ArgumentException(
        $"Chunk ID {chunkId} is not known in this space");
    }
    return _chunks.Remove(chunkId);
  }

  /// <summary>
  /// Add zero or more ChunkIds to this set.
  /// See also <see cref="UnionWith(IEnumerable{ChunkId}[])"/>
  /// </summary>
  /// <returns>
  /// This modified set itself, enabling fluent calls
  /// </returns>
  public ChunkSet<T> AddRange(IEnumerable<ChunkId> chunkIds)
  {
    foreach(var chunkId in chunkIds)
    {
      Add(chunkId);
    }
    return this;
  }

  /// <summary>
  /// Remove zero or more ChunkIds from this set.
  /// See also <see cref="DifferenceWith(IEnumerable{ChunkId}[])"/>
  /// </summary>
  public ChunkSet<T> RemoveRange(IEnumerable<ChunkId> chunkIds)
  {
    foreach(var chunkId in chunkIds)
    {
      Remove(chunkId);
    }
    return this;
  }

  /// <summary>
  /// Enumerate the ids of the chunks in this set whose
  /// parents are not in this set.
  /// See also <see cref="RootSet()"/>
  /// </summary>
  public IEnumerable<ChunkId> RootIds {
    get {
      foreach(var chunkId in _chunks)
      {
        var chunk = Space[chunkId];
        if(!_chunks.Contains(chunk.ParentId))
        {
          yield return chunkId;
        }
      }
    }
  }

  /// <summary>
  /// Return a new ChunkSet containing the chunks in this set
  /// that have no parent in this set.
  /// See also <see cref="RootIds"/>
  /// </summary>
  public ChunkSet<T> RootSet()
  {
    return Space.CreateSet(RootIds);
  }

  /// <summary>
  /// Return a new ChunkSet containing the descendents of
  /// the chunk IDs in this set
  /// </summary>
  /// <param name="relations">
  /// Defines parent-child relationships between chunks.
  /// Defaults to <see cref="ChunkSpace{T}.AllChildMap"/>
  /// </param>
  public ChunkSet<T> DescendentsSet(
    ChunkChunkSetMap<T>? relations = null)
  {
    var result = Space.CreateSet();
    AddDescendentsTo(result, relations);
    return result;
  }

  /// <summary>
  /// Add the descendent IDs of the chunk IDs in this set
  /// to the given <paramref name="target"/> set.
  /// </summary>
  /// <param name="target">
  /// The set to modify (must be different from this set)
  /// </param>
  /// <param name="relations">
  /// Defines parent-child relationships between chunks.
  /// Defaults to <see cref="ChunkSpace{T}.AllChildMap"/>
  /// </param>
  public void AddDescendentsTo(
    ChunkSet<T> target,
    ChunkChunkSetMap<T>? relations = null)
  {
    relations ??= Space.AllChildMap;
    foreach(var chunkId in _chunks)
    {
      relations.AddDescendentsTo(chunkId, target);
    }
  }

  /// <summary>
  /// Return a new ChunkSet containing the ancestors of
  /// the chunk IDs in this set.
  /// </summary>
  /// <param name="includeSelf">
  /// If true, also include the chunk IDs in this set itself
  /// </param>
  public ChunkSet<T> AncestorSet(bool includeSelf = false)
  {
    var result = includeSelf ? Clone() : Space.CreateSet();
    AddAncestorsTo(result);
    return result;
  }

  /// <summary>
  /// Return the inversion of this set, i.e. the set of
  /// all chunk IDs in the space that are not in this set.
  /// </summary>
  public ChunkSet<T> Inverted()
  {
    var result = Space.CreateSet();
    foreach(var chunkId in Space.AllIds)
    {
      if(!_chunks.Contains(chunkId))
      {
        result.Add(chunkId);
      }
    }
    return result;
  }

  /// <summary>
  /// Add the ancestor IDs of the chunk IDs in this set
  /// to <paramref name="target"/>
  /// </summary>
  /// <param name="target">
  /// The set to add ancestors to (must be different from this set)
  /// </param>
  public void AddAncestorsTo(
    ChunkSet<T> target)
  {
    foreach(var chunkId in _chunks)
    {
      var x = Space.Find(chunkId);
      // Do NOT add x.NodeId (== chunkId)!
      while(x != null)
      {
        x = Space.Find(x.ParentId);
        if(x != null)
        {
          target.Add(x.NodeId);
        }
      }
    }
  }

  /// <summary>
  /// Add chunks connected to the chunks in this set to
  /// <paramref name="target"/>. 
  /// </summary>
  /// <param name="target">
  /// The target set to add the connected chunks to
  /// (must be different from this set)
  /// </param>
  /// <param name="includeSelf">
  /// If true, the chunks in this set are included
  /// </param>
  /// <param name="includeDescendents">
  /// If true, the descendents of this set are included
  /// </param>
  /// <param name="includeAncestors">
  /// If true, the ancestors of this set are included
  /// </param>
  /// <param name="relations">
  /// Defines parent-child relationships between chunks used
  /// to determine descendents. Defaults to
  /// <see cref="ChunkSpace{T}.AllChildMap"/>, but can be
  /// customized to include only certain relationships.
  /// </param>
  public void AddConnectedTo(
    ChunkSet<T> target,
    bool includeSelf = true,
    bool includeDescendents = true,
    bool includeAncestors = true,
    ChunkChunkSetMap<T>? relations = null)
  {
    if(includeDescendents)
    {
      AddDescendentsTo(target, relations);
    }
    if(includeAncestors)
    {
      AddAncestorsTo(target);
    }
    if(includeSelf)
    {
      target.AddRange(_chunks);
    }
  }

  /// <summary>
  /// Add the given chunk ID sets to this set
  /// </summary>
  public void UnionWith(params IEnumerable<ChunkId>[] chunkSets)
  {
    foreach(var chunkSet in chunkSets)
    {
      AddRange(chunkSet);
    }
  }

  /// <summary>
  /// Remove the given chunk ID sets from this set
  /// </summary>
  public void DifferenceWith(params IEnumerable<ChunkId>[] chunkSets)
  {
    foreach(var chunkSet in chunkSets)
    {
      RemoveRange(chunkSet);
    }
  }

  /// <summary>
  /// Implements <see cref="IEnumerable{T}.GetEnumerator"/>
  /// </summary>
  public IEnumerator<ChunkId> GetEnumerator()
  {
    return ((IEnumerable<ChunkId>)_chunks).GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return ((IEnumerable)_chunks).GetEnumerator();
  }

  /// <summary>
  /// Create a copy of this set
  /// </summary>
  public ChunkSet<T> Clone()
  {
    return Space.CreateSet(_chunks);
  }

  /// <summary>
  /// Return a new set containing the union of the two sets
  /// </summary>
  public static ChunkSet<T> operator +(ChunkSet<T> a, ChunkSet<T> b)
  {
    return a.Clone().AddRange(b);
  }

  /// <summary>
  /// Return a new set containing the difference of the two sets
  /// </summary>
  public static ChunkSet<T> operator -(ChunkSet<T> a, ChunkSet<T> b)
  {
    return a.Clone().RemoveRange(b);
  }

  /// <summary>
  /// Return a new set containing the intersection of the two sets
  /// </summary>
  public static ChunkSet<T> operator *(ChunkSet<T> a, ChunkSet<T> b)
  {
    var result = a.Space.CreateSet();
    foreach(var chunkId in a)
    {
      if(b.Contains(chunkId))
      {
        result.Add(chunkId);
      }
    }
    return result;
  }

  /// <summary>
  /// Return a new set containing the inverse of the set in its space
  /// </summary>
  public static ChunkSet<T> operator -(ChunkSet<T> a)
  {
    var result = a.Space.CreateSet();
    foreach(var chunkId in a.Space.AllIds)
    {
      if(!a.Contains(chunkId))
      {
        result.Add(chunkId);
      }
    }
    return result;
  }

  /// <summary>
  /// Enumerate the chunks objects corresponding to the IDs in this set
  /// </summary>
  public IEnumerable<T> Projection {
    get => Space[this];
  }

  // --------
}
