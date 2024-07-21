/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Tags;

/// <summary>
/// A collection of <see cref="ContextFieldGroup"/> instances
/// indexable by name and context
/// </summary>
public class ContextTagMap
{
  private readonly Dictionary<(string name, string context), ContextFieldGroup> _groups;

  /// <summary>
  /// Create a new ContextTagMap
  /// </summary>
  public ContextTagMap()
  {
    _groups = [];
  }

  /// <summary>
  /// Get or create the group for the given name-context pair
  /// </summary>
  public ContextFieldGroup this[string name, string context] {
    get {
      if(!_groups.TryGetValue((name, context), out var group))
      {
        group = new ContextFieldGroup(name, context);
        _groups[(name, context)] = group;
      }
      return group;
    }
  }

  /// <summary>
  /// Find the existing group for the given name-context pair
  /// (returning null if not found)
  /// </summary>
  public ContextFieldGroup? Find(string name, string context) { 
    return _groups.TryGetValue((name, context), out var group) ? group : null;
  }

  /// <summary>
  /// Put the given tag in the appropriate group. If the group
  /// is missing it is created if <paramref name="createGroup"/> is
  /// true, while nothing is put if false.
  /// </summary>
  /// <param name="tag">
  /// The tag to insert
  /// </param>
  /// <param name="createGroup">
  /// Determines the behaviour if the group did not exist yet
  /// </param>
  /// <returns>
  /// True if the tag was inserted, false if the group did not exist
  /// and <paramref name="createGroup"/> was false.
  /// </returns>
  public bool Put(ContextTag tag, bool createGroup = true)
  {
    if(createGroup)
    {
      var group = this[tag.Name, tag.Context];
      group.Put(tag);
      return true;
    }
    else
    {
      var group = Find(tag.Name, tag.Context);
      group?.Put(tag);
      return group != null;
    }
  }

}
