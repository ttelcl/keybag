/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Tags;

/// <summary>
/// Handling of a tag related to synchronizing a KB3 entry
/// with a KB2 entry (carrying multiple child values)
/// </summary>
public class Kb2Tag
{
  /// <summary>
  /// Create a new Kb2TagGroup
  /// </summary>
  private Kb2Tag(
    string context,
    string? source)
  {
    Kb2Attached = true;
    Context = context;
    Source = source;
  }

  /// <summary>
  /// Check if the KB3 entry tag string is a valid "kb2" tag for the
  /// specified <paramref name="context"/> (file ID) and parse it if it is
  /// </summary>
  /// <param name="tagText">
  /// The text of the entry tag
  /// </param>
  /// <param name="context">
  /// The context to match (the 8 character hexadecimal KB2 file ID)
  /// </param>
  public static Kb2Tag? TryFrom(string tagText, string context)
  {
    var tag = ContextTag.TryParse(
      tagText, exactName: "kb2", requireValue: true, exactContext: context);
    if(tag != null && !tag.HasField && tag.HasValue)
    {
      var kb2tag = new Kb2Tag(context, tagText);
      return kb2tag.TryPut(tag) ? kb2tag : null;
    }
    return null;
  }

  /// <summary>
  /// Check if the KB3 entry tag string is a valid "kb2" tag for
  /// ANY context (file ID) and parse it if it is
  /// </summary>
  /// <param name="tagText">
  /// The text of the entry tag
  /// </param>
  public static Kb2Tag? TryFromGeneral(string tagText)
  {
    var tag = ContextTag.TryParse(
      tagText, exactName: "kb2", requireValue: true, requireContext: true);
    if(tag != null && !tag.HasField && tag.HasValue)
    {
      var kb2tag = new Kb2Tag(tag.Context, tagText);
      return kb2tag.TryPut(tag) ? kb2tag : null;
    }
    return null;
  }

  /// <summary>
  /// Create a brand new <see cref="Kb2Tag"/>
  /// </summary>
  /// <param name="context">
  /// The context (KB2 file ID in hex form)
  /// </param>
  /// <param name="entryId">
  /// The KB2 entry ID
  /// </param>
  /// <param name="editId">
  /// The KB2 edit ID
  /// </param>
  /// <returns></returns>
  public static Kb2Tag Create(string context, uint entryId, ulong editId)
  {
    var kb2tag = new Kb2Tag(context, null);
    kb2tag.Update(entryId, editId);
    return kb2tag;
  }

  /// <summary>
  /// Insert the content of <paramref name="tag"/>
  /// if it matches this group's context (==fileId) and name (=="kb2")
  /// and does not have a field name.
  /// </summary>
  private bool TryPut(ContextTag? tag)
  {
    if(tag != null && tag.Name=="kb2" && !tag.HasField && tag.HasValue && tag.ContextIs(Context))
    {
      var text = tag.Value ?? "";
      var parts = text.Split(':');
      if(parts.Length == 3)
      {
        var attached = Parse32(parts[0]);
        var entryId = Parse32(parts[1]);
        var editId = Parse64(parts[2]);
        if(attached.HasValue && entryId.HasValue && editId.HasValue)
        {
          Kb2Attached = attached.Value != 0;
          Kb2EntryId = entryId;
          Kb2EditId = editId;
          return true;
        }
      }
    }
    return false;
  }

  /// <summary>
  /// Return the serialized tag value for this object, if sufficient
  /// information is present. Return null if not.
  /// </summary>
  public string? TryGetTag()
  {
    if(Kb2EntryId.HasValue && Kb2EditId.HasValue)
    {
      var tag = new ContextTag(
        true,
        "kb2",
        Context,
        "",
        FormatValue()
      );
      return tag.ToString();
    }
    return null;
  }

  /// <summary>
  /// Format the value of the KB2 tag from its parts
  /// </summary>
  public string FormatValue()
  {
    string[] parts = [
      Kb2Attached ? "1" : "0",
      Kb2EntryId?.ToString("X8") ?? "",
      Kb2EditId?.ToString("X16") ?? ""
    ];
    return String.Join(":", parts);
  }

  /// <summary>
  /// The original tag text this <see cref="Kb2Tag"/> was parsed from
  /// (null if it wasn't parsed from an existing tag)
  /// </summary>
  public string? Source { get; }

  /// <summary>
  /// Get the tag group's "context" - the 8 character HEX representation
  /// of the KB2 file ID
  /// </summary>
  public string Context { get; }

  /// <summary>
  /// Get the flag that indicates if the KB3 entry is still
  /// attached to the KB2 source. Initially true, use
  /// <see cref="Detach"/> to set to false.
  /// </summary>
  public bool Kb2Attached { get; private set; }

  /// <summary>
  /// Get the KB2 entry ID for the associated KB3 entry.
  /// Initialized using <see cref="Update(uint, ulong)"/>
  /// (or <see cref="TryFrom(string, string)"/>)
  /// </summary>
  public uint? Kb2EntryId { get; private set; }

  /// <summary>
  /// Get or set the KB2 edit ID for the associated KB3 entry
  /// (the last synced edit if in "attached" mode).
  /// Updated using <see cref="Update(uint, ulong)"/>
  /// (or <see cref="TryFrom(string, string)"/>)
  /// </summary>
  public ulong? Kb2EditId { get; private set; }

  /// <summary>
  /// Change to detached mode. Once in detached mode,
  /// <see cref="Kb2EntryId"/> and <see cref="Kb2EditId"/>
  /// can no longer be modified
  /// </summary>
  public void Detach()
  {
    Kb2Attached = false;
  }

  /// <summary>
  /// Update <see cref="Kb2EntryId"/> if not yet set and
  /// update <see cref="Kb2EditId"/>. This call fails if
  /// <see cref="Kb2Attached"/> is false.
  /// </summary>
  /// <param name="entryId">
  /// The entry ID to initialize, or to verify. Once set
  /// the entry ID cannot be changed.
  /// </param>
  /// <param name="editId">
  /// The new edit ID.
  /// </param>
  public void Update(uint entryId, ulong editId)
  {
    if(!Kb2Attached)
    {
      throw new InvalidOperationException(
        "Cannot update after detaching");
    }
    if(Kb2EntryId.HasValue && Kb2EntryId.Value != entryId)
    {
      throw new InvalidOperationException(
        "Cannot modify entry ID once set");
    }
    Kb2EntryId = entryId;
    Kb2EditId = editId;
  }

  private static uint? Parse32(string? text)
  {
    if(String.IsNullOrEmpty(text))
    {
      return null;
    }
    return UInt32.TryParse(
      text, NumberStyles.HexNumber,
      CultureInfo.InvariantCulture, out var value)
      ? value : null;
  }

  private static ulong? Parse64(string? text)
  {
    if(String.IsNullOrEmpty(text))
    {
      return null;
    }
    return UInt64.TryParse(
      text, NumberStyles.HexNumber,
      CultureInfo.InvariantCulture, out var value)
      ? value : null;
  }

}
