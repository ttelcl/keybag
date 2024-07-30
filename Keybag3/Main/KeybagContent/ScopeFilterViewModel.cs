/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Keybag3.MessageUtilities;
using Keybag3.WpfUtilities;

using Lcl.KeyBag3.Model;

namespace Keybag3.Main.KeybagContent;

public class ScopeFilterViewModel: ViewModelBase<KeybagViewModel>
{
  public ScopeFilterViewModel(
    KeybagViewModel keybag)
    : base(keybag)
  {
  }

  public const string ScopeFilterChanged = "scope-filter-changed";

  public bool Expanded {
    get => _expanded;
    set {
      if(SetValueProperty(ref _expanded, value))
      {
        RaisePropertyChanged(nameof(ExpanderIcon));
      }
    }
  }
  private bool _expanded = false;

  public string ExpanderIcon {
    get {
      return Expanded ? "ChevronUpCircleOutline" : "ChevronDownCircleOutline";
    }
  }

  public bool? ShowArchived {
    get => _showArchived;
    set {
      if(SetValueProperty(ref _showArchived, value))
      {
        Model.SendMessage(ScopeFilterChanged, this);
      }
    }
  }
  private bool? _showArchived = false;

  public bool? ShowErased {
    get => _showErased;
    set {
      if(SetValueProperty(ref _showErased, value))
      {
        Model.SendMessage(ScopeFilterChanged, this);
      }
    }
  }
  private bool? _showErased = false;

  public bool? ShowSealed {
    get => _showSealed;
    set {
      if(SetValueProperty(ref _showSealed, value))
      {
        Model.SendMessage(ScopeFilterChanged, this);
      }
    }
  }
  private bool? _showSealed = null;

  public ChunkFlags ShowFilter {
    get {
      return
        (ShowArchived == true ? ChunkFlags.Archived : ChunkFlags.None)
        | (ShowErased == true ? ChunkFlags.Erased : ChunkFlags.None)
        | (ShowSealed == true ? ChunkFlags.Sealed : ChunkFlags.None);
    }
  }

  public ChunkFlags ShowMask {
    get {
      return
        (ShowArchived.HasValue ? ChunkFlags.Archived : ChunkFlags.None)
        | (ShowErased.HasValue ? ChunkFlags.Erased : ChunkFlags.None)
        | (ShowSealed.HasValue ? ChunkFlags.Sealed : ChunkFlags.None);
    }
  }

  public IEnumerable<T> Filter<T>(IEnumerable<T> items) where T : IKeybagChunk
  {
    var mask = ShowMask;
    var filter = ShowFilter;
    return items
      .Where(i => (i.Flags & mask) == filter);
  }
}
