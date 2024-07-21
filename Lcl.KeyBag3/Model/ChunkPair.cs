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
using Lcl.KeyBag3.Model.Contents;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// Wraps both an encrypted and a non-encrypted chunk. At least one of
/// them must be present, the other is optional
/// </summary>
public class ChunkPair: IKeybagChunk
{
  private readonly List<StoredChunk> _storedHistory;

  /// <summary>
  /// Create a new ChunkPair
  /// </summary>
  public ChunkPair(StoredChunk? persisted, ContentChunk? modeled)
  {
    _storedHistory = [];
    History = _storedHistory.AsReadOnly();
    if(persisted == null && modeled == null)
    {
      throw new ArgumentException(
        "The arguments cannot be both null");
    }
    if(persisted != null)
    {
      _storedHistory.Add(persisted);
    }
    ModelChunk = modeled;
    if(persisted != null && modeled != null)
    {
      if(persisted.NodeId.Value != modeled.NodeId.Value)
      {
        throw new ArgumentException(
          "Expecting arguments to be for the same chunk id");
      }
      if(persisted.FileId.Value != modeled.FileId.Value)
      {
        throw new ArgumentException(
          "Expecting arguments to be for the same file id");
      }
      if(persisted.Kind != modeled.Kind)
      {
        throw new ArgumentException(
          "Expecting arguments to be for the same chunk kind");
      }
      if(persisted.EditId.Value > modeled.EditId.Value)
      {
        throw new ArgumentException(
          "Expecting the edit ids to be equal, or the persisted one be older");
      }
      // note that flags, parent and edit id can differ
    }
  }

  /// <summary>
  /// If no <see cref="ModelChunk"/> is present yet, decrypt it from the
  /// <see cref="PersistChunk"/>
  /// </summary>
  /// <param name="cryptor">
  /// Decryption parameters
  /// </param>
  /// <returns>
  /// This <see cref="ChunkPair"/> itself, enabling use in fluent cals.
  /// </returns>
  public ChunkPair InitModel(ChunkCryptor cryptor)
  {
    if(ModelChunk == null && PersistChunk != null)
    {
      ModelChunk = ContentChunk.DeserializeUntyped(cryptor, PersistChunk);
    }
    return this;
  }

  /// <summary>
  /// A readonly view on current and past <see cref="StoredChunk"/> versions
  /// of this chunk, with the latest version first.
  /// </summary>
  public IReadOnlyList<StoredChunk> History { get; }

  /// <summary>
  /// The immutable encrypted chunk, matching the current or imminent in-file model
  /// </summary>
  public StoredChunk? PersistChunk { get => History.Count == 0 ? null : History[0]; }

  /// <summary>
  /// The mutable unencrypted chunk, exposing the chunk's content.
  /// The model is only roughly typed, as a <see cref="ContentChunk"/>
  /// carrying an unspecified subclass if <see cref="ContentBase"/>
  /// as content model. Consider using <see cref="TryModelAs{T}"/>
  /// to access it if you know the actual type.
  /// </summary>
  public ContentChunk? ModelChunk { get; private set; }

  /// <summary>
  /// Return the strongly typed content of <see cref="ModelChunk"/> if
  /// that is available at all and matches <typeparamref name="T"/>;
  /// return null otherwise.
  /// </summary>
  public T? TryModelAs<T>() where T : ContentBase
  {
    return ModelChunk?.BaseContent as T;
  }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.Kind"/> by forwarding it
  /// from <see cref="LeadVariant"/>.
  /// </summary>
  public ChunkKind Kind { get => LeadVariant.Kind; }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.Flags"/> by forwarding it
  /// from <see cref="LeadVariant"/>.
  /// </summary>
  public ChunkFlags Flags { get => LeadVariant.Flags; }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.NodeId"/> by forwarding it
  /// from <see cref="LeadVariant"/>.
  /// </summary>
  public ChunkId NodeId { get => LeadVariant.NodeId; }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.EditId"/> by forwarding it
  /// from <see cref="LeadVariant"/>.
  /// </summary>
  public ChunkId EditId { get => LeadVariant.EditId; }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.ParentId"/> by forwarding it
  /// from <see cref="LeadVariant"/>.
  /// </summary>
  public ChunkId ParentId { get => LeadVariant.ParentId; }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.FileId"/> by forwarding it
  /// from <see cref="LeadVariant"/>.
  /// </summary>
  public ChunkId FileId { get => LeadVariant.FileId; }

