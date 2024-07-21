/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Model.TreeMath;

namespace Keybag3.Main.KeybagContent;

public enum TagMatchResult
{
  NoMatch,
  MatchNegative,
  MatchPositive,
}

/// <summary>
/// Prepared search for tags
/// </summary>
public class TagSearch
{
  private readonly HashSet<string> _positives;
  private readonly HashSet<string> _negatives;
  private static readonly Regex _tagRegex =
    new Regex(@"^\??([-+])?\??([^-+?\s][^\s]*)$");

  private TagSearch()
  {
    _positives = new HashSet<string>(
      StringComparer.InvariantCultureIgnoreCase);
    _negatives = new HashSet<string>(
      StringComparer.InvariantCultureIgnoreCase);
  }

  public static TagSearch? ParseList(string tagList)
  {
    var result = new TagSearch();
    foreach(var tag in tagList.Split().Where(s => !String.IsNullOrEmpty(s)))
    {
      if(!_tagRegex.IsMatch(tag))
      {
        return null;
      }
      var match = _tagRegex.Match(tag);
      var isNegative = match.Groups[1].Success && match.Groups[1].Value == "-";
      var tagValue = match.Groups[2].Value;
      if(isNegative)
      {
        result._negatives.Add(tagValue);
      }
      else
      {
        result._positives.Add(tagValue);
      }
    }
    return result;
  }

  public static bool IsValidSingleTag(string tag)
  {
    return _tagRegex.IsMatch(tag);
  }

  public static bool IsValidTagList(string tagList)
  {
    foreach(var tag in tagList.Split().Where(s => !String.IsNullOrEmpty(s)))
    {
      if(!IsValidSingleTag(tag))
      {
        return false;
      }
    }
    return true;
  }

  public TagMatchResult EntryMatch(EntryViewModel evm)
  {
    var positive = false;
    foreach(var tag in evm.LogicalTags.All)
    {
      if(_negatives.Contains(tag.PureTag) || _negatives.Contains(tag.Key))
      {
        return TagMatchResult.MatchNegative;
      }
      if(_positives.Contains(tag.PureTag) || _positives.Contains(tag.Key))
      {
        positive = true;
      }
    }
    return positive ? TagMatchResult.MatchPositive : TagMatchResult.NoMatch;
  }

  /// <summary>
  /// Walk the entry tree and find which entries match this search.
  /// </summary>
  /// <param name="space">
  /// The space to search
  /// </param>
  /// <param name="matches">
  /// All entries that are to be considered a direct or indirect match
  /// </param>
  /// <param name="hits">
  /// All direct matches
  /// </param>
  /// <param name="stops">
  /// Anti-matches, where the search recursion was stopped
  /// </param>
  public void MatchTree(
    KeybagViewModel keybag,
    out ChunkSet<EntryViewModel> hits,
    out ChunkSet<EntryViewModel> stops)
  {
    var space = keybag.EntrySpace;
    hits = space.CreateSet();
    stops = space.CreateSet();
    foreach(var evm in space.Roots)
    {
      var matchResult = EntryMatch(evm);
      switch(matchResult)
      {
        case TagMatchResult.MatchNegative:
          stops.Add(evm.NodeId);
          // do not recurse
          break;
        case TagMatchResult.MatchPositive:
          hits.Add(evm.NodeId);
          // recurse
          MatchSubTree(true, evm.ChildSet(), hits, stops);
          break;
        case TagMatchResult.NoMatch:
          // recurse anyway
          MatchSubTree(false, evm.ChildSet(), hits, stops);
          break;
      }
    }
  }

  private void MatchSubTree(
    bool hadMatch,
    IEnumerable<ChunkId> chunks,
    ChunkSet<EntryViewModel> hits,
    ChunkSet<EntryViewModel> stops)
  {
    var space = hits.Space;
    foreach(var childId in chunks)
    {
      var child = space[childId];
      var matchResult = EntryMatch(child);
      switch(matchResult)
      {
        case TagMatchResult.MatchNegative:
          stops.Add(child.NodeId);
          // do not recurse
          break;
        case TagMatchResult.MatchPositive:
          hits.Add(child.NodeId);
          // recurse
          MatchSubTree(true, child.ChildSet(), hits, stops);
          break;
        case TagMatchResult.NoMatch:
          // recurse
          MatchSubTree(hadMatch, child.ChildSet(), hits, stops);
          break;
      }
    }
  }

  public IReadOnlySet<string> Positives { get => _positives; }

  public IReadOnlySet<string> Negatives { get => _negatives; }

}
