/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Model.Contents;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// A chunk with its fully decoded roughly typed content
/// </summary>
public class ContentChunk: IKeybagChunk
{
  private ChunkFlags _flags;

  /// <summary>
  /// Create a new ContentChunk
  /// </summary>
  public ContentChunk(
    ChunkKind kind,
    ChunkFlags flags,
    ChunkId nodeId,
    ChunkId editId,
    ChunkId parentId,
    ChunkId fileId,
    ContentBase content)
  {
    Kind = kind;
    NodeId = nodeId;
    EditId = editId;
    ParentId = parentId;
    FileId = fileId;
    BaseContent = content;
    _flags = flags;
    if(kind == ChunkKind.File)
    {
      if(FileId.Value != NodeId.Value)
      {
        throw new ArgumentException(
          "For file header chunks the node ID should equal the file ID");
      }
    }
  }

  /// <summary>
  /// The chunk Kind
  /// </summary>
  public ChunkKind Kind { get; }

  /// <summary>
  /// The chunk flags. Changing this also sets <see cref="MetadataModified"/>.
  /// </summary>
  public ChunkFlags Flags {
    get => _flags;
    set {
      MetadataModified |= value != Flags;
      _flags = value;
    }
  }

  /// <summary>
  /// The chunk ID
  /// </summary>
  public ChunkId NodeId { get; }

  /// <summary>
  /// The edit id
  /// </summary>
  public ChunkId EditId { get; protected set; }

  /// <summary>
  /// The parent's chunk id
  /// </summary>
  public ChunkId ParentId { get; }

  /// <summary>
  /// The ID of the file this is part of
  /// </summary>
  public ChunkId FileId { get; }

  /// <summary>
  /// The content model
  /// </summary>
  public ContentBase BaseContent { get; }

  /// <summary>
  /// Get the content cast as a subclass of <see cref="ContentBase"/>.
  /// </summary>
  public T2 GetContentAs<T2>() where T2 : ContentBase
  {
    if(BaseContent is T2 t2)
    {
      return t2;
    }
    else
    {
      throw new InvalidOperationException(
        "Incompatible model type");
    }
  }

  /// <summary>
  /// Try to get the content cast as a subclass of <see cref="ContentBase"/>.
  /// </summary>
  public T2? TryGetContentAs<T2>() where T2 : ContentBase
  {
    if(BaseContent is T2 t2)
    {
      return t2;
    }
    else
    {
      return null;
    }
  }

  /// <summary>
  /// True if any of the non-content fields have been modified since
  /// construction or serialization. Cleared by 
  /// <see cref="SerializeUntyped"/>
  /// </summary>
  public bool MetadataModified { get; protected set; }

  /// <summary>
  /// True if the metadata fields or the content have changed since
  /// construction or the last serialization
  /// </summary>
  public bool Modified { get => MetadataModified || BaseContent.Modified; }

  /// <summary>
  /// Flag to enable additional debugging. Should not be active under
  /// normal circumstances because this may leak information
  /// </summary>
  public bool DoDebug { get; set; }

  /// <summary>
  /// Serialize this model to a <see cref="StoredChunk"/>. This method
  /// assumes the model has changed and generates a new <see cref="EditId"/>.
  /// It also clears the <see cref="MetadataModified"/> and
  /// <see cref="BaseContent"/>.<see cref="ContentBase.Modified"/> flags.
  /// </summary>
  /// <param name="cryptor">
  /// Encryption information: the encryption key and the ID generator
  /// </param>
  /// <param name="registry">
  /// If not null: a custom registry of model adapters. If null
  /// <see cref="AdapterRegistry.Default"/> is used.
  /// </param>
  /// <param name="dangerousPreallocatedEditId">
  /// True to use the existing <see cref="EditId"/> as new encrypted edit
  /// id because you already updated it to a new never-before used value.
  /// This has a high risk of causing a security hole (it may lead to
  /// accidentally reusing an AES-GCM salt value).
  /// If false (default) a new ID based on the current time is generated.
  /// </param>
  /// <returns>
  /// The newly generated <see cref="StoredChunk"/>
  /// </returns>
  public StoredChunk SerializeUntyped(
    ChunkCryptor cryptor,
    AdapterRegistry? registry = null,
    bool dangerousPreallocatedEditId = false)
  {
    registry ??= AdapterRegistry.Default;
    var result = registry.EncryptModel(cryptor, this, dangerousPreallocatedEditId);
    EditId = result.EditId;
    BaseContent.Modified = false;
    MetadataModified = false;
    return result;
  }

