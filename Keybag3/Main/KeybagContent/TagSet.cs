/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Keybag3.Main.KeybagContent;

public class TagSet
{
  private readonly Dictionary<string, TagModel> _tags;

  public TagSet()
  {
    _tags = new Dictionary<string, TagModel>(StringComparer.InvariantCultureIgnoreCase);
  }

  public void Put(TagModel tagModel)
  {
    _tags[tagModel.PureTag] = tagModel;
  }

  public bool TryPut(string tag)
  {
    var tagModel = TagModel.TryFrom(tag);
    if(tagModel != null)
    {
      Put(tagModel);
      return true;
    }
    else
    {
      return false;
    }
  }

  public IReadOnlyCollection<TagModel> All => _tags.Values;

  /// <summary>
  /// The section memberships declared explicitly in these tags.
  /// </summary>
  public IEnumerable<string> KeySectionNames {
    get =>
      from tag in _tags.Values
      where tag.Section != null
      select tag.Section;
  }

  /// <summary>
  /// Find a matching entry for the given tag. If <paramref name="exact"/>
  /// is true, only exactly matching tags are considered (based on
  /// <see cref="TagModel.PureTag"/>, case insensitive). If false, also
  /// matches for just <see cref="TagModel.Key"/> are considered.
  /// If <paramref name="exact"/> is null, it is treated as equivalent
  /// to the value of <see cref="TagModel.HasValue"/> of <paramref name="tagModel"/>.
  /// </summary>
  /// <param name="tagModel">
  /// The tag query to compare against
  /// </param>
  /// <param name="exact">
  /// See description above
  /// </param>
  /// <returns></returns>
  public TagModel? FindMatch(TagModel tagModel, bool? exact = null)
  {
    if(exact == null)
    {
      exact = tagModel.HasValue;
    }
    if(exact.Value)
    {
      return _tags.TryGetValue(tagModel.PureTag, out var result) ? result : null;
    }
    else
    {
      var exactMatch = _tags.TryGetValue(tagModel.PureTag, out var result) ? result : null;
      if(exactMatch != null)
      {
        return exactMatch;
      }
      return _tags.Values.FirstOrDefault(
        t => String.Equals(
          t.Key, tagModel.Key, StringComparison.InvariantCultureIgnoreCase));
    }
  }

  public void AddAll(TagSet source)
  {
    foreach(var tagModel in source.All)
    {
      Put(tagModel);
    }
  }

  public static TagSet Union(params TagSet[] sets)
  {
    var result = new TagSet();
    foreach(var set in sets)
    {
      result.AddAll(set);
    }
    return result;
  }

  // ------
}
