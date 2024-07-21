/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// A content adapter intermediary for content modeled using
/// the <see cref="ContentModel"/> (through <see cref="ContentSlice"/>
/// and <see cref="ContentBuilder"/>)
/// </summary>
public class ContentModelAdapter<T>: ContentAdapter<T> where T : ContentBase
{
  private readonly Action<T, ContentBuilder> _serializer;
  private readonly Func<ContentSlice, T> _deserializer;

  /// <summary>
  /// Create a new ContentModelAdapter
  /// </summary>
  public ContentModelAdapter(
    ChunkKind kind,
    Action<T, ContentBuilder> serializer,
    Func<ContentSlice, T> deserializer)
    : base(kind)
  {
    _serializer = serializer;
    _deserializer = deserializer;
  }

  /// <inheritdoc/>
  protected sealed internal override T DecodeTyped(
    ReadOnlySpan<byte> decryptedContent)
  {
    var unwrappedSize = ContentAdapter.UnwrappedSize(decryptedContent);
    using(var unwrapped = UnwrapChunk(decryptedContent))
    {
      var slice = new ContentSlice(unwrapped);
      return _deserializer(slice);
    }
  }

  /// <inheritdoc/>
  protected sealed internal override CryptoBuffer<byte> EncodeTyped(
    T model)
  {
    using(var cb = new ContentBuilder())
    {
      _serializer(model, cb);
      return ContentAdapter.WrapChunk(cb.GetContent(), true);
    }
  }
}
