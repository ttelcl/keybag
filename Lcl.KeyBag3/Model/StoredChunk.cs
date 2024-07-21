/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// A node modeled based in how it is stored in a KeyBag3 file. This model
/// is immutable and carries its encrypted content as an opaque blob.
/// </summary>
public class StoredChunk: IKeybagChunk
{
  private readonly byte[] _content;

  /// <summary>
  /// Create a new StoredNode. Invoked via either <see cref="ReadFromFile"/>
  /// or <see cref="ChunkCryptor.EncryptContent"/>.
  /// </summary>
  internal StoredChunk(
    ChunkKind kind,
    ChunkFlags flags,
    ChunkId nodeId,
    ChunkId editId,
    ChunkId parentId,
    ChunkId fileId,
    UInt128 authCode,
    ReadOnlySpan<byte> content
    )
  {
    Kind = kind;
    Flags = flags;
    NodeId = nodeId;
    EditId = editId;
    ParentId = parentId;
    FileId = fileId;
    AuthCode = authCode;
    if(kind == ChunkKind.File)
    {
      if(fileId.Value != nodeId.Value)
      {
        throw new ArgumentException(
          "Expecting fileId == nodeId for file header chunk");
      }
    }
    _content = content.ToArray();
    if(!ChunkId.LooksValid(nodeId.Value))
    {
      throw new ArgumentOutOfRangeException(nameof(nodeId), $"Invalid node ID 0x{nodeId:08X}");
    }
    if(!ChunkId.LooksValid(editId.Value))
    {
      throw new ArgumentOutOfRangeException(nameof(editId), $"Invalid node edit ID 0x{editId:08X}");
    }
    if(!ChunkId.LooksValid(parentId.Value, true))
    {
      throw new ArgumentOutOfRangeException(nameof(parentId), $"Invalid node parent ID 0x{parentId:08X}");
    }
  }

  /// <summary>
  /// The type / kind / role of the node. This affects which node kind(s)
  /// are valid for parent and child nodes.
  /// </summary>
  public ChunkKind Kind { get; init; }

  /// <summary>
  /// Flags indicating the status of a node
  /// </summary>
  public ChunkFlags Flags { get; init; }

  /// <summary>
  /// The node ID. Only the lower 6 bytes are used. Represents the
  /// node's creation stamp in Unix Milliseconds style, as provided by
  /// <see cref="ChunkIds"/>.
  /// </summary>
  public ChunkId NodeId { get; init; }

  /// <summary>
  /// The node version. Only the lower 6 bytes are used. Represents the
  /// node's latest edit stamp in Unix Milliseconds style, as provided by
  /// <see cref="ChunkIds"/>. For a file header node this equals <see cref="NodeId"/>.
  /// </summary>
  public ChunkId EditId { get; init; }

  /// <summary>
  /// The node id of the logical parent node, or 0L for the file header node.
  /// </summary>
  public ChunkId ParentId { get; init; }

  /// <summary>
  /// The 16 byte AES-GCM authentication code for the encrypted content
  /// </summary>
  public UInt128 AuthCode { get; init; }

  /// <summary>
  /// A read-only view on the blob's (encrypted) content bytes
  /// </summary>
  public ReadOnlySpan<byte> Content { get => _content; }

  /// <summary>
  /// The ID of the file this chunk is part of
  /// </summary>
  public ChunkId FileId { get; }

  /// <summary>
  /// The size in bytes of this node including the 40 byte node header and the
  /// variable size content.
  /// </summary>
  public int Size { get => 40 + _content.Length; }

  /// <summary>
  /// The offset in the file where this was loaded from or saved to,
  /// or null if it wasn't loaded from a file.
  /// This property is primarily an annotation for diagnostic purposes.
  /// This being null indicates this <see cref="StoredChunk"/> has not been saved
  /// yet.
  /// </summary>
  public long? FileOffset { get; private set; }

  /// <summary>
  /// Create a clone of this StoredChunk, cloning all fields except for
  /// <see cref="FileOffset"/>. The latter is left at null, indicating that
  /// the clone has not been saved to a file yet.
  /// </summary>
  public StoredChunk Clone()
  {
    return new StoredChunk(
      Kind,
      Flags,
      NodeId,
      EditId,
      ParentId,
      FileId,
      AuthCode,
      _content);
  }