  /// <summary>
  /// Create a new <see cref="ContentChunk{T}"/> from its encrypted,
  /// wrapped, and encoded form.
  /// </summary>
  /// <param name="cryptor">
  /// Decryption parameters
  /// </param>
  /// <param name="storedChunk">
  /// The stored form of the chunk
  /// </param>
  /// <param name="registry">
  /// If not null: override the model decoder registry
  /// </param>
  /// <returns>
  /// The newly created <see cref="ContentChunk{T}"/>.
  /// </returns>
  public static ContentChunk DeserializeUntyped(
    ChunkCryptor cryptor,
    StoredChunk storedChunk,
    AdapterRegistry? registry = null)
  {
    registry ??= AdapterRegistry.Default;
    var model = registry.DecryptModel(cryptor, storedChunk);
    var result = new ContentChunk(
      storedChunk.Kind,
      storedChunk.Flags,
      storedChunk.NodeId,
      storedChunk.EditId,
      storedChunk.ParentId,
      storedChunk.FileId,
      model);
    model.Modified = false;
    result.MetadataModified = false;
    return result;
  }

}

/// <summary>
/// A chunk with its fully decoded strong typed content
/// </summary>
public class ContentChunk<T>: ContentChunk, IKeybagChunk where T : ContentBase
{
  /// <summary>
  /// Create a new ContentChunk
  /// </summary>
  public ContentChunk(
    ChunkKind kind,
    ChunkFlags flags,
    ChunkId nodeId,
    ChunkId editId,
    ChunkId parentId,
    ChunkId fileId,
    T content)
    : base(kind, flags, nodeId, editId, parentId, fileId, content)
  {
    Content = content;
  }

  /// <summary>
  /// The content model
  /// </summary>
  public T Content { get; }

  /// <summary>
  /// Serialize this model to a <see cref="StoredChunk"/>. This method
  /// assumes the model has changed and generates a new <see cref="ContentChunk.EditId"/>.
  /// It also clears the <see cref="ContentChunk.MetadataModified"/> and
  /// <see cref="Content"/>.<see cref="ContentBase.Modified"/> flags.
  /// </summary>
  /// <param name="cryptor">
  /// Encryption information: the encryption key and the ID generator
  /// </param>
  /// <param name="registry">
  /// If not null: a custom registry of model adapters. If null
  /// <see cref="AdapterRegistry.Default"/> is used.
  /// </param>
  /// <param name="dangerousPreallocatedEditId">
  /// Instead of safely generating a new edit Id, assume the existing Edit ID
  /// is already safe to use. Do not pass true unless that condition is true.
  /// </param>
  /// <returns>
  /// The newly generated <see cref="StoredChunk"/>
  /// </returns>
  public StoredChunk Serialize(
    ChunkCryptor cryptor,
    AdapterRegistry? registry = null,
    bool dangerousPreallocatedEditId = false)
  {
    registry ??= AdapterRegistry.Default;
    var result = registry.EncryptModel(cryptor, this, dangerousPreallocatedEditId);
    EditId = result.EditId;
    Content.Modified = false;
    MetadataModified = false;
    return result;
  }

  /// <summary>
  /// Create a new <see cref="ContentChunk{T}"/> from its encrypted,
  /// wrapped, and encoded form.
  /// </summary>
  /// <param name="cryptor">
  /// Decryption parameters
  /// </param>
  /// <param name="storedChunk">
  /// The stored form of the chunk
  /// </param>
  /// <param name="registry">
  /// If not null: override the model decoder registry
  /// </param>
  /// <returns>
  /// The newly created <see cref="ContentChunk{T}"/>.
  /// </returns>
  public static ContentChunk<T> Deserialize(
    ChunkCryptor cryptor,
    StoredChunk storedChunk,
    AdapterRegistry? registry = null)
  {
    registry ??= AdapterRegistry.Default;
    var model = registry.DecryptModel<T>(cryptor, storedChunk);
    var result = new ContentChunk<T>(
      storedChunk.Kind,
      storedChunk.Flags,
      storedChunk.NodeId,
      storedChunk.EditId,
      storedChunk.ParentId,
      storedChunk.FileId,
      model);
    model.Modified = false;
    result.MetadataModified = false;
    return result;
  }
}