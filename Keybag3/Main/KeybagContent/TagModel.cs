/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Lcl.KeyBag3.Model.Contents;
using Lcl.KeyBag3.Model.Tags;
using System.Windows;

namespace Keybag3.Main.KeybagContent;

public class TagModel
{
  private TagModel(string tag, TagClass? cls)
  {
    Tag = tag;
    ContextTag = ContextTag.TryParse(tag);
    if(tag.StartsWith('?'))
    {
      IsHidden = true;
      tag = tag[1..];
    }
    PureTag = tag;
    Key = EntryTag.TagKey(tag);
    HasValue = Tag.Contains('=');
    Class = cls ?? CalculateClass();
    IsVirtual = cls.HasValue;
  }

  public static TagModel? TryFrom(string tag, TagClass? cls = null)
  {
    return EntryTag.IsValidTag(tag) ? new TagModel(tag, cls) : null;
  }

  public static TagModel From(string tag, TagClass? cls = null)
  {
    return TryFrom(tag, cls) ?? throw new ArgumentException("Invalid tag", nameof(tag));
  }

  /// <summary>
  /// The full tag string
  /// </summary>
  public string Tag { get; }

  /// <summary>
  /// The tag with the leading '?' removed, if it was present
  /// </summary>
  public string PureTag { get; }

  /// <summary>
  /// The key part of the tag (<see cref="PureTag"/> up to the first '=' character)
  /// </summary>
  public string Key { get; }

  /// <summary>
  /// True if the tag contains any '=' characters. Does not check for
  /// well-formedness, so <see cref="CtValue"/> may still be null.
  /// </summary>
  public bool HasValue { get; }

  /// <summary>
  /// The context tag if the tag is a valid ContextTag
  /// </summary>
  public ContextTag? ContextTag { get; }

  /// <summary>
  /// True if the tag is hidden (full string starts with '?')
  /// </summary>
  public bool IsHidden { get; }

  public bool IsWellFormed { get => ContextTag!=null; }

  public bool IsVirtual { get; }

  public FontStyle FontStyle { get => IsVirtual ? FontStyles.Italic : FontStyles.Normal; }

  /// <summary>
  /// True if this tag is well-formed and has the specified name
  /// (without any context condition)
  /// </summary>
  public bool HasName(string name) => IsWellFormed && String.Equals(
    ContextTag!.Name, name, StringComparison.InvariantCultureIgnoreCase);

  public bool HasContextName(string name, string context = "")
  {
    return
      ContextTag != null &&
      String.Equals(ContextTag.Name, name, StringComparison.InvariantCultureIgnoreCase) &&
      String.Equals(ContextTag.Context, context, StringComparison.InvariantCultureIgnoreCase);
  }

  public string? CtName { get => ContextTag?.Name; }

  public string? CtContext { get => ContextTag?.Context; }

  public string? CtField { get => ContextTag?.Field; }

  public string? CtValue { get => ContextTag?.Value; }

  /// <summary>
  /// If this tag is a section tag: the section name.
  /// null otherwise.
  /// </summary>
  public string? Section {
    get => HasContextName("section", String.Empty) ? CtField : null;
  }

  public TagClass Class { get; private set; }

  public TagClass CalculateClass()
  {
    if(HasContextName("section", String.Empty))
    {
      return TagClass.Section;
    }
    if(IsHidden)
    {
      return TagClass.Hidden;
    }
    if(HasValue)
    {
      Trace.TraceWarning($"Found value tag {Tag}");
      return TagClass.Valued;
    }
    return TagClass.Other;
  }
}
