/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Tags;

/// <summary>
/// Tracks a set of <see cref="ContextTag"/> instances
/// with distinct values for their <see cref="ContextTag.Field"/>
/// value and have all the same <see cref="ContextTag.Context"/>
/// and <see cref="ContextTag.Name"/>.
/// </summary>
public class ContextFieldGroup
{
  private readonly Dictionary<string, ContextTag> _fields;

  /// <summary>
  /// Create a new ContextFieldGroup
  /// </summary>
  public ContextFieldGroup(
    string name,
    string context)
  {
    _fields = [];
    Name = name;
    Context = context;
  }

  /// <summary>
  /// Get the name shared by all tags in this group
  /// </summary>
  public string Name { get; private init; }

  /// <summary>
  /// Get the context shared by all tags in this group
  /// </summary>
  public string Context { get; private init; }

  /// <summary>
  /// Enumerates all tags in this group (each with a distinct
  /// <see cref="ContextTag.Field"/>)
  /// </summary>
  public IReadOnlyCollection<ContextTag> Tags { get => _fields.Values; }

  /// <summary>
  /// Get a tag object by field value, returning null if not found
  /// </summary>
  public ContextTag? this[string field] {
    get => _fields.TryGetValue(field, out var tag) ? tag : null;
  }

  /// <summary>
  /// Get the value of a field by field value, returning null if not found
  /// (or if the tag that was found has no value)
  /// </summary>
  public string? GetValue(string field)
  {
    return this[field]?.Value;
  }

  /// <summary>
  /// Insert the specified tag
  /// </summary>
  public void Put(ContextTag tag)
  {
    if(tag.Name != Name)
    {
      throw new ArgumentException(
        $"Incorrect tag name");
    }
    if(tag.Context != Context)
    {
      throw new ArgumentException(
        $"Incorrect tag context");
    }
    _fields[tag.Field] = tag;
  }

  /// <summary>
  /// Insert the specified tag if it matches this group's tag and context
  /// </summary>
  public bool TryPut(ContextTag tag)
  {
    if(tag.Name == Name && tag.Context == Context)
    {
      _fields[tag.Field] = tag;
      return true;
    }
    else
    {
      return false;
    }
  }

  /// <summary>
  /// Create a new tag and insert it
  /// </summary>
  public void PutValue(string field, string? value, bool hidden)
  {
    Put(new ContextTag(hidden, Name, Context, field, value));
  }

  /// <summary>
  /// Remove a tag by field name
  /// </summary>
  public bool Remove(string field)
  {
    return _fields.Remove(field);
  }
}
