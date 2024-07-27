/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Model.Contents;
using Lcl.KeyBag3.Model.TreeMath;

using Keybag3.Main.Database;
using Keybag3.Main.Synchronization;
using Keybag3.WpfUtilities;
using Lcl.KeyBag3.Storage;

namespace Keybag3.Main.KeybagContent;

public class KeybagViewModel: ViewModelBase, IEntryContainer, IHasMessageHub
{
  public KeybagViewModel(
    KeybagSetViewModel owner)
  {
    Owner = owner;
    MessageHub = new MessageHub();
    var fileName = owner.Model.PrimaryFile;
    RawKeybag = Keybag.FromFile(fileName);
    RawHistory = new KeybagHistory(RawKeybag, fileName);
    ChunkPairs = new ChunkPairMap();
    ChunkPairs.InsertAll(RawKeybag.Chunks);
    EntrySpace = new ChunkSpace<EntryViewModel>();
    ScopeFilter = new ScopeFilterViewModel(this);
    SearchFilter = new SearchFilterViewModel(this);
    _scope = EntrySpace.CreateSet();
    _matches = EntrySpace.CreateSet();
    _visibleSet = EntrySpace.CreateSet();
    _searchResult = BuildClearResult();
    Sections = new SectionMap(this);
    DeselectCommand = new DelegateCommand(p => {
      if(SelectedEntry != null)
      {
        SelectedEntry.IsSelected = false;
      }
    });
    StartNewRootEntryCommand = new DelegateCommand(p => {
      if(SelectedEntry != null)
      {
        SelectedEntry.IsSelected = false;
      }
      EntryEditViewModel.StartNewRootEntry(this);
    });
    var key = Owner.FindKey();
    if(key != null)
    {
      RawKeybag.ValidateSeals(key);
      ChunkPairs.InitModels(key);
      Trace.TraceInformation($"Loaded {ChunkPairs.Chunks.Count} chunks");
      foreach(var pair in ChunkPairs.Chunks)
      {
        if(pair.ModelChunk?.BaseContent is EntryContent)
        {
          var evm = new EntryViewModel(pair, this);
          EntrySpace.Register(evm);
        }
      }
      Trace.TraceInformation($"Loaded {EntrySpace.All.Count} entries");
      InvalidateChildMap();
      RecalculateScope();
      RecalculateMatches();
      RebuildList(true);
      Trace.TraceInformation($"Connected entries: found {ChildList.Count} roots");
      Sections.SyncBoth();
      Trace.TraceInformation($"Sections: found {Sections.Sections.Count} sections");
      Decoded = true;
    }
    else
    {
      Trace.TraceError("Internal Error: Keybag Not Decoded. This state may be unstable.");
      Decoded = false;
    }
    this.Subscribe<ScopeFilterViewModel>(
      MessageChannels.ScopeFilterChanged, ScopeFilterChanged, true);
  }

  public ICommand DeselectCommand { get; }

  public ICommand StartNewRootEntryCommand { get; }

  /// <summary>
  /// The collection of all entries (as ViewModel) in the keybag.
  /// This includes all entries in the file, even those that are
  /// not visible.
  /// </summary>
  public ChunkSpace<EntryViewModel> EntrySpace { get; }

  public IReadOnlySet<ChunkId> ChildSet()
  {
    return ChildIdMap().RootIds;
  }

  public KeybagViewModel HostKeybag { get => this; }

  public MessageHub MessageHub { get; }

  /// <summary>
  /// A map of entry IDs to their child entry IDs. Returns
  /// a cached map that is recalculated after it is invalidated.
  /// Invalidation happens automatically upon adding (or removing)
  /// entries in <see cref="EntrySpace"/>, but can be forced by
  /// calling <see cref="InvalidateChildMap"/>.
  /// </summary>
  internal ChunkChunkSetMap<EntryViewModel> ChildIdMap()
  {
    return EntrySpace.AllChildMap;
  }

  /// <summary>
  /// Invalidate the master child id map, causing it to be
  /// recalculated upon next invocation of <see cref="ChildIdMap"/>.
  /// Note that child lists in <see cref="EntryViewModel"/> instances
  /// are NOT invalidated by this method.
  /// </summary>
  public void InvalidateChildMap()
  {
    EntrySpace.InvalidateChildMap();
  }

  public bool FilterExpanded {
    get => _filterExpanded;
    set {
      if(SetValueProperty(ref _filterExpanded, value))
      {
        RaisePropertyChanged(nameof(FilterExpandedIcon));
      }
    }
  }
  private bool _filterExpanded = true;

