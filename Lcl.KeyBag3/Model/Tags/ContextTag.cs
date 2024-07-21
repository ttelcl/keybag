/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Tags;

/// <summary>
/// Interpretation of tags of a special form like
/// "name[context].field=value", such as
/// "?kb2[0A1B0FB7].entry=B6748E1D"
/// </summary>
public class ContextTag
{
  /// <summary>
  /// Create a new ContextTag
  /// </summary>
  /// <param name="hidden">
  /// True to indicate a "hidden" tag
  /// </param>
  /// <param name="name">
  /// The primary name of the tag
  /// </param>
  /// <param name="context">
  /// The context value, or an empty string to indicate there is no context
  /// </param>
  /// <param name="field">
  /// The field name, or an empty string to indicate there are no subfields
  /// </param>
  /// <param name="value">
  /// The tag value, or null to indicate the tag is name-only. Note that
  /// an empty string and null are not equivalent.
  /// </param>
  public ContextTag(
    bool hidden,
    string name,
    string context,
    string field,
    string? value)
  {
    Hidden = hidden;
    Name = name;
    Context = context;
    Field = field;
    Value = value;
  }

  /// <summary>
  /// Try to parse the tag as a ContextTag, returning null if
  /// <paramref name="tag"/> does not match <see cref="TagRegex"/>
  /// </summary>
  /// <param name="tag">
  /// The tag string to parse
  /// </param>
  /// <param name="exactName">
  /// If not null: the exact tag name expected (returning null if
  /// not matching)
  /// </param>
  /// <param name="requireValue">
  /// True to reject tags without a value. Unusually, an empty value
  /// still counts as "a value" for this purpose.
  /// </param>
  /// <param name="requireContext">
  /// True to reject tags without a context part. Ignored if
  /// <paramref name="exactContext"/> is not null.
  /// </param>
  /// <param name="exactContext">
  /// If not null: the exact value for the context expected
  /// (ignoring <paramref name="requireContext"/>)
  /// </param>
  /// <param name="requireField">
  /// True to reject tags without a field part
  /// </param>
  public static ContextTag? TryParse(
    string tag,
    string? exactName = null,
    bool requireValue = false,
    bool requireContext = false,
    string? exactContext = null,
    bool requireField = false)
  {
    var match = TagRegex.Match(tag);
    if(match.Success)
    {
      var hiddenGroup = match.Groups["hidden"];
      var hidden = hiddenGroup.Success;
      var nameGroup = match.Groups["name"];
      var name = nameGroup.Value;
      var contextGroup = match.Groups["context"];
      var context = contextGroup.Success ? contextGroup.Value : String.Empty;
      var fieldGroup = match.Groups["field"];
      var field = fieldGroup.Success ? fieldGroup.Value : String.Empty;
      var valueGroup = match.Groups["value"];
      var value = valueGroup.Success ? valueGroup.Value : null;
      if(requireValue && value == null)
      {
        return null;
      }
      if(exactName != null && exactName != name)
      {
        return null;
      }
      if(exactContext != null)
      {
        if(context != exactContext)
        {
          return null;
        }
      } else if(requireContext && String.IsNullOrEmpty(context))
      {
        return null;
      }
      if(requireField && String.IsNullOrEmpty(field))
      {
        return null;
      }
      return new ContextTag(
        hidden,
        name,
        context,
        field,
        value);
    }
    else
    {
      return null;
    }
  }

  /// <summary>
  /// Serialize this object back to a tag string
  /// </summary>
  public override string ToString()
  {
    var sb = new StringBuilder();
    if(Hidden)
    {
      sb.Append('?');
    }
    sb.Append(Name);
    if(!String.IsNullOrEmpty(Context))
    {
      sb.Append('[');
      sb.Append(Context);
      sb.Append(']');
    }
    if(!String.IsNullOrEmpty(Field))
    {
      sb.Append('.');
      sb.Append(Field);
    }
    if(Value != null)
    {
      // Value == "" is different from null!
      sb.Append('=');
      sb.Append(Value);
    }
    return sb.ToString();
  }

  /// <summary>
  /// The tag's main name
  /// </summary>
  public string Name { get; private init; }

  /// <summary>
  /// The tag's context string (empty string for contextless tags)
  /// </summary>
  public string Context { get; private init; }

  /// <summary>
  /// The field in the tag group (Empty string for fieldless
  /// tags)
  /// </summary>
  public string Field { get; private init; }

  /// <summary>
  /// The flag indicating that this tag should be hidden in user
  /// interfaces (and be serialized with a leading "?" character)
  /// </summary>
  public bool Hidden { get; set; }

  /// <summary>
  /// The value of the tag
  /// </summary>
  public string? Value { get; set; }

  /// <summary>
  /// True if there is a value part (even if empty)
  /// </summary>
  public bool HasValue { get => Value != null; }

  /// <summary>
  /// True if there is a context part
  /// </summary>
  public bool HasContext { get => !String.IsNullOrEmpty(Context); }

  /// <summary>
  /// Return true if the context is the given string
  /// </summary>
  public bool ContextIs(string context)
  {
    return context == Context;
  }

  /// <summary>
  /// True if there is a field name part
  /// </summary>
  public bool HasField { get => !String.IsNullOrEmpty(Field); }

  /// <summary>
  /// The regular expression for breaking down tag expressions
  /// </summary>
  public static Regex TagRegex { get; } =
    new(
      @"^
(?<hidden>\?)?
(?<name>[a-z][a-z0-9]*)
(?:\[(?<context>[^]=]*)\])?
(?:\.(?<field>[a-z][a-z0-9]*))?
(?:=(?<value>.*))?
$",
      RegexOptions.IgnoreCase | RegexOptions.Compiled
      | RegexOptions.IgnorePatternWhitespace);
}