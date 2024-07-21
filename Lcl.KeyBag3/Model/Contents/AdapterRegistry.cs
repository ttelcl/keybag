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
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Stores <see cref="ContentAdapter{T}"/> instances for the supported
/// chunk types. Pseudo-singleton
/// </summary>
public class AdapterRegistry
{
  private readonly Dictionary<ChunkKind, ContentAdapter> _adapters;

  /// <summary>
  /// Create a new AdapterRegistry. Probably you want to access the
  /// <see cref="Default"/> instance instead.
  /// </summary>
  public AdapterRegistry()
  {
    _adapters = [];
  }

  /// <summary>
  /// Get the content adapter for the given chunk kind and validate that
  /// it models the chunk as type <typeparamref name="T"/>.
  /// </summary>
  public ContentAdapter<T> Get<T>(ChunkKind kind) where T : ContentBase
  {
    if(!_adapters.TryGetValue(kind, out var adapter))
    {
      throw new InvalidOperationException(
        $"Unrecognized chunk kind ({kind})");
    }
    if(adapter is ContentAdapter<T> cat)
    {
      return cat;
    }
    throw new InvalidOperationException(
      $"Incorrect model type for chunk kind {kind}");
  }

  /// <summary>
  /// Get the content adapter for the given chunk kind without checking
  /// the content model type it encodes or decodes
  /// </summary>
  public ContentAdapter GetUntyped(ChunkKind kind)
  {
    if(!_adapters.TryGetValue(kind, out var adapter))
    {
      throw new InvalidOperationException(
        $"Unrecognized chunk kind ({kind})");
    }
    return adapter;
  }

  /// <summary>
  /// Try to decode the given <paramref name="storedChunk"/>, returning
  /// null if decryption failed
  /// </summary>
  public T? TryDecryptModel<T>(
    ChunkCryptor cryptor,
    StoredChunk storedChunk) where T : ContentBase
  {
    var adapter = Get<T>(storedChunk.Kind);
    using(var decryptedBuffer = new ZapBuffer<byte>(storedChunk.Size))
    {
      if(!cryptor.TryDecryptContent(storedChunk, decryptedBuffer))
      {
        return null;
      }
      return adapter.DecodeTyped(decryptedBuffer.All);
    }
  }

  /// <summary>
  /// Decrypt <paramref name="storedChunk"/> to an instance of the model
  /// type <typeparamref name="T"/>
  /// </summary>
  public T DecryptModel<T>(
    ChunkCryptor cryptor,
    StoredChunk storedChunk) where T : ContentBase
  {
    var adapter = Get<T>(storedChunk.Kind);
    using(var decryptedBuffer = cryptor.DecryptContent(storedChunk))
    {
      return adapter.DecodeTyped(decryptedBuffer.Span);
    }
  }

  /// <summary>
  /// Decrypt <paramref name="storedChunk"/> to an instance of some unspecified
  /// subclass of <see cref="ContentBase"/>.
  /// </summary>
  public ContentBase DecryptModel(
    ChunkCryptor cryptor,
    StoredChunk storedChunk)
  {
    var adapter = GetUntyped(storedChunk.Kind);
    using(var decryptedBuffer = cryptor.DecryptContent(storedChunk))
    {
      return adapter.Decode(decryptedBuffer.Span);
    }
  }

  /// <summary>
  /// Encrypt a chunk from model form into stored form. The returned
  /// value uses a new <see cref="StoredChunk.EditId"/>, it is the responsibility
  /// of the caller to update the edit id of <paramref name="chunk"/>.
  /// </summary>
  /// <typeparam name="T">
  /// The used to model the chunk's content
  /// </typeparam>
  /// <param name="cryptor">
  /// The encryption engine (carrying the loaded key)
  /// </param>
  /// <param name="chunk">
  /// The chunk model to encode, wrap and encrypt
  /// </param>
  /// <param name="dangerousPreallocatedEditId">
  /// Instead of safely generating a new edit Id, assume the existing Edit ID
  /// is already safe to use. Do not pass true unless that condition is true.
  /// </param>
  /// <returns>
  /// The resulting <see cref="StoredChunk"/>, with a new
  /// <see cref="StoredChunk.EditId"/>. 
  /// </returns>
  public StoredChunk EncryptModel<T>(
    ChunkCryptor cryptor,
    ContentChunk<T> chunk,
    bool dangerousPreallocatedEditId = false) where T : ContentBase
  {
    var adapter = Get<T>(chunk.Kind);
    using(var plaintextBuffer = adapter.EncodeTyped(chunk.Content))
    {
      var storedChunk = cryptor.EncryptContent(
        chunk.FileId,
        plaintextBuffer.Span,
        chunk.Kind,
        chunk.Flags,
        chunk.NodeId,
        chunk.ParentId,
        dangerousPreallocatedEditId ? chunk.EditId : null);
      return storedChunk;
    }
  }

