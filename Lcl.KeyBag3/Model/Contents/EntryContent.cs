/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model.Contents.Blocks;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Description of EntryContent
/// </summary>
public class EntryContent: ContentBase
{
  private readonly HashSet<string> _tags;
  private readonly List<EntryBlock> _blocks;

  /// <summary>
  /// Create a new EntryContent
  /// </summary>
  public EntryContent(
    string label,
    IEnumerable<string> tags,
    IEnumerable<EntryBlock> blocks)
  {
    _tags = new HashSet<string>(tags, StringComparer.InvariantCultureIgnoreCase);
    Tags = _tags;
    _blocks = [];
    _blocks.AddRange(blocks);
    Blocks = _blocks.AsReadOnly();
    Label = label;
    if(!IsValidLabel(label))
    {
      throw new ArgumentException(
        $"Not a valid entry label: '{label}'");
    }
  }

  /// <summary>
  /// Create an <see cref="EntryContent"/> from a content model
  /// </summary>
  public static EntryContent FromModel(ContentSlice model)
  {
    switch(model.Tag)
    {
      case '!':
        var slices = model.Split(Ascii.FS);
        return FromSegments(slices);
      default:
        throw new InvalidDataException(
          $"Unrecognized entry content tag '{model.Tag}'");
    }
  }

  private static EntryContent FromSegments(IReadOnlyList<ContentSlice> slices)
  {
    var label = "";
    var tags = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    var blocks = new List<EntryBlock>();
    foreach(var slice in slices)
    {
      switch(slice.Tag)
      {
        case 'L':
          label = slice.AsString;
          break;
        case 'T':
          var tagSlices = slice.Split(Ascii.LF);
          foreach(var tagSlice in tagSlices)
          {
            var tag = tagSlice.AsString.Replace(' ', '_');
            if(!String.IsNullOrEmpty(tag))
            {
              tags.Add(tag);
            }
          }
          break;
        case '=':
          var block = new PlainEntryBlock(slice);
          blocks.Add(block);
          break;
        default:
          // assume an not-yet-supported entry block
          blocks.Add(new UnrecognizedBlock(slice));
          break;
      }
    }
    var entry = new EntryContent(label, tags, blocks);
    entry.Modified = false;
    return entry;
  }

  /// <summary>
  /// The label for the entry
  /// </summary>
  public string Label { get; private set; }

  /// <summary>
  /// Test if the given string is valid as a label. To be valid
  /// the string must not be empty, not have leading or trailing whitespace
  /// not have an consecutive whitespace characters, not have any "/"
  /// character and not have any whitespace other than spaces.
  /// </summary>
  /// <param name="label">
  /// The string to test
  /// </param>
  /// <returns>
  /// True if the requirements are met: 
  /// </returns>
  public static bool IsValidLabel(string label)
  {
    if(String.IsNullOrWhiteSpace(label))
    {
      return false;
    }
    if(label.Contains('/'))
    {
      return false;
    }
    var label2 = Regex.Replace(label, @"\s+", " ").Trim();
    if(label2 != label)
    {
      // - no whitespace other than space
      // - no leading or trailing whitespace
      // - no multiple consecutive whitespace characters
      return false;
    }
    return true;
  }

  /// <summary>
  /// Describe why the given label is not valid, or return null
  /// if it is valid.
  /// </summary>
  public static string? DescribeLabelError(string label)
  {
    if(String.IsNullOrWhiteSpace(label))
    {
      return "Label cannot be empty";
    }
    if(label.Contains('/'))
    {
      return "Label cannot contain the character '/'";
    }
    var label2 = Regex.Replace(label, @"\s+", " ").Trim();
    if(label2 != label)
    {
      return "Label cannot have leading or trailing whitespace or multiple consecutive spaces";
    }
    return null;
  }

  /// <summary>
  /// The explicitly defined tags for this entry.
  /// These are defined as case-insensitive
  /// </summary>
  public IReadOnlySet<string> Tags { get; }

  /// <summary>
  /// The list of content blocks, possibly empty
  /// </summary>
  public IReadOnlyList<EntryBlock> Blocks { get; }

  /// <summary>
  /// Find a block by its in-memory ID (<see cref="EntryBlock.VolatileGuid"/>)
  /// </summary>
  /// <returns>
  /// The block if found, or null if not found
  /// </returns>
  public EntryBlock? FindBlock(Guid id)
  {
    return Blocks.FirstOrDefault(b => b.VolatileGuid == id);
  }

  /// <summary>
  /// Remove a block from the entry
  /// </summary>
  public bool RemoveBlock(EntryBlock block)
  {
    if(_blocks.Remove(block))
    {
      Modified = true;
      return true;
    }
    return false;
  }

