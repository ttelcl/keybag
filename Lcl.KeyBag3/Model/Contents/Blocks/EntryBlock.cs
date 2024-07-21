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
/// Serializable block inside an entry (one block of the main content)
/// </summary>
public abstract class EntryBlock
{
  /// <summary>
  /// Create a new EntryBlock
  /// </summary>
  protected EntryBlock()
  {
    VolatileGuid = Guid.NewGuid();
  }

  /// <summary>
  /// An identifier that is unique for this block instance. It is
  /// stable while the entry is in memory, but is not saved to disk.
  /// </summary>
  public Guid VolatileGuid { get; }

  /// <summary>
  /// Append this entry block as a child to the given content builder
  /// </summary>
  public abstract void AppendAsChild(SegmentBuilder builder);

}
