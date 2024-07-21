/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Keybag3.WpfUtilities;

using Lcl.KeyBag3.Model.Contents.Blocks;

namespace Keybag3.Main.KeybagContent.EntryBlocks;

public class PlainBlockEditViewModel:
  BlockEditViewModel
{
  public PlainBlockEditViewModel(
    EntryEditViewModel owner,
    PlainBlockViewModel? block)
    : base(owner, block)
  {
    Block = block;
    _delete = false;
    _text = block?.Text ?? String.Empty;
  }

  public override string BlockLabel => "Plain Text Block";

  /// <summary>
  /// The block to edit or null if a new block is being created
  /// </summary>
  public PlainBlockViewModel? Block { get; }

  public bool Delete {
    get => _delete;
    set {
      if(SetValueProperty(ref _delete, value))
      {
        RaisePropertyChanged(nameof(CanEdit));
        RaisePropertyChanged(nameof(Decoration));
      }
    }
  }
  private bool _delete;

  /// <summary>
  /// A ghost block that is auto-deleted if empty. This is used
  /// fore the initial block in a new entry to guide the user.
  /// </summary>
  public bool AutoDelete { get; set; }

  public string DeleteDescription {
    get => AutoDelete
      ? "Delete this block (activates automatically if this initial block is left empty)"
      : "Delete this block";
  }

  public bool CanEdit => !Delete;

  public string Text {
    get => _text;
    set {
      if(SetValueProperty(ref _text, value))
      {
      }
    }
  }
  private string _text;

  public TextDecorationCollection? Decoration {
    get => Delete ? TextDecorations.Strikethrough : null;
  }

  public override EntryBlock? AcceptEdit()
  {
    if(Delete)
    {
      return null;
    }
    var text = Text.Trim();
    if(AutoDelete && String.IsNullOrEmpty(text))
    {
      return null;
    }
    var block = new PlainEntryBlock();
    block.Text = text;
    return block;
  }

}
