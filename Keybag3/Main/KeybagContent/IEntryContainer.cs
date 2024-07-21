/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Model.TreeMath;

namespace Keybag3.Main.KeybagContent;

/// <summary>
/// A host for a list of <see cref="EntryViewModel"/>
/// instances (i.e. an object that can have EntryViewmodel 
/// instances as "children")
/// </summary>
public interface IEntryContainer
{
  /// <summary>
  /// The space defining all known entries
  /// </summary>
  ChunkSpace<EntryViewModel> EntrySpace { get; }

  KeybagViewModel HostKeybag { get; }

  /// <summary>
  /// Return the set of child chunk IDs for this container
  /// (in no particular order).
  /// </summary>
  IReadOnlySet<ChunkId> ChildSet();

  /// <summary>
  /// The sorted list of visible children, as built and
  /// frozen by <see cref="RebuildList(bool)"/>. The implementation
  /// should notify changes to this list.
  /// </summary>
  IReadOnlyList<EntryViewModel> ChildList { get; }
  
  /// <summary>
  /// Rebuild <see cref="ChildList"/>
  /// </summary>
  /// <param name="recursive">
  /// If true, recurse into child nodes
  /// </param>
  void RebuildList(bool recursive);
}

