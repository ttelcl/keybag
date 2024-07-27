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

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Model.TreeMath;

namespace Keybag3.Main.KeybagContent;

public class SectionMap: ViewModelBase<KeybagViewModel>
{
  private readonly Dictionary<string, SectionModel> _sectionMap;

  public SectionMap(
    KeybagViewModel ownerModel)
    : base(ownerModel)
  {
    _sectionMap = new Dictionary<string, SectionModel>(
      StringComparer.InvariantCultureIgnoreCase);
    var defaultSectionValue =
      // If not found, this will initialize the section settings!
      // And by extension, this may initialize the settings file.
      Model.Owner.Model.GetSectionState(DefaultSectionName);
    DefaultSection = new SectionModel(
      DefaultSectionName, this, defaultSectionValue);
    DefaultSection.IsEnabled = false;
    _sectionMap[DefaultSection.SectionName] = DefaultSection;
    // (Sections other than the default section are created on use.)
    // Initialize the sections viewmodel
    RebuildSections(false /* prevent recursion */);
  }

  public const string DefaultSectionName = "(default)";

  public ChunkSpace<EntryViewModel> Space { get => Model.EntrySpace; }

  public SectionModel DefaultSection { get; }

  public SectionModel Get(string sectionName, bool create)
  {
    if(!_sectionMap.TryGetValue(sectionName, out var section))
    {
      if(!create)
      {
        throw new InvalidOperationException(
          "Unknown Section");
      }
      var keybagSet = Model.Owner.Model;
      var isActive = keybagSet.GetSectionState(sectionName);
      section = new SectionModel(sectionName, this, isActive);
      _sectionMap[sectionName] = section;
      RebuildSections();
    }
    return section;
  }

  /// <summary>
  /// Make sure a section key entry is in the sections it declares.
  /// This does NOT remove the key from sections it no longer declares.
  /// </summary>
  /// <param name="entryId">
  /// The entry ID to sync.
  /// </param>
  /// <returns>
  /// True if there were any changes
  /// </returns>
  public bool SyncEntry(ChunkId entryId)
  {
    var changes = false;
    var entry = Space[entryId];
    foreach(var sectionName in entry.KeySectionSet)
    {
      var section = Get(sectionName, true);
      changes = section.KeyEntries.Add(entryId) || changes;
    }
    return changes;
  }

  public void SyncBoth()
  {
    Trace.TraceWarning(
      $"Synchronizing sections ({DateTime.Now:yyyy-MM-dd HH:mm:ss.fff})");
    SyncAllEntries();
    SyncAllSections();
  }

  public void SyncAllEntries()
  {
    foreach(var entry in Space.All)
    {
      SyncEntry(entry.NodeId);
    }
  }

  /// <summary>
  /// Remove entries from sections that no longer declare them.
  /// Removes sections that no longer have any key entries.
  /// </summary>
  public void SyncAllSections()
  {
    var sections = _sectionMap.Values.ToList();
    var changed = false;
    foreach(var section in sections)
    {
      if(!Object.ReferenceEquals(section, DefaultSection)
        && section.SyncSection()
        && section.KeyEntries.ChunkIds.Count == 0)
      {
        _sectionMap.Remove(section.SectionName);
        changed = true;
      }
    }
    if(changed)
    {
      RebuildSections();
    }
  }

  public IReadOnlyList<SectionModel> Sections {
    get => _sections;
    private set {
      if(SetInstanceProperty(ref _sections, value))
      {
      }
    }
  }
  private IReadOnlyList<SectionModel> _sections = [];

  private void RebuildSections(bool notify=true)
  {
    var list = _sectionMap.Values.ToList();
    list.Sort((a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(
      a.SectionName, b.SectionName));
    Sections = list;
    if(notify) // else prevent recursion
    {
      SectionActiveChanged();
    }
  }

  internal void SectionActiveChanged()
  {
    var keybagSet = Model.Owner.Model;
    var activeCount = _sectionMap.Values.Count(s => s.IsActive);
    var lockdown = activeCount <= 1;
    foreach(var s in _sectionMap.Values)
    {
      s.IsEnabled = !lockdown || !s.IsActive;
      keybagSet.SetSectionState(s.SectionName, s.IsActive);
    }
    if(activeCount == 0)
    {
      DefaultSection.IsActive = true; // will recurse
    }
    else
    {
      var selection = Model.SelectedEntry;
      if(selection != null)
      { 
        selection.IsSelected = false;
      }
      Model.RecalculateScope();
      Model.RecalculateMatches();
      if(selection != null && Model.VisibleSet.Contains(selection.NodeId))
      {
        selection.IsSelected = true;
      }
    }
  }

  public ChunkSet<EntryViewModel> SectionScope() 
  { 
    // All key entries in all sections plus descendants (but not ancestors!)
    var allSectionsScope = Space.CreateSet();
    // All key entries, descendants, and ancestors for active sections
    var activeSectionsScope = Space.CreateSet();
    foreach(var section in _sectionMap.Values)
    {
      if(!Object.ReferenceEquals(section, DefaultSection))
      {
        var scope = Space.CreateSet();
        section.KeyEntries.AddConnectedTo(scope, true, true, false);
        allSectionsScope.AddRange(scope);
        if(section.IsActive)
        {
          activeSectionsScope.AddRange(scope);
          section.KeyEntries.AddAncestorsTo(activeSectionsScope);
        }
      }
    }
    if(DefaultSection.IsActive)
    {
      // All entries that are not in any non-default section (excluding
      // section key ancestors) : that is the default section
      var remaining = Space.CreateSet(true) - allSectionsScope;
      activeSectionsScope.AddRange(remaining);
    }
    return activeSectionsScope;
  }

  // --
}
