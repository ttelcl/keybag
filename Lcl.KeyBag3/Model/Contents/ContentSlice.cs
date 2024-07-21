/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Wraps a <see cref="ReadOnlyMemory{T}"/> of bytes that contains
/// UTF8 encoded text that uses ASCII control characters to structure
/// the content. The aim is to access the structure parts without
/// having to convert the entire buffer into one string.
/// See <see cref="ContentModel"/> for a format description.
/// </summary>
public readonly struct ContentSlice
{

  /// <summary>
  /// Create a new <see cref="ContentSlice"/> wrapping the given content.
  /// This type does NOT copy the content; <paramref name="content"/> must
  /// stay valid throughout the lifetime of this <see cref="ContentSlice"/>. 
  /// It reads the tag from the content if present, but is not aware of the
  /// separator.
  /// </summary>
  public ContentSlice(ReadOnlyMemory<byte> content)
  {
    Content = content;
    if(Content.Length > 0)
    {
      var tag = (char)Content.Span[0];
      if(ContentModel.IsValidTag(tag))
      {
        Tag = tag;
      }
      else
      {
        Tag = Ascii.NUL;
      }
    }
    else
    {
      Tag = Ascii.NUL;
    }
  }

  /// <summary>
  /// Create a new ContentReadBuffer wrapping the given content
  /// of the buffer.
  /// This type does NOT copy the content.
  /// </summary>
  public ContentSlice(IHasReadOnlyMemory<byte> buffer)
    : this(buffer.ReadOnlyMemory)
  {
  }

  /// <summary>
  /// The full content
  /// </summary>
  public ReadOnlyMemory<byte> Content { get; }

  /// <summary>
  /// Return a segment of the full content as a new <see cref="ContentSlice"/>.
  /// </summary>
  public ContentSlice this[Range range] {
    get {
      return new ContentSlice(Content[range]);
    }
  }

  /// <summary>
  /// Return the tag character: the first byte of the buffer
  /// if it satisfies <see cref="ContentModel.IsValidTag(char)"/>, or
  /// <see cref="Ascii.NUL"/> if the slice is empty or the first byte is
  /// not valid.
  /// </summary>
  public char Tag { get; }

  /// <summary>
  /// True if the tag character is valid
  /// </summary>
  public bool HasTag { get => ContentModel.IsValidTag(Tag); }

  /// <summary>
  /// True if this slice is empty
  /// </summary>
  public bool IsEmpty { get => Content.Length == 0; }

  /// <summary>
  /// Return a list of one or more sub-slices that are separated
  /// by the given <paramref name="separator"/> character. The separators
  /// are not included in the returned values. Returns an empty list if
  /// this slice is empty (and therefore has no tag byte)
  /// </summary>
  /// <param name="separator">
  /// The character to use as separator, as defined in the <see cref="ContentModel"/>
  /// definition, satisfying <see cref="ContentModel.IsValidSeparator(char)"/>.
  /// </param>
  /// <returns>
  /// The list of child slices that were separated by the separator character
  /// </returns>
  public IReadOnlyList<ContentSlice> Split(char separator)
  {
    if(!ContentModel.IsValidSeparator(separator))
    {
      throw new ArgumentOutOfRangeException(
        nameof(separator), "The separator must be an ASCII control character");
    }
    var list = new List<ContentSlice>();
    if(Content.Length == 0)
    {
      return list;
    }
    var bsep = (byte)separator;
    var content = HasTag ? Content[1..] : Content;
    while(true)
    {
      var index = content.Span.IndexOf(bsep);
      if(index < 0)
      {
        list.Add(new ContentSlice(content));
        break;
      }
      list.Add(new ContentSlice(content[..index]));
      content = content[(index+1)..];
    }
    return list;
  }

  /// <summary>
  /// Convert the content of this slice minus the tag character to a string
  /// </summary>
  public string AsString {
    get => HasTag 
      ? Encoding.UTF8.GetString(Content.Span[1..])
      : Encoding.UTF8.GetString(Content.Span);
  }

  /// <summary>
  /// Convert the full content of this slice (tag byte included) to a string
  /// </summary>
  public override string ToString()
  {
    return Encoding.UTF8.GetString(Content.Span);
  }
}
