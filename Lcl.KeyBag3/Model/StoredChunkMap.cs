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
/// A collection of <see cref="StoredChunk"/>s, accessible by node id
/// and tracking history
/// </summary>
public class StoredChunkMap
{
  private readonly Dictionary<ChunkId, List<StoredChunk>> _trails;

  /// <summary>
  /// Create a new StoredNodesMap
  /// </summary>
  public StoredChunkMap()
  {
    _trails = [];
    ChangeCounter = 0;
  }

  /// <summary>
  /// The file node in this set, if there is any
  /// </summary>
  public StoredChunk? FileChunk { get; private set; }

  /// <summary>
  /// Incremented each time a node is added or removed
  /// </summary>
  public int ChangeCounter { get; private set; }

  /// <summary>
  /// Return the current version of a node, if it exists
  /// </summary>
  public StoredChunk? FindChunk(ChunkId nodeId)
  {
    var trail = FindTrail(nodeId);
    return trail != null && trail.Count > 0 ? trail[0] : null;
  }

  /// <summary>
  /// Insert a node that isn't in the history trail yet. This method
  /// ensures that a node's history trail is sorted newest to oldest.
  /// It also ensures that the collection contains only one file node.
  /// </summary>
  /// <param name="chunk">
  /// The node to insert
  /// </param>
  /// <returns>
  /// True if the node was inserted, false if the node already
  /// existed (same node id and same edit id, presumably the same
  /// node content) and was therefore not inserted.
  /// </returns>
  public bool PutChunk(StoredChunk chunk)
  {
    if(FileChunk != null && chunk.Kind==ChunkKind.File)
    {
      // A file node can have only one version and there can be only one
      // file node.
      if(chunk.NodeId.Value != FileChunk.NodeId.Value)
      {
        throw new InvalidOperationException(
          "A StoredNodesMap can have only one File Header Node");
      }
      if(chunk.EditId.Value != FileChunk.EditId.Value)
      {
        throw new InvalidOperationException(
          "Attempt to modify the file header node in this collection");
      }
    }
    var trail = GetTrail(chunk.NodeId);
    var index = trail.FindIndex(n => n.EditId.Value < chunk.EditId.Value);
    if(index < 0)
    {
      index = trail.Count;
    }
    else if(trail[index].EditId.Value == chunk.EditId.Value)
    {
      // node is already present (assuming an EditId uniquely identifies
      // the node for all time and nodes are immutable)
      return false;
    }
    if(chunk.Kind == ChunkKind.File)
    {
      FileChunk = chunk;
    }
    trail.Insert(index, chunk);
    ChangeCounter++;
    return true;
  }

  /// <summary>
  /// Put all given chunks and return the number that actually changed
  /// </summary>
  public int PutChunks(IEnumerable<StoredChunk> chunks)
  {
    return chunks.Count(PutChunk);
  }

  /// <summary>
  /// Put the stored chunks currently in <paramref name="pairs"/>
  /// into this map
  /// </summary>
  /// <param name="pairs">
  /// The pair map
  /// </param>
  /// <returns>
  /// the number of chunks copied to this <see cref="StoredChunkMap"/>
  /// </returns>
  public int PutChunks(ChunkPairMap pairs)
  {
    return PutChunks(
      from pair in pairs.Chunks
      let sc = pair.PersistChunk
      where sc != null
      select sc);
  }

  /// <summary>
  /// Return the history trail of a node, sorted newest first.
  /// If the node is unknown, an empty list is returned.
  /// </summary>
  /// <param name="nodeId">
  /// The id to find
  /// </param>
  /// <returns>
  /// A list of past instances of the node, the most recent first.
  /// Possibly an empty list if the node is not known.
  /// </returns>
  public IReadOnlyList<StoredChunk> ChunkHistory(ChunkId nodeId)
  {
    var trail = FindTrail(nodeId);
    return trail == null
      ? Array.Empty<StoredChunk>()
      : trail;
  }

  /// <summary>
  /// Return all history trails that have at least one entry chunk.
  /// </summary>
  public IEnumerable<IReadOnlyList<StoredChunk>> GetHistoryLists()
  {
    return _trails.Values.Where(list => list.Count > 0);
  }

  internal void RemoveAllHistory()
  {
    foreach(var trail in _trails.Values)
    {
      trail.RemoveRange(1, _trails.Count-1);
    }
    ChangeCounter++;
  }

  /// <summary>
  /// Remove a chunk that is not current from the history trail
  /// matching the given chunk reference.
  /// </summary>
  /// <param name="chunkReference">
  /// The chunk reference to match
  /// </param>
  /// <returns>
  /// True if a matching chunk was found (that was not the current
  /// version of the chunk) and it was removed.
  /// </returns>
  public bool RemoveOldChunk(IKeybagChunk chunkReference)
  {
    var trail = FindTrail(chunkReference.NodeId);
    if(trail != null)
    {
      var index = trail.FindIndex(n => n.EditId.Value == chunkReference.EditId.Value);
      if(index >= 1) // 0 is the current version of the chunk, refuse to remove it
      {
        trail.RemoveAt(index);
        ChangeCounter++;
        return true;
      }
    }
    return false;
  }

  /// <summary>
  /// The number of nodes being tracked (distinct node IDs)
  /// </summary>
  public int ChunkCount { get => _trails.Count; }

  /// <summary>
  /// Enumerate all the current versions of nodes
  /// </summary>
  public IEnumerable<StoredChunk> CurrentChunks {
    get => _trails.Values.Select(a => a[0]);
  }

  /// <summary>
  /// Get the history trail for a node, with the latest version as the first
  /// entry. If not known, a new empty trail is created.
  /// </summary>
  /// <param name="nodeId">
  /// The node ID to look for
  /// </param>
  /// <returns>
  /// The existing or new trail
  /// </returns>
  protected List<StoredChunk> GetTrail(ChunkId nodeId)
  {
    if(!_trails.TryGetValue(nodeId, out var trail))
    {
      trail = new List<StoredChunk>();
      _trails[nodeId] = trail;
    }
    return trail;
  }

  /// <summary>
  /// Find the history trail for a node, with the latest version as the first
  /// entry. If not known, null is returned.
  /// </summary>
  /// <param name="nodeId">
  /// The node ID to look for
  /// </param>
  /// <returns>
  /// The existing trail or null.
  /// </returns>
  protected List<StoredChunk>? FindTrail(ChunkId nodeId)
  {
    if(!_trails.TryGetValue(nodeId, out var trail))
    {
      return null;
    }
    return trail;
  }

}