  /// <summary>
  /// Insert a block after the given reference block (or at the top
  /// if the reference is null or not found)
  /// </summary>
  /// <param name="block">
  /// The block to insert
  /// </param>
  /// <param name="after">
  /// The block after which to insert the new block, or null to insert at the top
  /// </param>
  /// <returns></returns>
  public void InsertBlock(EntryBlock block, EntryBlock? after)
  {
    var index = after == null ? -1 : _blocks.IndexOf(after);
    _blocks.Insert(index + 1, block);
    Modified = true;
  }

  /// <summary>
  /// Append a block to the entry
  /// </summary>
  public void AppendBlock(EntryBlock block)
  {
    _blocks.Add(block);
    Modified = true;
  }

  /// <summary>
  /// Replace all blocks with the given list of new blocks
  /// (some of the new blocks may be reused existing blocks, not strictly
  /// "new")
  /// </summary>
  /// <param name="newBlocks">
  /// The new list of blocks.
  /// </param>
  public void ReplaceBlocks(IEnumerable<EntryBlock> newBlocks)
  {
    _blocks.Clear();
    _blocks.AddRange(newBlocks);
    Modified = true;
  }

  /// <summary>
  /// Add a tag.
  /// </summary>
  /// <param name="tag">
  /// The tag to add
  /// </param>
  /// <returns>
  /// True if the tag was added, false if it was already present (case insensitively)
  /// </returns>
  public bool AddTag(string tag)
  {
    if(String.IsNullOrEmpty(tag))
    {
      throw new ArgumentOutOfRangeException(
        nameof(tag), "Tags cannot be empty");
    }
    if(!EntryTag.IsValidTag(tag))
    {
      throw new ArgumentOutOfRangeException(
        nameof(tag),
        "tags cannot contain whitespace characters or be the string '?' or start with '-' or '+'");
    }
    var added = _tags.Add(tag);
    Modified |= added;
    return added;
  }

  /// <summary>
  /// Remove a tag
  /// </summary>
  /// <param name="tag">
  /// The tag to remove
  /// </param>
  /// <returns>
  /// True if the tag was found (case insensitive) and removed.
  /// False if it wasn't found
  /// </returns>
  public bool RemoveTag(string tag)
  {
    var removed = _tags.Remove(tag);
    Modified |= removed;
    return removed;
  }

  /// <summary>
  /// Remove <paramref name="oldTag"/> and add <paramref name="newTag"/>
  /// (but skipping the operation if they are equal)
  /// </summary>
  /// <param name="oldTag">
  /// The tag to remove (or null to not remove anything)
  /// </param>
  /// <param name="newTag">
  /// The tag to add (or null to not add anything)
  /// </param>
  /// <returns>
  /// True if any change was made
  /// </returns>
  public bool ReplaceTag(string? oldTag, string? newTag)
  {
    if(oldTag == newTag)
    {
      return false;
    }
    var modified = false;
    if(!String.IsNullOrEmpty(oldTag))
    {
      modified = RemoveTag(oldTag) || modified;
    }
    if(!String.IsNullOrEmpty(newTag))
    {
      modified = AddTag(newTag) || modified;
    }
    return modified;
  }

  /// <summary>
  /// Remove all tags
  /// </summary>
  public void ClearTags()
  {
    if(_tags.Count > 0)
    {
      _tags.Clear();
      Modified = true;
    }
  }

  /// <summary>
  /// Change the label text
  /// </summary>
  /// <param name="label">
  /// The new label. Cannot be empty. Cannot contain any '/' characters.
  /// Illegal whitespace is silently replaced by legal whitespace.
  /// </param>
  /// <returns>
  /// True if the label changed, false if it was the same as before.
  /// </returns>
  public bool ChangeLabel(string label)
  {
    label = Regex.Replace(label, @"\s+", " ").Trim();
    if(String.IsNullOrEmpty(label))
    {
      throw new ArgumentException(
        "Entry label cannot be empty");
    }
    if(label.Contains('/'))
    {
      throw new ArgumentException(
        "Entry labels cannot contain the character '/'");
    }
    if(!IsValidLabel(label))
    {
      throw new ArgumentException(
        $"Invalid entry label '{label}'");
    }
    var changed = !Label.Equals(label);
    if(changed)
    {
      Label = label;
      Modified = true;
      return true;
    }
    else
    {
      return false;
    }
  }

  /// <summary>
  /// Serialize this entry into the given buffer. Existing content for the
  /// buffer is destroyed.
  /// </summary>
  public void Serialize(ContentBuilder buildBuffer)
  {
    using(var root = buildBuffer.StartBuilding('!', Ascii.FS))
    {
      root.AppendLeaf(Label, 'L');
      if(Tags.Count > 0)
      {
        using(var segment = root.StartChildSegment('T', '\n'))
        {
          foreach(var tag in Tags)
          {
            segment.AppendLeaf(tag);
          }
        }
      }
      foreach(var block in Blocks)
      {
        if(block is not PlainEntryBlock plainEntryBlock
          || !String.IsNullOrEmpty(plainEntryBlock.Text))
        {
          block.AppendAsChild(root);
        }
      }
    }
  }

}