  /// <summary>
  /// Insert the given chunk in the <see cref="History"/> list, preserving
  /// the sort order as newest to oldest. If a chunk with the same edit ID
  /// already exists it is treated as a duplicate and the provided chunk is
  /// not inserted.
  /// </summary>
  /// <param name="storedChunk">
  /// The chunk to be inserted
  /// </param>
  /// <returns>
  /// True if inserted, false if not inserted because it is a duplicate.
  /// </returns>
  public bool TrackStoredChunk(StoredChunk storedChunk)
  {
    if(ModelChunk != null && storedChunk.EditId.Value > ModelChunk.EditId.Value)
    {
      // ModelChunk should have been updated (or discarded) first
      throw new InvalidOperationException(
        "Attempt to track a persisted model that is newer than the active model");
    }
    for(var i = 0; i < _storedHistory.Count; i++)
    {
      if(_storedHistory[i].EditId.Value < storedChunk.EditId.Value)
      {
        _storedHistory.Insert(i, storedChunk);
        return true;
      }
      if(_storedHistory[i].EditId.Value == storedChunk.EditId.Value)
      {
        // Duplicate detected. Do not store
        return false;
      }
    }
    _storedHistory.Add(storedChunk);
    return true;
  }

  /// <summary>
  /// Get <see cref="ModelChunk"/> or <see cref="PersistChunk"/> if the
  /// former is null. The properties returned define the properties of
  /// this pair
  /// </summary>
  public IKeybagChunk LeadVariant {
    get => ((IKeybagChunk?)ModelChunk) ?? PersistChunk ??
      throw new InvalidOperationException("the chunk variants cannot be both null");
  }

  /// <summary>
  /// Discard <see cref="ModelChunk"/>. Fails if the persisted history is
  /// empty (and <see cref="PersistChunk"/> therefore is null)
  /// </summary>
  public void DiscardModel()
  {
    if(_storedHistory.Count == 0)
    {
      throw new InvalidOperationException(
        "Cannot discard the active model without having a persisted backup");
    }
    ModelChunk = null;
  }

  /// <summary>
  /// Change (or set) <see cref="ModelChunk"/>. The model must be no older than
  /// the current value of <see cref="PersistChunk"/>
  /// </summary>
  public void SetModel(ContentChunk model)
  {
    if(PersistChunk != null && PersistChunk.EditId.Value > model.EditId.Value)
    {
      throw new InvalidOperationException("Attempt to attach a model that is already outdated");
    }
    ModelChunk = model;
  }

  /// <summary>
  /// True if <see cref="ModelChunk"/> is newer than <see cref="PersistChunk"/>, and
  /// the persisted chunk needs updating
  /// </summary>
  public bool NeedsPersisting {
    get {
      return
        ModelChunk != null &&
        (
          PersistChunk == null
          || PersistChunk.EditId.Value < ModelChunk.EditId.Value
          || ModelChunk.Modified
        );
    }
  }

  /// <summary>
  /// If <see cref="PersistChunk"/> is outdated (or missing), recalculate it to match
  /// <see cref="ModelChunk"/>. This will update the <see cref="IKeybagChunk.EditId"/>s
  /// of both as a side effect.
  /// </summary>
  /// <param name="cryptor">
  /// The encryption parameters
  /// </param>
  /// <param name="dangerousPreallocatedEditId">
  /// If true, the model's <see cref="ContentChunk.EditId"/> has already been
  /// assigned a guaranteed safe value and no new edit id is generated.
  /// This risks violating preconditions assumed in encryption, potentially
  /// leading to a security hole (it bypasses the safeguard against
  /// AES-GCM salt reuse).
  /// </param>
  /// <returns>
  /// True if <see cref="PersistChunk"/> was updated, false if it wasn't
  /// </returns>
  public bool PrepareToSave(
    ChunkCryptor cryptor,
    bool dangerousPreallocatedEditId = false)
  {
    if(NeedsPersisting)
    {
      if(dangerousPreallocatedEditId)
      {
        // last chance check on constraint violation
        if(PersistChunk?.EditId.Value == ModelChunk!.EditId.Value)
        {
          throw new InvalidOperationException(
            "Internal error (AES-GCM precondition violated)");
        }
      }
      var stored = ModelChunk!.SerializeUntyped(cryptor, null, dangerousPreallocatedEditId);
      TrackStoredChunk(stored);
      return true;
    }
    else
    {
      return false;
    }
  }

}
