/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// Specialized <see cref="ChunkMap{T}"/> for storing <see cref="ChunkPair"/>
/// instances.
/// </summary>
public class ChunkPairMap: ChunkMap<ChunkPair>
{
  /// <summary>
  /// Create a new ChunkPairMap
  /// </summary>
  public ChunkPairMap()
  {
  }

  /// <summary>
  /// Update the <see cref="ChunkPair.ModelChunk"/> of the pair for
  /// the given model. Creates the pair if necessary. The update is skipped
  /// if it exists and already has the same or a newer model.
  /// </summary>
  /// <param name="model">
  /// The chunk model to add or update
  /// </param>
  /// <param name="pair">
  /// Returns the pair that hosts <paramref name="model"/> or its existing newer
  /// counterpart after the update.
  /// </param>
  /// <returns>
  /// True if a new pair was created, or a the existing pair was updated.
  /// False if the existing pair contained a newer model already
  /// </returns>
  public bool UpdateModel(ContentChunk model, out ChunkPair pair)
  {
    if(TryGetChunk(model, out var pair0))
    {
      pair = pair0;
      if(pair0.ModelChunk == null || pair0.ModelChunk.EditId.Value < model.EditId.Value)
      {
        pair0.SetModel(model);
        return true;
      }
      else
      {
        return false;
      }
    }
    else
    {
      pair = new ChunkPair(null, model);
      UpdateChunk(pair);
      return true;
    }
  }

  /// <summary>
  /// Update (or create) the <see cref="ChunkPair"/> corresponding to the 
  /// given <see cref="StoredChunk"/>. If the given chunk is older than
  /// either half of the existing pair the update is skipped (in that
  /// case a newer stored chunk exists or will be created at some later point)
  /// </summary>
  /// <param name="stored">
  /// The chunk to add
  /// </param>
  /// <param name="pair">
  /// Returns the affected <see cref="ChunkPair"/> (or the one that would
  /// have been affected if the update wasn't skipped)
  /// </param>
  /// <returns>
  /// True if the pair was created or updated, False if the update of the
  /// existing pair was skipped.
  /// </returns>
  public bool UpdateStored(StoredChunk stored, out ChunkPair pair)
  {
    if(TryGetChunk(stored, out var pair0))
    {
      pair = pair0;
      if(pair0.PersistChunk == null 
        || pair0.PersistChunk.EditId.Value < stored.EditId.Value)
      {
        if(pair0.ModelChunk != null 
          && pair0.ModelChunk.EditId.Value > stored.EditId.Value)
        {
          // skip attaching the persisted half if it is already outdated
          return false;
        }
        pair0.TrackStoredChunk(stored);
        return true;
      }
      else
      {
        return false;
      }
    }
    else
    {
      pair = new ChunkPair(stored, null);
      UpdateChunk(pair);
      return true;
    }
  }

  /// <summary>
  /// Insert the given <see cref="StoredChunk"/> instances that are not
  /// already outdated
  /// </summary>
  public void InsertAll(IEnumerable<StoredChunk> storedChunks)
  {
    foreach(StoredChunk storedChunk in storedChunks)
    {
      UpdateStored(storedChunk, out _);
    }
  }

  /// <summary>
  /// Insert all current <see cref="StoredChunk"/> instances in
  /// <paramref name="scm"/> that are not outdated already
  /// </summary>
  public void InsertAll(StoredChunkMap scm)
  {
    InsertAll(scm.CurrentChunks);
  }

  /// <summary>
  /// Insert all current <see cref="StoredChunk"/> instances in
  /// <paramref name="kb3"/> that are not outdated already
  /// </summary>
  public void InsertAll(Keybag kb3)
  {
    InsertAll(kb3.Chunks);
  }

  /// <summary>
  /// Ensure that all chunks are decrypted (by calling
  /// <see cref="ChunkPair.InitModel(ChunkCryptor)"/> on each chunk)
  /// </summary>
  /// <param name="cryptor">
  /// The decryption parameters
  /// </param>
  public void InitModels(ChunkCryptor cryptor)
  {
    foreach(var pair in Chunks)
    {
      pair.InitModel(cryptor);
    }
  }

  /// <summary>
  /// Ensure that all chunks of the given kind are decrypted (by calling
  /// <see cref="ChunkPair.InitModel(ChunkCryptor)"/> on each of them)
  /// </summary>
  /// <param name="cryptor">
  /// The decryption parameters
  /// </param>
  /// <param name="kind">
  /// The kind of chunks to decrypt
  /// </param>
  public void InitModels(ChunkCryptor cryptor, ChunkKind kind)
  {
    foreach(var pair in Chunks)
    {
      if(pair.Kind == kind)
      {
        pair.InitModel(cryptor);
      }
    }
  }

  /// <summary>
  /// Enumerate the model half of the pairs for which that
  /// model is present.
  /// </summary>
  /// <returns></returns>
  public IEnumerable<ContentChunk> GetInitializedModels()
  {
    return
      from chunk in Chunks
      let model = chunk.ModelChunk
      where model != null
      select model;
  }

  /// <summary>
  /// Prepare to save, creating missing or outdated encrypted versions
  /// of the models.
  /// </summary>
  /// <param name="cryptor">
  /// The encryption logic
  /// </param>
  /// <param name="dangerousPreallocatedEditId">
  /// If false (default, the safe option): new edit IDs will be generated
  /// for each chunk during encryption.
  /// If true: the <see cref="ContentChunk.EditId"/> of the chunk model
  /// is assumed to have been updated to a guaranteed new value for any
  /// chunk that needs encryption. Failing to meet this requirement breaks
  /// the AES-GCM assumption on uniqueness of the salt, breaking its security.
  /// </param>
  /// <returns>
  /// The number of entries for which a new <see cref="StoredChunk"/>
  /// was created.
  /// </returns>
  public int PrepareToSave(
    ChunkCryptor cryptor,
    bool dangerousPreallocatedEditId = false)
  {
    var count = 0;
    foreach(var chunk in Chunks)
    {
      if(chunk.PrepareToSave(cryptor, dangerousPreallocatedEditId))
      {
        count++;
      }
    }
    return count;
  }
}
