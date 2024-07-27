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
using Lcl.KeyBag3.Utilities;

using static System.Net.WebRequestMethods;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// A pseudo-chunk carrying only the fields required to implement
/// <see cref="IKeybagChunk"/>.
/// </summary>
public class KeybagChunkStub: IKeybagChunk
{
  /// <summary>
  /// Create a new KeybagChunkStub by copying values
  /// from the given template
  /// </summary>
  public KeybagChunkStub(
    IKeybagChunk template)
  {
    Kind = template.Kind;
    Flags = template.Flags;
    NodeId = template.NodeId;
    EditId = template.EditId;
    ParentId = template.ParentId;
    FileId = template.FileId;
  }

  /// <summary>
  /// Create a new KeybagChunkStub with the given values
  /// </summary>
  public KeybagChunkStub(
    ChunkKind kind,
    ChunkFlags flags,
    ChunkId nodeId,
    ChunkId editId,
    ChunkId parentId,
    ChunkId fileId)
  {
    Kind=kind;
    Flags=flags;
    NodeId=nodeId;
    EditId=editId;
    ParentId=parentId;
    FileId=fileId;
  }

  /// <inheritdoc/>
  public ChunkKind Kind { get; }

  /// <inheritdoc/>
  public ChunkFlags Flags { get; }

  /// <inheritdoc/>
  public ChunkId NodeId { get; }

  /// <inheritdoc/>
  public ChunkId EditId { get; }

  /// <inheritdoc/>
  public ChunkId ParentId { get; }

  /// <inheritdoc/>
  public ChunkId FileId { get; }

  /// <summary>
  /// Try read the next chunk from the given file, returning
  /// only a stub for the chunk, skipping the content. Returns null
  /// at EOF.
  /// </summary>
  public static KeybagChunkStub? TryReadFrom(
    Stream file,
    ChunkId fileId)
  {
    if(!StoredChunk.TrySkipChunkPrefix(file))
    {
      return null;
    }
    Span<byte> header = stackalloc byte[40];
    var reader = new SpanReader();
    file.ReadExactly(header);
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
    var contentLength = size - 40;
    if(contentLength > 0)
    {
      file.Position += contentLength;
    }
    return new KeybagChunkStub(
      role,
      flags,
      nodeId,
      new ChunkId(editId),
      new ChunkId(parentId, true),
      fileId);
  }

  // ---------------
}
