/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Model.Contents;
using Lcl.KeyBag3.Model.Contents.Blocks;

using Keybag3.WpfUtilities;
using Keybag3.Services;
using System.Collections.ObjectModel;
using Keybag3.Main.KeybagContent.EntryBlocks;

namespace Keybag3.Main.KeybagContent;

public class EntryEditViewModel: ViewModelBase
{
  public EntryEditViewModel(
    EntryViewModel? original,
    EntryViewModel? parent,
    KeybagViewModel? owner)
  {
    if(original != null && parent != null)
    {
      throw new InvalidOperationException(
        "At least one of 'original' and 'parent' should be null");
    }
    if((original != null || parent != null) && owner != null)
    {
      throw new InvalidOperationException(
        "Only pass a non-null 'owner' if neither 'original' nor 'parent' are given");
    }
    parent ??= original?.Parent;
    Owner = owner ?? parent?.Owner ?? original?.Owner ??
      throw new InvalidOperationException(
        "At least one of the arguments must be non-null");
    Parent = parent;
    Original = original;
    ParentChunkId = parent?.NodeId ?? Owner.Owner.FileId;
    SiblingLabelTags = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    Blocks = new ObservableCollection<BlockEditViewModel>();

    IEnumerable<ChunkId> siblingIds =
      Parent == null ? Owner.ChildSet() : Parent.ChildSet();
    foreach(var entryId in siblingIds)
    {
      var entry = Owner.EntrySpace[entryId];
      // ignore erased siblings and ignore the entry we are editing (if any) itself
      if(!entry.IsErased && !Object.ReferenceEquals(entry, original))
      {
        SiblingLabelTags.Add(entry.Label.Trim().Replace(' ', '_'));
      }
    }
    if(original != null)
    {
      Label = original.Label;
      var tags = String.Join(" ", original.Tags.All.Select(tag => tag.Tag));
      Tags = tags;
      _originalTags = tags; // for detecting changes

      foreach(var block in original.Blocks)
      {
        Blocks.Add(BlockEditViewModel.Create(this, block));
      }
    }
    else
    {
      LabelError = EntryContent.DescribeLabelError(_label);
      TagsError = DescribeTagsError(_tags);
      // Add an initial block to guide the user. This block is auto-deleted
      // if still empty when accepting the edit.
      var autoBlock = new PlainBlockEditViewModel(this, null) {
        AutoDelete = true,
      };
      Blocks.Add(autoBlock);
    }
    CancelCommand = new DelegateCommand(p => { Cancel(); });
    EditOrCreateCommand = new DelegateCommand(
      p => { EditOrCreate(); },
      p => IsValid);
    AddPlaintextBlockCommand = new DelegateCommand(
      p => { AddPlaintextBlock(); });
  }

  public static void StartNewRootEntry(KeybagViewModel owner)
  {
    var edit = new EntryEditViewModel(null, null, owner);
    owner.Owner.Owner.AppModel.PushOverlay(edit);
  }

  public static void StartNewChildEntry(EntryViewModel evm)
  {
    var edit = new EntryEditViewModel(null, evm, null);
    evm.Owner.Owner.Owner.AppModel.PushOverlay(edit);
  }

  public static void StartEditEntry(EntryViewModel evm)
  {
    var edit = new EntryEditViewModel(evm, null, null);
    evm.Owner.Owner.Owner.AppModel.PushOverlay(edit);
  }

  public ICommand CancelCommand { get; }

  public ICommand EditOrCreateCommand { get; }

  public ICommand AddPlaintextBlockCommand { get; }

  public void Cancel()
  {
    Owner.Owner.Owner.AppModel.PopOverlay(this);
  }

  public void EditOrCreate()
  {
    if(IsValid) // else do nothing at all
    {
      if(Original != null)
      {
        ApplyEdit();
      }
      else
      {
        CreateNewEntry();
      }
      Owner.Owner.Owner.AppModel.PopOverlay(this);
    }
  }

  public void AddPlaintextBlock()
  {
    var block = new PlainBlockEditViewModel(this, null);
    Blocks.Add(block);
  }

  public HashSet<string> SiblingLabelTags { get; }

  /// <summary>
  /// The original entry to be edited, or null when creating a new entry
  /// </summary>
  public EntryViewModel? Original { get; }

  /// <summary>
  /// The parent of the entry to be edited or created. If null, a new
  /// root entry is being created.
  /// </summary>
  public EntryViewModel? Parent { get; }

  /// <summary>
  /// The Keybag, owner of the new entry or the original and the parent.
  /// </summary>
  public KeybagViewModel Owner { get; }

  public ChunkId ParentChunkId { get; }

  public string Label {
    get => _label;
    set {
      if(SetInstanceProperty(ref _label, value))
      {
        var error = EntryContent.DescribeLabelError(_label);
        var labelTag = _label.Trim().Replace(' ', '_');
        if(error == null && SiblingLabelTags.Contains(labelTag))
        {
          error = $"The parent entry already contains a child labeled {labelTag}";
        }
        LabelError = error;
      }
    }
  }
  private string _label = String.Empty;

  public string? LabelError {
    get => _labelError;
    private set {
      if(SetNullableInstanceProperty(ref _labelError, value))
      {
        RaisePropertyChanged(nameof(IsLabelValid));
        RaisePropertyChanged(nameof(LabelStatus));
        RaisePropertyChanged(nameof(LabelStatusIcon));
        RaisePropertyChanged(nameof(IsLabelInvalid));
      }
    }
  }
  private string? _labelError;

  public GeneralStatus LabelStatus {
    get => IsLabelValid ? GeneralStatus.OK : GeneralStatus.Error;
  }

