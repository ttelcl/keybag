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

using Lcl.KeyBag3.Model.TreeMath;

namespace Keybag3.Main.KeybagContent;

/// <summary>
/// Information about a section
/// </summary>
public class SectionModel: ViewModelBase
{
  internal SectionModel(
    string sectionName,
    SectionMap owner,
    bool isActive)
  {
    SectionName = sectionName;
    Owner = owner;
    KeyEntries = Owner.Space.CreateSet();
    _isActive = isActive;
  }

  public string SectionName { get; }

  public SectionMap Owner { get; }

  public bool IsActive {
    get => _isActive;
    set {
      if(SetValueProperty(ref _isActive, value))
      {
        Owner.SectionActiveChanged();
      }
    }
  }
  private bool _isActive;

  public bool IsEnabled {
    get => _isEnabled;
    set {
      if(SetValueProperty(ref _isEnabled, value))
      {
      }
    }
  }
  private bool _isEnabled = true;

  /// <summary>
  /// The key entries that define this section
  /// </summary>
  public ChunkSet<EntryViewModel> KeyEntries { get; private set; }

  /// <summary>
  /// Make sure that this section is still in all the key entries
  /// that define it, removing entries it no longer is in.
  /// </summary>
  /// <returns>
  /// True if any entries were removed from this set.
  /// </returns>
  public bool SyncSection()
  {
    var changes = false;
    var entryIds = KeyEntries.ToList();
    foreach(var entryId in entryIds)
    {
      var entry = Owner.Space[entryId];
      var present = entry.KeySectionSet.Contains(SectionName);
      if(!present)
      {
        KeyEntries.Remove(entryId);
        changes = true;
      }
    }
    return changes;
  }
}
