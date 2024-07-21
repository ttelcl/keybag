/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Contents.Blocks;

/// <summary>
/// A plain text entry block (keybag 2 compatible)
/// </summary>
public class PlainEntryBlock: EntryBlock
{
  /// <summary>
  /// Create a new PlainEntryBlock
  /// </summary>
  public PlainEntryBlock(
    ContentSlice slice)
  {
    Tag = slice.Tag;
    Text = slice.AsString;
  }

  /// <summary>
  /// Create a new empty PlainEntryBlock
  /// </summary>
  public PlainEntryBlock(char tag = '=') 
  {
    Tag = tag;
    Text = String.Empty;
  }

  /// <summary>
  /// The tag character of this entry block
  /// (normally '=')
  /// </summary>
  public char Tag { get; }

  /// <summary>
  /// The text of this entry block
  /// </summary>
  public string Text { get; set; } = String.Empty;

  /// <inheritdoc />
  public override void AppendAsChild(
    SegmentBuilder builder)
  {
    builder.AppendLeaf(Text, Tag);
  }
}