  public string LabelStatusIcon {
    get => IsLabelValid ? "CheckCircle" : "CloseCircle";
  }

  public bool IsLabelValid {
    get => String.IsNullOrEmpty(LabelError);
  }

  public bool IsLabelInvalid {
    get => !IsLabelValid;
  }

  public string Tags {
    get => _tags;
    set {
      if(SetInstanceProperty(ref _tags, value))
      {
        TagsError = DescribeTagsError(_tags);
      }
    }
  }
  private string _tags = String.Empty;
  private string _originalTags = String.Empty;

  public static string? DescribeTagsError(string tagsText)
  {
    var tags = tagsText.Split().Where(tag => !String.IsNullOrEmpty(tag));
    foreach(var tag in tags)
    {
      var error = EntryTag.DescribeInvalidTag(tag);
      if(error != null)
      {
        return error;
      }
    }
    var testSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    foreach(var tag in tags)
    {
      var tag2 = tag.StartsWith('?') ? tag[1..] : tag;
      if(!testSet.Add(tag2))
      {
        return $"Duplicate tag: '{tag}'";
      }
    }
    return null;
  }

  public string? TagsError {
    get => _tagsError;
    private set {
      if(SetNullableInstanceProperty(ref _tagsError, value))
      {
        RaisePropertyChanged(nameof(AreTagsValid));
        RaisePropertyChanged(nameof(TagsStatus));
        RaisePropertyChanged(nameof(TagsStatusIcon));
        RaisePropertyChanged(nameof(AreTagsInvalid));
      }
    }
  }
  private string? _tagsError;

  public GeneralStatus TagsStatus {
    get => AreTagsValid ? GeneralStatus.OK : GeneralStatus.Error;
  }

  public string TagsStatusIcon {
    get => AreTagsValid ? "CheckCircle" : "CloseCircle";
  }

  public bool AreTagsValid {
    get => String.IsNullOrEmpty(TagsError);
  }

  public bool AreTagsInvalid {
    get => !AreTagsValid;
  }

  public ObservableCollection<BlockEditViewModel> Blocks { get; }

  [Obsolete("'PrimaryContent' is to be phased out")]
  public string PrimaryContent {
    get => _primaryContent;
    set {
      if(SetInstanceProperty(ref _primaryContent, value))
      {
      }
    }
  }
  private string _primaryContent = String.Empty;

  public bool IsValid {
    get => IsLabelValid && AreTagsValid;
  }

  /// <summary>
  /// Apply changes in case of editing an existing entry.
  /// This Edit Model should not be used after this call.
  /// </summary>
  public void ApplyEdit()
  {
    if(!IsValid)
    {
      throw new InvalidOperationException("Cannot apply edit to invalid entry");
    }
    if(Original == null)
    {
      throw new InvalidOperationException("Cannot apply edit to new entry");
    }

    var content = Original.Content;

    var tags = Tags.Split().Where(tag => !String.IsNullOrEmpty(tag)); // split on whitespace
    var tagSet = new TagSet();
    foreach(var tag in tags)
    {
      if(!String.IsNullOrEmpty(tag))
      {
        if(!tagSet.TryPut(tag))
        {
          throw new InvalidOperationException(
            $"Invalid tag in tag set: '{tag}'");
        }
      }
    }

    // Note that the ViewModel is designed mostly as read-only view of the
    // content; updating the content and the ViewModel is largely separate.

    content.ChangeLabel(Label); // viewmodel is updated below

    // The hard part of applying tag changes is to not accidentally mark the
    // entry as modified when it isn't.

    var tagsText = Tags.Trim();
    if(tagsText != _originalTags) // else: tags were not modified
    {
      // Tags WERE modified. No need to be careful with the content update
      _originalTags = tagsText;
      content.ClearTags(); // side effect: this WILL mark the entry as modified
      foreach(var tag in tags)
      {
        content.AddTag(tag);
      }

      // The ViewModel tags are easier: just replace the whole set.
      Original.Tags = tagSet;
      Original.RecalculateVisibleTags();
    }

    var newBlocks =
      from block in Blocks
      let newBlock = block.AcceptEdit()
      where newBlock != null
      select newBlock;
    Original.Content.ReplaceBlocks(newBlocks);

    Original.SyncBlocks();

    // Changes the ViewModel label (model was already changed).
    // Also may change parent sort order.
    Original.Label = Label;
    Original.PostEditNotification();

    Owner.Sections.SyncBoth();
  }

  /// <summary>
  /// Apply the content of this object to a brand new entry.
  /// This Edit Model should not be used after this call.
  /// </summary>
  public void CreateNewEntry()
  {
    if(!IsValid)
    {
      throw new InvalidOperationException("Cannot create an invalid entry");
    }
    if(Original != null)
    {
      throw new InvalidOperationException("Expecting an Edit, not a Create");
    }
    var cryptor = Owner.Owner.FindKey();
    if(cryptor == null)
    {
      throw new InvalidOperationException("No key found for the new entry");
    }

    var tags =
      Tags.Split()
      .Select(tag => tag.Trim())
      .Where(tag => !String.IsNullOrEmpty(tag));

    var newBlocks =
      from block in Blocks
      let newBlock = block.AcceptEdit()
      where newBlock != null
      select newBlock;
    var entryContent = new EntryContent(Label, tags, newBlocks);
    entryContent.Modified = true;
    var parentId = ParentChunkId;
    var fileId = Owner.Owner.FileId;
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
    var evm = Owner.AddNewEntry(entryContent, Parent);
    if(Parent != null)
    { 
      Parent.IsExpanded = true;
    }
  }

  // --------
}
