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
/// An entry block that is not recognized by the parser,
/// but can be re-emitted as-is later on.
/// </summary>
public class UnrecognizedBlock: EntryBlock
{
  private readonly byte[] _content;

  /// <summary>
  /// Create a new UnrecognizedBlock
  /// </summary>
  public UnrecognizedBlock(
    ContentSlice slice)
  {
    _content = slice.Content.ToArray();
  }

  /// <inheritdoc />
  public override void AppendAsChild(
    SegmentBuilder builder)
  {
    builder.AppendRaw(_content);
  }
}
