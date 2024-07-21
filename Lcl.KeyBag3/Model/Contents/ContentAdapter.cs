/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Abstracted content encoder / decoder
/// </summary>
public abstract class ContentAdapter
{
  /// <summary>
  /// Create a new ContentAdapter
  /// </summary>
  protected ContentAdapter(
    ChunkKind kind)
  {
    Kind = kind;
  }

  /// <summary>
  /// The chunk kind handled by this adapter
  /// </summary>
  public ChunkKind Kind { get; }

  /// <summary>
  /// Creates a content model from a chunk's decrypted content
  /// </summary>
  protected internal abstract ContentBase Decode(
    ReadOnlySpan<byte> decryptedContent);

  /// <summary>
  /// creates an unencrypted chunk content from a content model
  /// </summary>
  /// <param name="model">
  /// The model to serialize
  /// </param>
  protected internal abstract CryptoBuffer<byte> Encode(
    ContentBase model);

  /// <summary>
  /// Given standard decrypted chunk content, calculate the
  /// size after unwrapping (that most likely is: the decompressed
  /// size)
  /// </summary>
  public static int UnwrappedSize(ReadOnlySpan<byte> decrypted)
  {
    if(decrypted.Length == 0)
    {
      return 0;
    }
    var reader = new SpanReader();
    reader.ReadVarInt(decrypted, out var count);
    if(count == 0)
    {
      // indicates uncompressed content
      return reader.BytesLeft(decrypted);
    }
    if(count < 0)
    {
      throw new InvalidDataException(
        "Internal error. Not exapecting a negative VarInt");
    }
    if(count > 0x00100000)
    {
      throw new InvalidDataException(
        "Expecting decompressed chunk content to be smaller than 1 Mb");
    }
    return (int)count;
  }

  /// <summary>
  /// Unwrap a decrypted chunk into the given pre-allocated buffer
  /// </summary>
  /// <param name="decrypted">
  /// The chunk to unwrap / decompress
  /// </param>
  /// <param name="unwrapped">
  /// The output buffer for the unwrapped content. Its size should
  /// have been calculated with <see cref="UnwrappedSize(ReadOnlySpan{byte})"/>
  /// before.
  /// </param>
  public static void UnwrapChunk(
    ReadOnlySpan<byte> decrypted, Span<byte> unwrapped)
  {
    if(decrypted.Length == 0)
    {
      if(unwrapped.Length == 0)
      {
        return;
      }
      else
      {
        throw new ArgumentException(
          "Expecting output buffer to have length 0");
      }
    }
    var reader = new SpanReader();
    reader.ReadVarInt(decrypted, out var count);
    if(count == 0)
    {
      // uncompressed content
      var size = reader.BytesLeft(decrypted);
      if(size != unwrapped.Length)
      {
        throw new ArgumentException(
          "Incorrect return buffer size");
      }
      reader.ReadSpan(decrypted, unwrapped);
      return;
    }
    // decompress
    var sourceSize = reader.BytesLeft(decrypted);
    if(count != unwrapped.Length)
    {
      throw new ArgumentException(
        "Incorrect return buffer size");
    }
    reader.ReadSlice(decrypted, sourceSize, out var source);
    if(!BrotliDecoder.TryDecompress(source, unwrapped, out var bytesWritten))
    {
      throw new InvalidDataException(
        "Decompression failed");
    }
    if(bytesWritten != unwrapped.Length)
    {
      throw new InvalidDataException(
        "Decompression failed (incorrect size)");
    }
  }

