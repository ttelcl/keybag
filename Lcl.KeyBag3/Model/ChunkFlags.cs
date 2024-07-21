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
/// Flag bits defining the status of a node
/// </summary>
[Flags]
public enum ChunkFlags: byte
{
  /// <summary>
  /// No flags
  /// </summary>
  None = 0x00,

  /// <summary>
  /// The node content is no longer valid and should normally
  /// not be shown. The content is still present, and a node
  /// can be un-archived.
  /// </summary>
  Archived = 0x01,

  /// <summary>
  /// The node content is mostly no longer be present and cannot
  /// be restored. The node is not expected to ever be shown again.
  /// If the node is an entry node its name is still preserved for
  /// display in edge cases like synchronization importing non-deleted
  /// descendents.
  /// </summary>
  Erased = 0x02,

  /// <summary>
  /// Indicates that an external application assumes ownership of the
  /// entry. The entry should not be modified by the user, unless explicitly
  /// "unsealed" first.
  /// </summary>
  Sealed = 0x04,

  /// <summary>
  /// Indicates a chunk that is part of the serialization infrastructure
  /// and should not be exposed to the GUI app (let alone the user).
  /// This is currently used for seal chunks.
  /// </summary>
  Infrastructure = 0x08,

  /// <summary>
  /// Combines <see cref="Erased"/> and <see cref="Archived"/>: the
  /// node should normally be hidden in the UI.
  /// </summary>
  Removed = Erased | Archived,
}