  public string FilterExpandedIcon {
    get {
      return FilterExpanded ? "ChevronUpCircleOutline" : "ChevronDownCircleOutline";
    }
  }

  public KeybagSetViewModel Owner { get; }

  public ChunkSet<EntryViewModel> Scope {
    get => _scope;
    set {
      if(SetInstanceProperty(ref _scope, value))
      {
        UpdateVisibleSet();
      }
    }
  }
  private ChunkSet<EntryViewModel> _scope;

  public ChunkSet<EntryViewModel> Matches {
    get => _matches;
    set {
      if(SetInstanceProperty(ref _matches, value))
      {
        UpdateVisibleSet();
      }
    }
  }
  private ChunkSet<EntryViewModel> _matches;

  /// <summary>
  /// The set of ALL visible entries (not just roots)
  /// </summary>
  public ChunkSet<EntryViewModel> VisibleSet {
    get => _visibleSet;
    set {
      if(SetInstanceProperty(ref _visibleSet, value))
      {
        RebuildList(true);
      }
    }
  }
  private ChunkSet<EntryViewModel> _visibleSet;

  public bool IgnoreScope {
    get => _ignoreScope;
    set {
      if(SetValueProperty(ref _ignoreScope, value))
      {
        UpdateVisibleSet();
      }
    }
  }
  private bool _ignoreScope = false;

  private void UpdateVisibleSet()
  {
    var oldSelected = SelectedEntry;
    if(oldSelected != null)
    {
      oldSelected.IsSelected = false;
    }
    if(IgnoreScope)
    {
      VisibleSet = EntrySpace.CreateSet(Matches); // clone!
    }
    else
    {
      VisibleSet = Matches * Scope;
    }
    if(oldSelected != null && VisibleSet.Contains(oldSelected.NodeId))
    {
      oldSelected.IsSelected = true;
    }
  }

  public SectionMap Sections { get; }

  public void RecalculateScope()
  {
    var dummy = EntrySpace.ParentMap; // force update of parent-child relations
    var flagsScope = EntrySpace.CreateSet();
    foreach(var entry in ScopeFilter.Filter(EntrySpace.All))
    {
      flagsScope.Add(entry.NodeId);
    }
    Sections.SyncBoth();
    flagsScope = flagsScope.AncestorSet(true);
    var sectionScope = Sections.SectionScope();
    var scope = flagsScope * sectionScope;
    //// for now: make sure that the current entry is kept in the scope
    //if(SelectedEntry != null)
    //{
    //  scope.Add(SelectedEntry.NodeId);
    //}
    Scope = scope;
  }

  public void RecalculateMatches()
  {
    var rawResult = SearchFilter.RunSearch();

    //// for now: make sure that the current entry is kept in the matches
    //if(SelectedEntry != null)
    //{
    //  result.Add(SelectedEntry.NodeId);
    //}

    SearchResult = rawResult;
  }

  /// <summary>
  /// The list of visible roots (implements <see cref="IEntryContainer.ChildList"/>)
  /// </summary>
  public IReadOnlyList<EntryViewModel> ChildList {
    get => _childList;
    private set {
      if(SetInstanceProperty(ref _childList, value))
      {
      }
    }
  }
  private IReadOnlyList<EntryViewModel> _childList = [];

