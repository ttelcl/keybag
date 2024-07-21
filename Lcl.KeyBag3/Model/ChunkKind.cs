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
/// Type codes of nodes, used in the serialized form
/// </summary>
public enum ChunkKind: byte
{

  /// <summary>
  /// A normal entry node (KeyBag2 style). These can have other Entry nodes
  /// or a file node as parent
  /// </summary>
  Entry = 2,

  /// <summary>
  /// The imaginary file node, the logical parent of all "top level"
  /// entry nodes, having the file ID as its node id. Does not have a parent.
  /// </summary>
  File = 3,

  /// <summary>
  /// A subnode storing a "record". Parent is an Entry node.
  /// NOT YET SUPPORTED
  /// </summary>
  Record = 4,

  /// <summary>
  /// An attachment link subnode. Parent is an Entry node.
  /// NOT YET SUPPORTED
  /// </summary>
  Attachment = 5,

  /// <summary>
  /// A seal chunk. Parent is the file chunk. Provides a checksum
  /// on all preceding chunks in the file. The final chunk in the file
  /// must be of this kind.
  /// </summary>
  Seal = 6,
}