  /// <summary>
  /// Encrypt a chunk from model form into stored form. The returned
  /// value uses a new <see cref="StoredChunk.EditId"/>, it is the responsibility
  /// of the caller to update the edit id of <paramref name="chunk"/>.
  /// This method fails at runtime if the chunk content does not match the
  /// expected type; to avoid that use
  /// <see cref="EncryptModel{T}(ChunkCryptor, ContentChunk{T}, bool)"/> instead.
  /// </summary>
  /// <param name="cryptor">
  /// The encryption engine (carrying the loaded key)
  /// </param>
  /// <param name="chunk">
  /// The chunk model to encode, wrap and encrypt
  /// </param>
  /// <param name="dangerousPreallocatedEditId">
  /// Instead of safely generating a new edit Id, assume the existing Edit ID
  /// is already safe to use. Do not pass true unless that condition is true.
  /// </param>
  /// <returns>
  /// The resulting <see cref="StoredChunk"/>, with a new
  /// <see cref="StoredChunk.EditId"/>. 
  /// </returns>
  public StoredChunk EncryptModel(
    ChunkCryptor cryptor,
    ContentChunk chunk,
    bool dangerousPreallocatedEditId = false)
  {
    var adapter = GetUntyped(chunk.Kind);
    using(var plaintextBuffer = adapter.Encode(chunk.BaseContent))
    {
      var storedChunk = cryptor.EncryptContent(
        chunk.FileId,
        plaintextBuffer.Span,
        chunk.Kind,
        chunk.Flags,
        chunk.NodeId,
        chunk.ParentId,
        dangerousPreallocatedEditId ? chunk.EditId : null);
      return storedChunk;
    }
  }

  /// <summary>
  /// Get the default registry
  /// </summary>
  public static AdapterRegistry Default { get; } = 
    (new AdapterRegistry())
    .RegisterFileHeaderAdapter()
    .RegisterEntryAdapter();

  /// <summary>
  /// Register the default handler for <see cref="ChunkKind.File"/>
  /// (modeling the content as <see cref="EmptyContent"/>)
  /// </summary>
  public AdapterRegistry RegisterFileHeaderAdapter()
  {
    return RegisterEmpty(ChunkKind.File);
  }

  /// <summary>
  /// Register the default adapter for <see cref="ChunkKind.Entry"/>
  /// (modeling the content as <see cref="EntryContent"/>)
  /// </summary>
  public AdapterRegistry RegisterEntryAdapter()
  {
    return RegisterForContentModel<EntryContent>(
      ChunkKind.Entry,
      (ec, cb) => { ec.Serialize(cb); },
      cs => EntryContent.FromModel(cs));
  }

  /// <summary>
  /// Register an adapter for a <see cref="ChunkKind"/>
  /// </summary>
  /// <param name="adapter">
  /// The adapter to register
  /// </param>
  /// <returns>
  /// Returns this registry itself (enabling fluent calls)
  /// </returns>
  public AdapterRegistry Register(ContentAdapter adapter)
  {
    if(_adapters.ContainsKey(adapter.Kind))
    {
      throw new InvalidOperationException(
        $"Duplicate chunk kind handler for kind {adapter.Kind}");
    }
    _adapters[adapter.Kind] = adapter;
    return this;
  }

  /// <summary>
  /// Create a new <see cref="ContentModelAdapter{T}"/> and register it.
  /// The adapter is defined via delegates
  /// </summary>
  /// <typeparam name="T">
  /// The <see cref="ContentBase"/> subclass modeling the full in-memory
  /// representation for the chunk kind <paramref name="kind"/>
  /// </typeparam>
  /// <param name="kind">
  /// The kind of chunk to model
  /// </param>
  /// <param name="serializer">
  /// The delegate translating <typeparamref name="T"/> into a content model
  /// </param>
  /// <param name="deserializer">
  /// The delegate translating a content model into an instance of <typeparamref name="T"/>
  /// </param>
  /// <returns>
  /// This registry itself
  /// </returns>
  public AdapterRegistry RegisterForContentModel<T>(
    ChunkKind kind,
    Action<T, ContentBuilder> serializer,
    Func<ContentSlice, T> deserializer)
    where T : ContentBase
  {
    var adapter = new ContentModelAdapter<T>(kind, serializer, deserializer);
    return Register(adapter);
  }

  /// <summary>
  /// Register an adapter for the given chunk kind that uses an empty model.
  /// </summary>
  public AdapterRegistry RegisterEmpty(ChunkKind kind)
  {
    return Register(new EmptyContentAdapter(kind));
  }

}
