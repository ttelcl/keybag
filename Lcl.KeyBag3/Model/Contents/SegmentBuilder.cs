/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Helper class for creating a segment in a <see cref="ContentBuilder"/>.
/// See also <see cref="ContentModel"/>
/// </summary>
public class SegmentBuilder: IDisposable
{
  /// <summary>
  /// Create a new SegmentBuilder
  /// </summary>
  /// <param name="owner">
  /// The owning <see cref="ContentBuilder"/>
  /// </param>
  /// <param name="parent">
  /// The parent <see cref="SegmentBuilder"/>, if there is any
  /// </param>
  /// <param name="tag">
  /// The tag character, satisfying <see cref="ContentModel.IsValidTag(char)"/>
  /// Note that passing <see cref="ContentModel.LeafTag"/> has a special meaning.
  /// </param>
  /// <param name="separator">
  /// The separator character, normally satisfying
  /// <see cref="ContentModel.IsValidSeparator(char)"/>.
  /// The special value <see cref="ContentModel.LeafSeparator"/> indicates that
  /// the segment is a leaf (even if <paramref name="tag"/> is not
  /// <see cref="ContentModel.LeafTag"/>).
  /// </param>
  public SegmentBuilder(
    ContentBuilder owner,
    SegmentBuilder? parent,
    char tag,
    char separator)
  {
    Owner = owner;
    Parent = parent;
    Tag = tag;
    Separator = separator;
    IsLeaf = Tag == ContentModel.LeafTag || Separator == ContentModel.LeafSeparator;
    if(!ContentModel.IsValidTag(Tag))
    {
      throw new InvalidOperationException(
        "Invalid tag character");
    }
    if(IsLeaf)
    {
      if(Separator != ContentModel.LeafSeparator)
      {
        throw new ArgumentException(
          "Leaf segments should pass Ascii.NUL as separator");
      }
    }
    else
    {
      if(!ContentModel.IsValidSeparator(Separator))
      {
        throw new ArgumentException(
          "Invalid separator character");
      }
    }
    if(Parent != null)
    {
      if(Parent.ActiveChild != null)
      {
        throw new InvalidOperationException(
          "Cannot attach a new active child segment to a parent that still has another active child");
      }
      Parent.ActiveChild = this;
    }
    AppendAsciiCharacter(Tag);
  }

  /// <summary>
  /// The tag character
  /// </summary>
  public char Tag { get; }

  /// <summary>
  /// The separator character, or <see cref="Ascii.NUL"/> for leaf segments
  /// </summary>
  public char Separator { get; }

  /// <summary>
  /// True for a leaf segment, where <see cref="Tag"/> is <see cref="ContentModel.LeafTag"/>.
  /// </summary>
  public bool IsLeaf { get; }

  /// <summary>
  /// The number of child segments created so far
  /// </summary>
  public int ChildCount { get; private set; }

  /// <summary>
  /// The parent segment builder (if there is one)
  /// </summary>
  public SegmentBuilder? Parent { get; }

  /// <summary>
  /// The <see cref="ContentBuilder"/> owning this <see cref="SegmentBuilder"/>
  /// </summary>
  public ContentBuilder Owner { get; }

  /// <summary>
  /// The currently active child segment builder. While this is not null
  /// no other new children can be added.
  /// </summary>
  public SegmentBuilder? ActiveChild { get; private set; }

  /// <summary>
  /// True after disposal
  /// </summary>
  public bool Disposed { get; private set; }

  /// <summary>
  /// Append a leaf child
  /// </summary>
  public void AppendLeaf(string text, char tag = ContentModel.LeafTag)
  {
    StartChild();
    // Instead of creating a child builder, write the content directly
    AppendAsciiCharacter(tag);
    AppendText(text);
  }

  /// <summary>
  /// Append a preformatted child slice (which should be valid UTF8
  /// content). This is useful for copying content from one slice to
  /// another without understanding the content.
  /// </summary>
  /// <param name="sliceContent">
  /// The preformatted child slice to append
  /// </param>
  public void AppendRaw(ReadOnlySpan<byte> sliceContent)
  {
    StartChild();
    Owner.AppendBytes(sliceContent);
  }

  /// <summary>
  /// Append an empty child (with no tag or content).
  /// This may be useful if the client model assigns meaning to child
  /// segments by their index.
  /// </summary>
  public void AppendEmpty()
  {
    StartChild();
  }

  /// <summary>
  /// Start building a new child segment
  /// </summary>
  /// <param name="tag">
  /// The child's tag
  /// </param>
  /// <param name="separator">
  /// The child's separator
  /// </param>
  /// <returns>
  /// A new child <see cref="SegmentBuilder"/>, which now is the 
  /// <see cref="ActiveChild"/> of this builder.
  /// </returns>
  public SegmentBuilder StartChildSegment(char tag, char separator)
  {
    StartChild();
    return new SegmentBuilder(Owner, this, tag, separator);
  }

  internal void AppendText(string text)
  {
    ObjectDisposedException.ThrowIf(Disposed, this);
    if(ActiveChild != null)
    {
      throw new InvalidOperationException(
        "Cannot append content while another child is still active");
    }
    Owner.AppendText(text);
  }

  internal void AppendAsciiCharacter(char c)
  {
    ObjectDisposedException.ThrowIf(Disposed, this);
    if(ActiveChild != null)
    {
      throw new InvalidOperationException(
        "Cannot append content while another child is still active");
    }
    Owner.AppendAsciiCharacter(c);
  }

  /// <summary>
  /// First step of creating a new child (normal, leaf or empty). This
  /// checks locking, updates <see cref="ChildCount"/> and writes
  /// the separator
  /// </summary>
  private void StartChild()
  {
    ObjectDisposedException.ThrowIf(Disposed, this);
    if(ActiveChild != null)
    {
      throw new InvalidOperationException(
        "Cannot add a new child segment while another is still active");
    }
    if(ChildCount > 0)
    {
      if(IsLeaf)
      {
        throw new InvalidOperationException(
          "Cannot add more than one child to a leaf node");
      }
      AppendAsciiCharacter(Separator);
    }
    ChildCount++;
  }

  /// <summary>
  /// Clean up by marking this instance as disposed and detaching
  /// it from its parent
  /// </summary>
  public void Dispose()
  {
    if(!Disposed)
    {
      if(ActiveChild != null)
      {
        throw new InvalidOperationException(
          "Attempt to dispose a parent segment builder while its child is still active");
      }
      Disposed = true;
      if(Parent != null && Parent.ActiveChild == this)
      {
        Parent.ActiveChild = null;
      }
      GC.SuppressFinalize(this);
    }
  }
}