  /// <summary>
  /// Unwrap a decrypted chunk into a newly allocated buffer
  /// </summary>
  /// <param name="decrypted">
  /// The chunk to unwrap / decompress
  /// </param>
  /// <returns>
  /// A new <see cref="CryptoBuffer{T}"/> containing the decompressed or otherwise
  /// unwrapped content
  /// </returns>
  public static CryptoBuffer<byte> UnwrapChunk(ReadOnlySpan<byte> decrypted)
  {
    var unwrappedSize = UnwrappedSize(decrypted);
    var buffer = new CryptoBuffer<byte>(unwrappedSize);
    try
    {
      UnwrapChunk(decrypted, buffer.Span);
      return buffer;
    }
    catch(Exception)
    {
      buffer.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Wrap uncompressed and unencrypted chunk content in a package
  /// ready for encryption
  /// </summary>
  /// <param name="bytes">
  /// The pristine chunk content to compress and wrap
  /// </param>
  /// <param name="compress">
  /// True to try compressing
  /// </param>
  /// <returns>
  /// A newly allocated CryptoBuffer containing the wrapped (but
  /// still unencrypted) chunk data.
  /// </returns>
  public static CryptoBuffer<byte> WrapChunk(
    ReadOnlySpan<byte> bytes, bool compress)
  {
    if(bytes.Length == 0)
    {
      // format 1: an empty buffer
      return new CryptoBuffer<byte>(0);
    }
    if(!compress)
    {
      // format 2: uncompressed
      var buffer = new CryptoBuffer<byte>(bytes.Length + 1);
      buffer.Span[0] = 0;
      bytes.CopyTo(buffer.Span[1..]);
      return buffer;
    }
    // format 3: compressed
    // We don't know in advance how large the compressed content will
    // be, but we estimate "no larger than the input". If it fails we should
    // use format 2 instead
    var uncompressedSize = bytes.Length;
    var maxSize = uncompressedSize; // BrotliEncoder.GetMaxCompressedLength(bytes.Length)+42;
    using(var zap = new ZapBuffer<byte>(maxSize))
    {
      zap.Resize(zap.Capacity);
      if(BrotliEncoder.TryCompress(bytes, zap.All, out var compressedSize, 11, 23)
        && compressedSize < bytes.Length)
      {
        var compressed = zap.All[..compressedSize];
        Span<byte> varintBuffer = stackalloc byte[8];
        var writer = new SpanWriter();
        writer.WriteVarInt(varintBuffer, uncompressedSize, out var varIntSize);
        var buffer = new CryptoBuffer<byte>(compressedSize + varIntSize);
        var writer2 = new SpanWriter();
        writer2
          .WriteSpan(buffer.Span, writer.WrittenBytes(varintBuffer))
          .WriteSpan(buffer.Span, compressed)
          .CheckFull(buffer.Span);
        return buffer;
      }
      else
      {
        // oops! compressing would increase the size
        return WrapChunk(bytes, false);
      }
    }
  }

}

/// <summary>
/// Typed content encoder / decoder
/// </summary>
/// <typeparam name="T">
/// The content model class
/// </typeparam>
public abstract class ContentAdapter<T>: ContentAdapter
  where T : ContentBase
{
  /// <summary>
  /// Instantiate an adapter
  /// </summary>
  /// <param name="kind"></param>
  protected ContentAdapter(
    ChunkKind kind)
    : base(kind)
  {
  }

  /// <summary>
  /// Decode the decrypted content to an instance of this
  /// decoder's associated content model type.
  /// </summary>
  /// <param name="decryptedContent">
  /// The decrypted chunk content
  /// </param>
  protected internal abstract T DecodeTyped(
    ReadOnlySpan<byte> decryptedContent);

  /// <summary>
  /// Serialize the typed model to a byte buffer
  /// </summary>
  protected internal abstract CryptoBuffer<byte> EncodeTyped(
    T model);

  /// <summary>
  /// Implements <see cref="ContentAdapter.Decode(ReadOnlySpan{byte})"/> by
  /// redirecting to <see cref="DecodeTyped(ReadOnlySpan{byte})"/>
  /// </summary>
  /// <param name="decryptedContent">
  /// The decrypted chunk content
  /// </param>
  /// <returns></returns>
  sealed protected internal override ContentBase Decode(
    ReadOnlySpan<byte> decryptedContent)
  {
    return DecodeTyped(decryptedContent);
  }

  /// <summary>
  /// Implements <see cref="ContentAdapter.Encode(ContentBase)"/> by
  /// redirecting to <see cref="EncodeTyped(T)"/>
  /// </summary>
  sealed protected internal override CryptoBuffer<byte> Encode(
    ContentBase model)
  {
    if(model is T t)
    {
      return EncodeTyped(t);
    }
    else
    {
      throw new InvalidOperationException(
        "Unexpected content model type");
    }
  }

}
