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

namespace Lcl.KeyBag3.Model;

/// <summary>
/// A collection of chunks indexed by their ID
/// </summary>
public class ChunkMap<T> where T : IKeybagChunk
{
  private readonly Dictionary<long, T> _chunks;

  /// <summary>
  /// Create a new ChunkMap
  /// </summary>
  public ChunkMap()
  {
    _chunks = [];
  }

  /// <summary>
  /// The root node if it was added
  /// </summary>
  public T? Root { get; private set; }

  /// <summary>
  /// Try to find the chunk with the given ID in this map, returning
  /// null if not found
  /// </summary>
  /// <returns>
  /// The chunk, if found, or null if not found
  /// </returns>
  public T? FindChunk(ChunkId chunkId)
  {
    return _chunks.TryGetValue(chunkId.Value, out var chunk) ? chunk : default;
  }

  /// <summary>
  /// Return the chunk with the given id from this map, throwing
  /// a <see cref="KeyNotFoundException"/> if not found
  /// </summary>
  /// <returns>
  /// The chunk that was found
  /// </returns>
  /// <exception cref="KeyNotFoundException">
  /// Thrown if not found
  /// </exception>
  public T GetChunk(ChunkId chunkId)
  {
    if(!_chunks.TryGetValue(chunkId.Value, out var chunk))
    {
      throw new KeyNotFoundException(
        $"Missing chunk {chunkId} ({chunkId.ToBase26()})");
    }
    return chunk;
  }

  /// <summary>
  /// A read-only view on the collection of all chunks
  /// </summary>
  public IReadOnlyCollection<T> Chunks { get => _chunks.Values; }

  /// <summary>
  /// Try to find the chunk with the given ID in this map
  /// </summary>
  /// <param name="chunkId">
  /// The ID to find
  /// </param>
  /// <param name="chunk">
  /// Returns the chunk that was found. This will not be null if it was found
  /// (marked with <see cref="NotNullWhenAttribute"/>)
  /// </param>
  /// <returns>
  /// True if found, false if not
  /// </returns>
  public bool TryGetChunk(ChunkId chunkId, [NotNullWhen(true)] out T? chunk)
  {
    return _chunks.TryGetValue(chunkId.Value, out chunk);
  }

  /// <summary>
  /// Add a new chunk or replace an older chunk with a newer one.
  /// This operation fails when adding a root chunk if a different root
  /// chunk is already present.
  /// </summary>
  /// <param name="chunk">
  /// The chunk to insert
  /// </param>
  /// <returns>
  /// True if the chunk was newly added, or if an older version was
  /// replaced. False if the same or newer version was already present.
  /// </returns>
  public bool UpdateChunk(T chunk)
  {
    if(_chunks.TryGetValue(chunk.NodeId.Value, out var old))
    {
      if(old.EditId.Value < chunk.EditId.Value)
      {
        // replace
        _chunks[chunk.NodeId.Value] = chunk;
        if(chunk.ParentId.Value == 0L)
        {
          if(old.ParentId.Value != 0L)
          {
            throw new InvalidOperationException(
              "Attempt to change a non-root node into a root");
          }
          Root = chunk;
        }
        else if(old.ParentId.Value != 0L)
        {
          throw new InvalidOperationException(
            "Attempt to change a root node into a non-root");
        }
        return true;
      }
      else
      {
        return false;
      }
    }
    else
    {
      // add
      _chunks[chunk.NodeId.Value] = chunk;
      if(chunk.ParentId.Value == 0)
      {
        if(Root != null)
        {
          throw new InvalidOperationException(
            "Attempt to add more than one root node");
        }
        Root = chunk;
      }
      return true;
    }
  }

  /// <summary>
  /// Remove the chunk with the given ID. If it is the Root node, also
  /// set <see cref="Root"/> to null.
  /// </summary>
  /// <param name="chunkId">
  /// The ID of the chunk to remove
  /// </param>
  /// <returns>
  /// True if a chunk was removed
  /// </returns>
  public bool RemoveChunk(ChunkId chunkId)
  {
    var result = _chunks.Remove(chunkId.Value);
    if(Root != null && Root.NodeId.Value == chunkId.Value)
    {
      Root = default;
    }
    return result;
  }

  /// <summary>
  /// Find the chunk with the same <see cref="IKeybagChunk.NodeId"/> as the argument.
  /// </summary>
  public T? FindChunk(IKeybagChunk chunkRef)
  {
    return FindChunk(chunkRef.NodeId);
  }

  /// <summary>
  /// Get the chunk with the same <see cref="IKeybagChunk.NodeId"/> as the argument.
  /// </summary>
  public T GetChunk(IKeybagChunk chunkRef)
  {
    return GetChunk(chunkRef.NodeId);
  }

  /// <summary>
  /// Remove the chunk with the same <see cref="IKeybagChunk.NodeId"/> as the argument.
  /// </summary>
  public bool RemoveChunk(IKeybagChunk chunkRef)
  {
    return RemoveChunk(chunkRef.NodeId);
  }

  /// <summary>
  /// Try to find the chunk with the same <see cref="IKeybagChunk.NodeId"/> as the
  /// given reference chunk
  /// </summary>
  /// <param name="chunkRef">
  /// The chunk whose <see cref="IKeybagChunk.NodeId"/> is used to locate the target
  /// </param>
  /// <param name="chunk">
  /// Returns the chunk that was found. This will not be null if it was found
  /// (marked with <see cref="NotNullWhenAttribute"/>)
  /// </param>
  /// <returns>
  /// True if found, false if not
  /// </returns>
  public bool TryGetChunk(IKeybagChunk chunkRef, [NotNullWhen(true)] out T? chunk)
  {
    return _chunks.TryGetValue(chunkRef.NodeId.Value, out chunk);
  }

}
