/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model.Contents;
using Lcl.KeyBag3.Model.Contents.Blocks;

using Keybag3.WpfUtilities;

namespace Keybag3.Main.KeybagContent.EntryBlocks;

public abstract class EntryBlockViewModel: ViewModelBase
{
  protected EntryBlockViewModel(
    EntryBlock baseBlock,
    EntryViewModel owner)
  {
    BaseBlock = baseBlock;
    Owner = owner;
  }

  public EntryViewModel Owner { get; }

  public EntryBlock BaseBlock { get; }

  public EntryContent Content => Owner.Content;

  public static EntryBlockViewModel FromRawBlock(
    EntryBlock rawBlock, EntryViewModel entryViewModel)
  {
    return rawBlock switch {
      PlainEntryBlock plainBlock =>
        new PlainBlockViewModel(plainBlock, entryViewModel),
      _ => new UnrecognizedBlockViewModel(rawBlock, entryViewModel),
    };
  }

  protected void TagAsModified()
  {
    Content.Modified = true;
    Owner.CheckNeedsPersisting();
    Owner.CheckChanged();
  }

}
