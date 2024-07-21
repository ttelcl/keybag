/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model.Contents.Blocks;

namespace Keybag3.Main.KeybagContent.EntryBlocks;

public class PlainBlockViewModel: EntryBlockViewModel
{
  public PlainBlockViewModel(
    PlainEntryBlock model,
    EntryViewModel owner)
    : base(model, owner)
  {
    Model = model;
  }

  public PlainEntryBlock Model { get; }

  public string Text {
    get => Model.Text;
    set {
      var old = Model.Text;
      Model.Text = value;
      if(CheckValueProperty(old, value))
      {
        TagAsModified();
      }
    }
  }

}
