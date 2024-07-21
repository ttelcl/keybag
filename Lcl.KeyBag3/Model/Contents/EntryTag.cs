/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Static functionality for entry tags
/// </summary>
public static class EntryTag
{
  /// <summary>
  /// Check if a string is a valid tag. A valid tag fits the following
  /// rules: (1) a leading '?' character is ignored, (2) the tag does
  /// not start with a '-' or '+' character, (3) the tag is not empty,
  /// (4) the tag does not contain any whitespace characters.
  /// </summary>
  /// <param name="tag">
  /// The string to test
  /// </param>
  /// <returns>
  /// True if the string is a valid tag
  /// </returns>
  public static bool IsValidTag(string tag)
  {
    if(tag.StartsWith('?'))
    {
      tag = tag[1..];
    }
    if(tag.StartsWith('-') || tag.StartsWith('+'))
    {
      return false;
    }
    return !String.IsNullOrEmpty(tag) && tag.All(c => !Char.IsWhiteSpace(c));
  }

  /// <summary>
  /// Check if the string is a valid tag, returning null on success
  /// or returning a string describing the error if the tag is invalid.
  /// </summary>
  public static string? DescribeInvalidTag(string tag)
  {
    var qmRemovedMessage = "";
    if(tag.StartsWith('?'))
    {
      qmRemovedMessage = " (after removing leading '?')";
      tag = tag[1..];
    }
    if(tag.StartsWith('-') || tag.StartsWith('+'))
    {
      return $"Tag cannot start with '-' or '+'{qmRemovedMessage}";
    }
    if(String.IsNullOrEmpty(tag))
    {
      return $"Tag cannot be empty{qmRemovedMessage}";
    }
    if(tag.Any(Char.IsWhiteSpace))
    {
      return "Tag cannot contain whitespace";
    }
    return null;
  }

  /// <summary>
  /// Return the "key" part of a keyed tag. The key is the part
  /// of the tag up to the first '=' character, but removing a leading
  /// '?' character if it is present. If there is no '=', the entire
  /// tag string is returned (minus the leading '?' if present).
  /// </summary>
  /// <param name="tag">
  /// The tag string to get the key for
  /// </param>
  public static string TagKey(string tag)
  {
    if(tag.StartsWith('?'))
    {
      tag = tag[1..];
    }
    var index = tag.IndexOf('=');
    return index > 0 ? tag[..index] : tag;
  }

}
