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

public class UnrecognizedBlockViewModel: EntryBlockViewModel
{
  public UnrecognizedBlockViewModel(
    EntryBlock model,
    EntryViewModel owner) 
    : base(model, owner)
  {
  }

}
