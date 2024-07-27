/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Model.Contents;
using Lcl.KeyBag3.Model.Tags;
using Lcl.KeyBag3.Model.TreeMath;

using Keybag3.WpfUtilities;
using Keybag3.Main.KeybagContent.EntryBlocks;

namespace Keybag3.Main.KeybagContent;

/// <summary>
/// Models an entry for GUI use
/// </summary>
public class EntryViewModel:
  ViewModelBase<ChunkPair>, IKeybagChunk, IEntryContainer
{
  public EntryViewModel(
    ChunkPair entryPair,
    KeybagViewModel owner)
    : base(entryPair)
  {
    Owner = owner;
    _childList = [];
    Blocks = [];
    var modelChunk = Model.ModelChunk;
    if(modelChunk?.BaseContent is EntryContent entryContent)
    {
      Chunk = modelChunk;
      Content = entryContent;
      Label = entryContent.Label;
    }
    else
    {
      throw new ArgumentException(
        "Expecting a decoded entry chunk pair");
    }
    var Tags = new TagSet(); // dummy
    ReloadTags();
    AncestorTrail = [];
    SyncBlocks();
    SelectThisCommand = new DelegateCommand(p => {
      IsSelected = true;
    });
    AddChildCommand = new DelegateCommand(p => {
      EntryEditViewModel.StartNewChildEntry(this);
    });
    EditThisCommand = new DelegateCommand(p => {
      EntryEditViewModel.StartEditEntry(this);
    }, p => !IsSealed && !IsErased && !IsArchived);
    BreakSealCommand = new DelegateCommand(p => {
      BreakSeal();
    }, p => IsSealed);
    ArchiveCommand = new DelegateCommand(p => {
      Archive();
    }, p => CanArchive);
    UnarchiveCommand = new DelegateCommand(p => {
      Unarchive();
    }, p => CanUnarchive);
    RecalculateVisibleTags();
  }

  public ICommand SelectThisCommand { get; }

  public ICommand AddChildCommand { get; }

  public ICommand EditThisCommand { get; }

  public ICommand BreakSealCommand { get; }

  public ICommand ArchiveCommand { get; }

  public ICommand UnarchiveCommand { get; }

  public ChunkSpace<EntryViewModel> EntrySpace { get => Owner.EntrySpace; }

  /// <summary>
  /// The object that contains this entry (either the parent entry
  /// or the keybag itself)
  /// </summary>
  public IEntryContainer EntryContainer {
    get {
      IEntryContainer? parent = EntrySpace.Find(ParentId);
      return parent ?? Owner;
    }
  }

  public IReadOnlySet<ChunkId> ChildSet()
  {
    return Owner.ChildIdMap()[NodeId];
  }

  public KeybagViewModel HostKeybag { get => Owner; }

  public IReadOnlyList<EntryViewModel> ChildList {
    get => _childList;
    set {
      if(SetInstanceProperty(ref _childList, value))
      {
      }
    }
  }
  private IReadOnlyList<EntryViewModel> _childList;

  public void RebuildList(bool recursive)
  {
    var visibleChildSet = EntrySpace.Intersection(
      ChildSet(), Owner.VisibleSet.ChunkIds);
    var list = new List<EntryViewModel>();
    foreach(var childId in visibleChildSet)
    {
      var child = EntrySpace.Find(childId);
      if(child != null)
      {
        list.Add(child);
        if(recursive)
        {
          child.RebuildList(true);
        }
      }
    }
    // sort:
    list.Sort((a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(
      a.Label, b.Label));
    ChildList = list;
    // Use the opportunity to set the Parent as well ...
    Parent = EntrySpace.Find(ParentId);
  }

  public KeybagViewModel Owner { get; }

  public ContentChunk Chunk { get; }

  public EntryContent Content { get; }

  public ChunkKind Kind { get => Chunk.Kind; } // immutable

  public ChunkId NodeId { get => Chunk.NodeId; } // immutable

  public ChunkId FileId { get => Chunk.FileId; } // immutable

  /// <summary>
  /// Requires manual RaisePropertyChanged after saving
  /// </summary>
  public ChunkId EditId { get => Chunk.EditId; } // it's complicated

  public ChunkId ParentId { // mutation not yet implemented
    get => Chunk.ParentId;
    // Underlying property is currently read-only
    //set {
    //  var old = Model.ParentId;
    //  Model.ParentId = value;
    //  if(CheckValueProperty(old, value))
    //  {
    //  }
    //}
  }

  public ChunkFlags Flags { // mutable
    get => Chunk.Flags;
    set {
      var old = Chunk.Flags;
      Chunk.Flags = value;
      if(CheckValueProperty(old, value))
      {
        RaisePropertyChanged(nameof(IsErased));
        RaisePropertyChanged(nameof(IsArchived));
        RaisePropertyChanged(nameof(IsSealed));
        RaisePropertyChanged(nameof(ForegroundForFlags));
        RaisePropertyChanged(nameof(EntryColor));
        RaisePropertyChanged(nameof(CanArchive));
        RaisePropertyChanged(nameof(CanUnarchive));
        CheckNeedsPersisting();
        CheckChanged();
      }
    }
  }

  public void CheckChanged()
  {
    if(NeedsPersisting)
    {
      var key = Owner.Owner.FindKey();
      if(key != null)
      {
        Model.PrepareToSave(key);
      }
      Owner.UpdateHasUnsavedChunks();
    }
  }

  public string NodeId26 { get => NodeId.ToBase26(); }

  public string EditId26 { get => EditId.ToBase26(); }

  public string Created {
    get => NodeId.ToStamp().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
  }

  public string Modified {
    get => EditId.ToStamp().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
  }

  public bool IsErased { get => Chunk.IsErased(); }

  public bool IsArchived { get => Chunk.IsArchived(); }

  public bool IsSealed { get => Chunk.IsSealed(); }

  public bool CanArchive {
    get => !IsSealed && !IsArchived && !IsErased;
  }

  public bool CanUnarchive {
    get => !IsSealed && IsArchived && !IsErased;
  }

  public ObservableCollection<EntryViewModel> AncestorTrail { get; }

  public ObservableCollection<EntryBlockViewModel> Blocks { get; }

  public string ForegroundForFlags {
    get {
      if(IsErased)
        return "Error";
      if(IsArchived)
        return "MidGray";
      if(IsSealed)
        return "LightGray";
      return "White";
    }
  }

  public bool NeedsPersisting {
    // Not automatically coupled to the model!
    get => _needsPersisting;
    set {
      if(SetValueProperty(ref _needsPersisting, value))
      {
      }
    }
  }
  private bool _needsPersisting;

  public void CheckNeedsPersisting()
  {
    NeedsPersisting = Model.NeedsPersisting;
  }

  public string Label {
    get => _label;
    set {
      if(SetInstanceProperty(ref _label, value))
      {
        EntryContainer.RebuildList(false);
      }
    }
  }
  private string _label = "?";

  public void PostEditNotification()
  {
    CheckNeedsPersisting();
    var key = Owner.Owner.FindKey();
    if(key != null)
    {
      Model.PrepareToSave(key);
    }
    else
    {
      Trace.TraceWarning(
        $"Key not found for bag '{Owner.Owner.Tag}'");
    }
    RaisePropertyChanged(nameof(EditId));
    RaisePropertyChanged(nameof(EditId26));
    RaisePropertyChanged(nameof(Modified));

    Owner.UpdateHasUnsavedChunks();
  }

  public void PostSaveNotification()
  {
    RaisePropertyChanged(nameof(EditId));
    RaisePropertyChanged(nameof(EditId26));
    RaisePropertyChanged(nameof(Modified));
  }

  public EntryViewModel? Parent {
    get => _parent;
    set {
      if(SetNullableInstanceProperty(ref _parent, value))
      {
      }
    }
  }
  private EntryViewModel? _parent;

  public string Icon {
    get {
      // use ChildSet rather than ChildList, so that entries that
      // have only invisible children still show as folders
      var hasChildren = ChildSet().Count > 0;
      var hasBlocks = Content.Blocks.Count > 0;
      return
        hasBlocks
        ? (hasChildren ? "FolderKey" : "ScriptTextKey")
        : (hasChildren ? "FolderKeyOutline" : "ScriptTextKeyOutline");
    }
  }

  /// <summary>
  /// True if this entry is selected.
  /// Note that its relation to the Treeview's SelectedItem is the opposite
  /// of what you may expect: this <see cref="IsSelected"/> child property
  /// controls the <see cref="KeybagViewModel.SelectedEntry"/> property
  /// of <see cref="Owner"/>, not the other way around
  /// </summary>
  public bool IsSelected {
    get => _isSelected;
    set {
      if(SetValueProperty(ref _isSelected, value))
      {
        if(value)
        {
          if(Owner.SelectedEntry != null)
          {
            // Hack-y solution alert!
            // Force correct timing on this. Without this, there is a
            // rare situation where Owner.SelectedEntry is set to null
            // *after* being set to this (adding first child to an entry).
            Owner.SelectedEntry.IsSelected = false;
          }
          Owner.SelectedEntry = this;
          // About to be shown, so last chance to pre-cache things
          PreShowing();
        }
        else
        {
          Owner.SelectedEntry = null;
        }
        // Trace.TraceInformation($"Selected {_isSelected}: {Label}");
      }
    }
  }
  private bool _isSelected;

  public bool IsExpanded {
    get => _isExpanded;
    set {
      if(SetValueProperty(ref _isExpanded, value))
      {
      }
    }
  }
  private bool _isExpanded;

  /// <summary>
  /// The set of tags directly in this entry
  /// (no synthetic tags)
  /// </summary>
  public TagSet Tags {
    get => _tags;
    set {
      if(SetInstanceProperty(ref _tags, value))
      {
        KeySectionSet = new HashSet<string>(
          Tags.KeySectionNames, StringComparer.InvariantCultureIgnoreCase);
        RaisePropertyChanged(nameof(AllTagCount));
      }
    }
  }
  private TagSet _tags = new();

  /// <summary>
  /// The set of tags that are visible in the GUI
  /// (may include synthetic tags)
  /// </summary>
  public TagSet VisibleTags {
    get => _visibleTags;
    set {
      if(SetInstanceProperty(ref _visibleTags, value))
      {
      }
    }
  }
  private TagSet _visibleTags = new();

  /// <summary>
  /// The set of tags that are logically present in the entry,
  /// whether or not visible and including synthetic tags
  /// </summary>
  public TagSet LogicalTags {
    get => _logicalTags;
    private set {
      if(SetInstanceProperty(ref _logicalTags, value))
      {
      }
    }
  }
  private TagSet _logicalTags = new();

  public int AllTagCount {
    get => Tags.All.Count;
  }

  public bool ShowHiddenTags {
    get => _showHiddenTags;
    set {
      if(SetValueProperty(ref _showHiddenTags, value))
      {
        RecalculateVisibleTags();
      }
    }
  }
  private bool _showHiddenTags;

  public IReadOnlySet<string> KeySectionSet {
    get => _keySectionSet;
    private set {
      if(SetInstanceProperty(ref _keySectionSet, value))
      {
        RaisePropertyChanged(nameof(DeclaresSections));
      }
    }
  }
  private IReadOnlySet<string> _keySectionSet =
    new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

  public bool DeclaresSections {
    get => KeySectionSet.Count > 0;
  }

  public void RecalculateVisibleTags()
  {
    var visibleTags = new TagSet();
    var hiddenTags = new TagSet();
    var titleTag = TagModel.TryFrom(
      Label.Trim().Replace(' ', '_'),
      TagClass.Title);
    if(titleTag != null)
    {
      visibleTags.Put(titleTag);
    }
    foreach(var tag in Tags.All)
    {
      if(tag.IsHidden)
      {
        hiddenTags.Put(tag);
      }
      else
      {
        visibleTags.Put(tag);
      }
    }
    var id26 = NodeId26;
    hiddenTags.Put(TagModel.From("?id=" + id26, TagClass.Hidden));
    hiddenTags.Put(TagModel.From("?edit=" + EditId26, TagClass.Hidden));
    hiddenTags.Put(TagModel.From("?tid=" + NodeId.ToStampText(), TagClass.Hidden));
    hiddenTags.Put(TagModel.From("?tedit=" + EditId.ToStampText(), TagClass.Hidden));
    var logicalTags = new TagSet();
    logicalTags.AddAll(visibleTags);
    logicalTags.AddAll(hiddenTags);
    // Logical-only tags:
    logicalTags.Put(TagModel.From(id26, TagClass.Hidden));
    logicalTags.Put(TagModel.From(EditId26, TagClass.Hidden));
    logicalTags.Put(TagModel.From(NodeId.ToStampText(), TagClass.Hidden));
    logicalTags.Put(TagModel.From(EditId.ToStampText(), TagClass.Hidden));
    if(ShowHiddenTags)
    {
      visibleTags.AddAll(hiddenTags);
    }
    VisibleTags = visibleTags;
    LogicalTags = logicalTags;
  }

  private void PreShowing()
  {
    AncestorTrail.Clear();
    var list = new List<EntryViewModel>();
    var e = Parent;
    while(e != null)
    {
      list.Add(e);
      e = e.Parent;
    }
    list.Reverse();
    foreach(var entry in list)
    {
      AncestorTrail.Add(entry);
    }
  }

  private void BreakSeal()
  {
    if(IsSealed)
    {
      var answer = MessageBox.Show(
        "Do you really want to break the seal on this entry?\n" +
        "This will prevent further updates from the app that created the entry.",
        "Break Seal",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);
      if(answer == MessageBoxResult.Yes)
      {
        var kb2s =
          (from tag in Tags.All
           let kb2 = Kb2Tag.TryFromGeneral(tag.Tag)
           where kb2 != null
           select kb2
          ).ToList();
        foreach(var kb2 in kb2s)
        {
          var before = kb2.Source!;
          kb2.Detach();
          var after = kb2.TryGetTag();
          if(String.IsNullOrEmpty(after))
          {
            throw new InvalidOperationException(
              "Internal error");
          }
          Content.ReplaceTag(before, after);
        }
        ReloadTags();
        Flags &= ~ChunkFlags.Sealed;
        RecalculateVisibleTags();
      }
    }
  }

  private void Archive()
  {
    if(!IsSealed && !IsArchived && !IsErased)
    {
      Flags |= ChunkFlags.Archived;
      // Owner.RecalculateScope(); // postpone until Save because UX would suck
    }
  }

  private void Unarchive()
  {
    if(!IsSealed && IsArchived && !IsErased)
    {
      Flags &= ~ChunkFlags.Archived;
      // Owner.RecalculateScope();
    }
  }

  private void ReloadTags()
  {
    var tags = new TagSet();
    foreach(var tag in Content.Tags)
    {
      if(!tags.TryPut(tag))
      {
        Trace.TraceError($"Cannot add tag: '{tag}' in entry {NodeId26} ({Content.Label})");
      }
    }
    Tags = tags;
  }

  public SearchOutcome SearchStatus {
    get => _searchStatus;
    set {
      if(SetValueProperty(ref _searchStatus, value))
      {
        RaisePropertyChanged(nameof(EntryColor));
      }
    }
  }
  private SearchOutcome _searchStatus = SearchOutcome.NoSearch;

  public bool IsInScope {
    get => _isInScope;
    set {
      if(SetValueProperty(ref _isInScope, value))
      {
        RaisePropertyChanged(nameof(EntryColor));
      }
    }
  }
  private bool _isInScope = true;

  public string EntryColor {
    get {
      if(SearchStatus == SearchOutcome.Hit)
      {
        return "EntryHit";
      }
      if(IsErased)
      {
        return "EntryErased";
      }
      if(IsArchived)
      {
        return "EntryArchived";
      }
      if(!IsInScope)
      {
        return "EntryOutOfScope";
      }
      if(IsSealed)
      {
        return "EntrySealed";
      }
      return "EntryDefault";
    }
  }

  /// <summary>
  /// Recreate the viewmodel blocks from the raw blocks
  /// </summary>
  public void SyncBlocks()
  {
    Blocks.Clear();
    foreach(var rawBlock in Content.Blocks)
    {
      var block = EntryBlockViewModel.FromRawBlock(rawBlock, this);
      Blocks.Add(block);
    }
  }

  // --- GRRR!
}