  /// <summary>
  /// Read the next node from the file. This method fails if the file is
  /// at EOF. The file is expected to be positioned at either the start of
  /// the size part (<paramref name="prereadsize"/> == 0), or just beyond it
  /// (<paramref name="prereadsize"/> != 0). The prefix is expected to have
  /// been skipped already.
  /// </summary>
  /// <param name="file">
  /// The stream to read
  /// </param>
  /// <param name="fileId">
  /// The file ID, ignored for a file header node
  /// </param>
  /// <param name="prereadsize">
  /// If not 0: the size part of the node, assumed to have been read in
  /// advance and there are 36 node header bytes remaining to be read.
  /// If 0: the size part is assumed not yet read, so there are 40 node
  /// header bytes remaining to be read.
  /// reamining
  /// </param>
  /// <returns>
  /// The raw <see cref="StoredChunk"/> that was loaded.
  /// </returns>
  /// <exception cref="InvalidDataException"></exception>
  public static StoredChunk ReadFromFile(
    Stream file,
    ChunkId fileId,
    int prereadsize = 0)
  {
    Span<byte> header = stackalloc byte[40];
    var offset = file.Position;
    var reader = new SpanReader();
    if(prereadsize < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(prereadsize),
        "Expecting 0 or a value >= 40");
    }
    if(prereadsize == 0)
    {
      file.ReadExactly(header);
    }
    else
    {
      file.ReadExactly(header[4..]);
      BinaryPrimitives.WriteInt32LittleEndian(header[..4], prereadsize);
    }
    reader
      .ReadI32(header, out var size)
      .ReadChunkId(header, out var nodeIdValue)
      .ReadChunkId(header, out var editId)
      .ReadUI128(header, out var auth)
      .ReadChunkId(header, out var parentId)
      .ReadByte(header, out var roleValue)
      .ReadByte(header, out var flagsValue)
      .CheckEmpty(header);
    if(size < 40 || size > 0x00100000) // cap node size at 1Mb-40bytes
    {
      throw new InvalidDataException($"Node size out of range: {size}");
    }
    var role = (ChunkKind)roleValue;
    if(!Enum.IsDefined<ChunkKind>(role))
    {
      throw new InvalidDataException(
        "Unrecognized node type");
    }
    var flags = (ChunkFlags)flagsValue;
    var nodeId = new ChunkId(nodeIdValue);
    if(role == ChunkKind.File)
    {
      fileId = nodeId;
    }
    var buffer = new byte[size-40];
    if(buffer.Length > 0)
    {
      file.ReadExactly(buffer);
    }
    return new StoredChunk(
      role,
      flags,
      nodeId,
      new ChunkId(editId),
      new ChunkId(parentId, true),
      fileId,
      auth,
      buffer) {
      FileOffset = offset
    };
  }

  /// <summary>
  /// Try to read the next chunk's prefix from the stream (and
  /// advance the stream beyond it). Returns true on success,
  /// false at EOF. Fails if there is a prefix but it is not
  /// valid.
  /// </summary>
  public static bool TrySkipChunkPrefix(Stream stream)
  {
    Span<byte> prefixBytes = stackalloc byte[4];
    if(stream.Read(prefixBytes) == 0)
    {
      return false;
    }
    var prefix = BinaryPrimitives.ReadInt32LittleEndian(prefixBytes);
    if(prefix != KeybagHeader.ChunkPrefix)
    {
      throw new InvalidDataException(
        $"keybag file error: expecting a chunk prefix");
    }
    return true;
  }

  /// <summary>
  /// Write this node to the stream
  /// </summary>
  /// <param name="file">
  /// The stream to write to
  /// </param>
  /// <param name="writePrefix">
  /// If true: include the 4 byte chunk prefix
  /// </param>
  public void WriteToFile(Stream file, bool writePrefix)
  {
    Span<byte> buffer = stackalloc byte[40 + (writePrefix ? 4 : 0)];
    var writer = new SpanWriter();
    if(writePrefix)
    {
      writer.WriteI32(buffer, KeybagHeader.ChunkPrefix);
    }
    writer
      .WriteI32(buffer, Size)
      .WriteChunkId(buffer, NodeId)
      .WriteChunkId(buffer, EditId)
      .WriteU128(buffer, AuthCode)
      .WriteChunkId(buffer, ParentId)
      .WriteByte(buffer, (byte)Kind)
      .WriteByte(buffer, (byte)Flags)
      .CheckFull(buffer);
    FileOffset = file.Position;
    file.Write(buffer);
    file.Write(Content);
  }
}
