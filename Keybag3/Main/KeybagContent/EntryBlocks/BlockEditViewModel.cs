/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Keybag3.WpfUtilities;

using Lcl.KeyBag3.Model.Contents.Blocks;

namespace Keybag3.Main.KeybagContent.EntryBlocks;

/// <summary>
/// Base class and fallback for block editing
/// </summary>
public class BlockEditViewModel: ViewModelBase
{
  public BlockEditViewModel(
    EntryEditViewModel owner,
    EntryBlockViewModel? baseBlock)
  {
    Owner = owner;
    BaseBlock = baseBlock;
    BlockLabel = "Unrecognized Block";
  }

  public EntryEditViewModel Owner { get; }

  public EntryBlockViewModel? BaseBlock { get; }

  public virtual string BlockLabel { get; protected set; }

  public static BlockEditViewModel Create(
    EntryEditViewModel owner,
    EntryBlockViewModel? baseBlock)
  {
    return baseBlock switch {
      PlainBlockViewModel plainBlock
        => new PlainBlockEditViewModel(owner, plainBlock),
      _ => new BlockEditViewModel(owner, baseBlock),
    };
  }

  /// <summary>
  /// Accept the edit and return the new block / modified block /
  /// or null if the block is deleted. The returned block is the
  /// RAW block, not the view model; the view model will be recreated
  /// from this.
  /// The default implementation just returns the original.
  /// </summary>
  public virtual EntryBlock? AcceptEdit()
  {
    return BaseBlock?.BaseBlock;
  }

}