  public void RebuildList(bool recursive)
  {
    // Assume (Scope and Matches and) VisibleSet have been calculated
    var list = new List<EntryViewModel>();
    foreach(var entryId in VisibleSet.RootIds)
    {
      list.Add(EntrySpace[entryId]);
    }
    list.Sort((a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(
      a.Label, b.Label));
    ChildList = list;
    if(recursive)
    {
      foreach(var entry in list)
      {
        entry.RebuildList(recursive);
      }
    }
  }

  private Keybag RawKeybag { get; set; }

  private KeybagHistory RawHistory { get; set; }

  private ChunkPairMap ChunkPairs { get; /*set;*/ }

  public KeybagSynchronizer CreateSynchronizer()
  {
    return new KeybagSynchronizer(Owner.Model, RawKeybag);
  }

  /// <summary>
  /// Enumerate all stored chunks in the chunk pair storage
  /// plus all seal chunks.
  /// </summary>
  public IEnumerable<StoredChunk> AllRawChunks {
    get {
      foreach(var pair in ChunkPairs.Chunks)
      {
        var storedChunk = pair.PersistChunk;
        if(storedChunk != null)
        {
          yield return storedChunk;
        }
      }
      foreach(var seal in RawKeybag.Seals.All)
      {
        yield return seal.Seal;
      }
    }
  }

  /// <summary>
  /// Enumerate all stored chunks in the chunk pair storage
  /// (but not seal chunks).
  /// </summary>
  public IEnumerable<StoredChunk> AllChunks {
    get {
      foreach(var pair in ChunkPairs.Chunks)
      {
        var storedChunk = pair.PersistChunk;
        if(storedChunk != null)
        {
          yield return storedChunk;
        }
      }
    }
  }

  public ChunkId GetNewestChunkEdit()
  {
    return AllChunks.MaxBy(c => c.EditId.Value)!.EditId;
  }

  /// <summary>
  /// Update the "modified" flag for the normal editing use case.
  /// Not valid for Synchronization!
  /// </summary>
  public void UpdateHasUnsavedChunks()
  {
    HasUnsavedChunks = ChunkPairs.Chunks.Any(
      pair => pair.PersistChunk == null || !pair.PersistChunk.FileOffset.HasValue);
  }

  /// <summary>
  /// Check if there are any unsaved chunks in the keybag's chunk pairs.
  /// Note that synchronization bypasses the chunk pairs completely,
  /// so it is NOT valid for that use case
  /// </summary>
  public bool HasUnsavedChunks {
    get => _hasUnsavedChunks;
    private set {
      if(SetValueProperty(ref _hasUnsavedChunks, value))
      {
      }
    }
  }
  private bool _hasUnsavedChunks;

  public bool Decoded {
    get => _decoded;
    set {
      if(SetValueProperty(ref _decoded, value))
      {
      }
    }
  }
  private bool _decoded;

  public void Save()
  {
    var key = Owner.FindKey();
    if(!Decoded || key == null)
    {
      MessageBox.Show(
        "Cannot save a keybag without having its key available",
        "Internal Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    if(!HasUnsavedChunks)
    {
      MessageBox.Show(
        "This keybag has nothing to save\n(internal error)",
        "Information",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
      return;
    }
    /*
     * At this point this viewmodel has not been synced to RawKeybag yet!
     * So let's do that first. make sure NOT to include seal chunks
     */
    foreach(var chunk in AllChunks)
    {
      RawKeybag.Chunks.PutChunk(chunk);
    }
    var primaryFile = Owner.Model.PrimaryFile;
    RawKeybag.WriteFull(primaryFile, key, RawHistory);
    SelectedEntry?.PostSaveNotification();
    Owner.Refresh();
    UpdateHasUnsavedChunks();
    if(HasUnsavedChunks)
    {
      Trace.TraceError("Error saving keybag (something is still marked as unsaved)");
      MessageBox.Show(
        "Error saving keybag (something is still marked as unsaved)",
        "Internal Error",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
    }
  }

  /// <summary>
  /// The selected entry. Because of the weird design of the
  /// WPF TreeView, this property is not bound to the TreeView
  /// but controlled by the <see cref="EntryViewModel.IsSelected"/> properties
  /// </summary>
  public EntryViewModel? SelectedEntry {
    get => _selectedEntry;
    set {
      var previous = _selectedEntry;
      if(SetNullableInstanceProperty(ref _selectedEntry, value))
      {
        if(_selectedEntry != null)
        {
          // Trace.TraceInformation($"Selected entry: {_selectedEntry.Label}");
        }
        else
        {
          // Trace.TraceInformation("Selected entry: null");
        }
        //if(previous != null)
        //{
        //  previous.IsSelected = false;
        //}
        //if(_selectedEntry != null)
        //{
        //  _selectedEntry.IsSelected = true;
        //}
      }
    }
  }
  private EntryViewModel? _selectedEntry;

  public EntryViewModel AddNewEntry(
    EntryContent entryContent, EntryViewModel? parent)
  {
    var cryptor = Owner.FindKey();
    if(cryptor == null)
    {
      throw new InvalidOperationException("No key found");
    }
    var fileId = RawKeybag.FileId;
    var parentId = parent?.NodeId ?? fileId;
    var nodeId = cryptor.IdGenerator.NextId();
    var editId = nodeId;
    var newEntry = new ContentChunk<EntryContent>(
      ChunkKind.Entry,
      ChunkFlags.None,
      nodeId,
      editId,
      parentId,
      fileId,
      entryContent);
    var created = ChunkPairs.UpdateModel(newEntry, out var pair);
    if(!created)
    {
      throw new InvalidOperationException(
        "Internal error: expected new entry node to have been created");
    }
    var evm = new EntryViewModel(pair, this);
    EntrySpace.Register(evm);

    Sections.SyncBoth();

    // for now: forcefully add it to the scope and matches, and assume
    // the parent is already visible
    Scope.Add(evm.NodeId);
    Matches.Add(evm.NodeId);
    UpdateVisibleSet();

    if(evm.Parent != null)
    {
      evm.Parent.IsExpanded = true;
    }
    evm.IsSelected = true;

    var key = Owner.FindKey();
    if(key != null)
    {
      ChunkPairs.PrepareToSave(key);
    }
    UpdateHasUnsavedChunks();

    return evm;
  }


  public ScopeFilterViewModel ScopeFilter {
    get;
  }

  private void ScopeFilterChanged(ScopeFilterViewModel model)
  {
    RecalculateScope();
  }

  public SearchFilterViewModel SearchFilter {
    get;
  }

  public ChunkMapping<SearchOutcome> BuildSearchResult(
    ChunkSet<EntryViewModel> hits,
    ChunkSet<EntryViewModel> blockers)
  {
    var blocked = blockers.DescendentsSet();
    var support = hits.AncestorSet(false);
    var indirect = hits.DescendentsSet();
    // Note that the sets above may overshoot. We fix that by considering
    // the right order for adding them to the result.
    var result = new ChunkMapping<SearchOutcome>(SearchOutcome.NoMatch);
    result.SetAll(hits.Space.AllIds, SearchOutcome.NoMatch);
    result.SetAll(indirect, SearchOutcome.Indirect);
    result.SetAll(support, SearchOutcome.Support);
    result.SetAll(hits, SearchOutcome.Hit);
    result.SetAll(blocked, SearchOutcome.Blocked);
    result.SetAll(blockers, SearchOutcome.Blocker);

    var searchKind = SearchFilter.SearchKind;
    Owner.Owner.AppModel.StatusMessage =
      $"{searchKind} search found {hits.ChunkIds.Count} matching entries";
    return result;
  }

  public ChunkMapping<SearchOutcome> BuildClearResult()
  {
    var result = new ChunkMapping<SearchOutcome>(SearchOutcome.NoSearch);
    result.SetAll(EntrySpace.AllIds, SearchOutcome.NoSearch);
    Owner.Owner.AppModel.StatusMessage = "";
    return result;
  }

  /// <summary>
  /// Set the search result (starting a chain of operations that changes
  /// <see cref="ChildList"/> and <see cref="VisibleSet"/> ultimately).
  /// </summary>
  public ChunkMapping<SearchOutcome> SearchResult {
    get => _searchResult;
    set {
      if(SetInstanceProperty(ref _searchResult, value))
      {
        ApplySearchResult(value);
      }
    }
  }
  private ChunkMapping<SearchOutcome> _searchResult;

  private void ApplySearchResult(
    ChunkMapping<SearchOutcome>? result)
  {
    var time0 = DateTime.UtcNow;
    result ??= BuildClearResult();
    var useScope = !IgnoreScope;
    var matches = EntrySpace.CreateSet();
    foreach(var entryId in EntrySpace.AllIdsTopological(false))
    {
      var entry = EntrySpace[entryId];
      entry.SearchStatus = result[entryId];
      entry.IsInScope = Scope.Contains(entryId);
      var searchVisible =
        result[entryId] switch {
          SearchOutcome.Hit => true,
          SearchOutcome.Indirect => true,
          SearchOutcome.Support => true,
          SearchOutcome.NoSearch => true,
          SearchOutcome.Blocked => false,
          SearchOutcome.Blocker => false,
          SearchOutcome.NoMatch => false,
          _ => throw new InvalidOperationException("Invalid SearchOutcome")
        };
      if(searchVisible)
      {
        matches.Add(entryId);
      }
    }
    Matches = matches; // recursively also changes VisibleSet and ChildList

    // Make sure expansion state is correct. Expand any ancestor of
    // a hit as well as any ancestor of the selected entry (if any).
    // The former includes the "support" but also any hits that have
    // child hits.
    var hitsAndSelection = EntrySpace.CreateSet(
      result.WhereValue(so => so == SearchOutcome.Hit));
    if(SelectedEntry != null)
    {
      hitsAndSelection.Add(SelectedEntry.NodeId);
    }
    var expansionSet = EntrySpace.CreateSet();
    hitsAndSelection.AddAncestorsTo(expansionSet);
    foreach(var chunkId in EntrySpace.AllIdsTopological(false))
    {
      var entry = EntrySpace[chunkId];
      if(VisibleSet.Contains(chunkId))
      {
        entry.IsExpanded = expansionSet.Contains(chunkId);
      }
    }

    var time1 = DateTime.UtcNow;
    var elapsed = time1 - time0;
    Trace.TraceInformation(
      $"Search result applied in {elapsed.TotalMilliseconds} ms. " +
      $"{Matches.ChunkIds.Count} matches. {expansionSet.ChunkIds.Count} expands");
  }

  // --
}
