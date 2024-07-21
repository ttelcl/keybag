/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model.TreeMath;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// A general model for a chunk in a keybag, applicable
/// to both raw and decoded chunks
/// </summary>
public interface IKeybagChunk
{
  /// <summary>
  /// The type of chunk
  /// </summary>
  ChunkKind Kind { get; }

  /// <summary>
  /// The flags of the chunk
  /// </summary>
  ChunkFlags Flags { get; }

  /// <summary>
  /// The chunk id (a 6 byte value representing a unique
  /// creation time stamp at millisecond precision)
  /// </summary>
  ChunkId NodeId { get; }

  /// <summary>
  /// The edit id (a 6 byte value representing a unique
  /// last modified time stamp at millisecond precision)
  /// </summary>
  ChunkId EditId { get; }

  /// <summary>
  /// The parent id. Either the chunk id of the parent node
  /// or 0 to indicate that there is no parent
  /// </summary>
  ChunkId ParentId { get; }

  /// <summary>
  /// The file ID. This is not serialized / deserialized directly and
  /// therefore typically is passed to the implementation's
  /// constructor
  /// </summary>
  ChunkId FileId { get; }
}

/// <summary>
/// Extension methods on <see cref="IKeybagChunk"/>
/// </summary>
public static class KeybagChunkExtensions
{
  /// <summary>
  /// True if <paramref name="chunk"/>'s <see cref="IKeybagChunk.Flags"/> include
  /// <see cref="ChunkFlags.Erased"/>.
  /// </summary>
  public static bool IsErased(this IKeybagChunk chunk)
    => (chunk.Flags & ChunkFlags.Erased) != ChunkFlags.None;

  /// <summary>
  /// True if <paramref name="chunk"/>'s <see cref="IKeybagChunk.Flags"/> include
  /// <see cref="ChunkFlags.Archived"/>.
  /// </summary>
  public static bool IsArchived(this IKeybagChunk chunk)
    => (chunk.Flags & ChunkFlags.Archived) != ChunkFlags.None;

  /// <summary>
  /// True if <paramref name="chunk"/>'s <see cref="IKeybagChunk.Flags"/> include
  /// <see cref="ChunkFlags.Sealed"/>.
  /// </summary>
  public static bool IsSealed(this IKeybagChunk chunk)
    => (chunk.Flags & ChunkFlags.Sealed) != ChunkFlags.None;

  /// <summary>
  /// True if <paramref name="chunk"/>'s <see cref="IKeybagChunk.Flags"/> include
  /// <see cref="ChunkFlags.Erased"/> or <see cref="ChunkFlags.Archived"/>.
  /// </summary>
  public static bool IsRemoved(this IKeybagChunk chunk)
    => (chunk.Flags & ChunkFlags.Removed) != ChunkFlags.None;

  /// <summary>
  /// Get an "attached property" value associated with the chunk
  /// </summary>
  /// <typeparam name="TValue">
  /// The value type
  /// </typeparam>
  /// <param name="chunk">
  /// The chunk to get the value for
  /// </param>
  /// <param name="mapping">
  /// The mapping storing the attached property values
  /// </param>
  /// <returns>
  /// The value associated with the chunk in <paramref name="mapping"/>
  /// (<see cref="ChunkMapping{TValue}.DefaultValue"/> if
  /// not yet defined)
  /// </returns>
  public static TValue GetValue<TValue>(
    this IKeybagChunk chunk,
    ChunkMapping<TValue> mapping)
  {
    return mapping[chunk.NodeId];
  }

  /// <summary>
  /// Set an "attached property" value associated with the chunk
  /// </summary>
  /// <typeparam name="TValue">
  /// The value type
  /// </typeparam>
  /// <param name="chunk">
  /// The chunk to set the value for
  /// </param>
  /// <param name="mapping">
  /// The mapping storing the attached property values
  /// </param>
  /// <param name="value">
  /// The value to set
  /// </param>
  public static void SetValue<TValue>(
    this IKeybagChunk chunk,
    ChunkMapping<TValue> mapping,
    TValue value)
  {
    mapping[chunk.NodeId] = value;
  }

}

