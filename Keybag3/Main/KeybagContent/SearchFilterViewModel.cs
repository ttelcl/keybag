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
using System.Windows;
using System.Windows.Input;

using Keybag3.WpfUtilities;

using Lcl.KeyBag3.Model.Contents.Blocks;
using Lcl.KeyBag3.Model.TreeMath;

namespace Keybag3.Main.KeybagContent;

public enum SearchKind
{
  Tag,
  Content,
  Regex,
}

public class SearchFilterViewModel: ViewModelBase<KeybagViewModel>
{
  public SearchFilterViewModel(KeybagViewModel kvm) : base(kvm)
  {
    _searchKind = SearchKind.Tag;
    SearchByTagCommand = new DelegateCommand(
      p => { SearchByTag(); },
      p => TagSearch.IsValidTagList(SearchText));
    SearchByContentCommand = new DelegateCommand(
      p => { SearchByContent(); },
      p => true);
    SearchByRegexCommand = new DelegateCommand(
      p => { SearchByRegex(); },
      p => CanSearchByRegex());
  }

  public ICommand SearchCommand {
    get => SearchKind switch {
      SearchKind.Tag => SearchByTagCommand,
      SearchKind.Content => SearchByContentCommand,
      SearchKind.Regex => SearchByRegexCommand,
      _ => throw new InvalidOperationException("Invalid SearchKind"),
    };
  }

  public SearchKind SearchKind {
    get => _searchKind;
    set {
      if(SetValueProperty(ref _searchKind, value))
      {
        Trace.TraceInformation($"SearchKind={value}");
        RaisePropertyChanged(nameof(SearchLabel));
        RaisePropertyChanged(nameof(SearchHelp));
        RaisePropertyChanged(nameof(SearchCommand));
        RaisePropertyChanged(nameof(SearchIcon));
      }
    }
  }
  private SearchKind _searchKind;

  public string SearchLabel => SearchClear
    ? "Reset Results"
    : SearchKind switch {
      SearchKind.Tag => "Search Tags",
      SearchKind.Content => "Search Content",
      SearchKind.Regex => "Search Regex",
      _ => throw new InvalidOperationException("Invalid SearchKind"),
    };

  public string SearchHelp => SearchKind switch {
    SearchKind.Tag => "Enter tags to find ('-tag' to block)",
    SearchKind.Content => "Enter a text fragment to find in content",
    SearchKind.Regex => "Enter a RegEx to find in content",
    _ => throw new InvalidOperationException("Invalid SearchKind"),
  };

  public string SearchIcon => SearchClear
    ? "MagnifyClose"
    : SearchKind switch {
      SearchKind.Tag => "TagMultiple",
      SearchKind.Content => "TextBoxSearch",
      SearchKind.Regex => "Regex",
      _ => throw new InvalidOperationException("Invalid SearchKind"),
    };

  public string SearchText {
    get => _searchText;
    set {
      if(SetValueProperty(ref _searchText, value))
      {
        SearchClear = String.IsNullOrWhiteSpace(_searchText);
      }
    }
  }
  private string _searchText = "";

  /// <summary>
  /// Shadows the model's IgnoreScope property
  /// </summary>
  public bool IgnoreScope {
    get => Model.IgnoreScope;
    set {
      var old = Model.IgnoreScope;
      Model.IgnoreScope = value;
      if(CheckValueProperty(old, value))
      {
      }
    }
  }

  public bool SearchClear {
    get => _searchClear;
    private set {
      if(SetValueProperty(ref _searchClear, value))
      {
        RaisePropertyChanged(nameof(SearchLabel));
        RaisePropertyChanged(nameof(SearchIcon));
        if(value)
        {
          // actually cleared the filter
          Model.RecalculateMatches();
        }
      }
    }
  }
  private bool _searchClear = true;

  public ICommand SearchByTagCommand { get; }

  private void SearchByTag()
  {
    // Trace.TraceInformation($"Search By Tag text={SearchText}");
    Model.RecalculateMatches();
  }

  public ICommand SearchByContentCommand { get; }

  private void SearchByContent()
  {
    // Trace.TraceInformation($"Search By Content text={SearchText}");
    Model.RecalculateMatches();
  }

  public ICommand SearchByRegexCommand { get; }

  private void SearchByRegex()
  {
    // Trace.TraceInformation($"Search By Regex text={SearchText}");
    Model.RecalculateMatches();
  }

  private bool CanSearchByRegex()
  {
    var regex = SearchText.Trim();
    if(String.IsNullOrWhiteSpace(regex))
    {
      // Special case: empty search text is valid because
      // it morphs into a search result reset
      return true;
    }
    try
    {
      var _ = new Regex(regex, RegexOptions.None, TimeSpan.FromMilliseconds(500));
      return true;
    }
    catch(Exception)
    {
      return false;
    }
  }

  public ChunkMapping<SearchOutcome> RunSearch()
  {
    Model.Owner.Owner.AppModel.StatusMessage = "";
    ChunkMapping<SearchOutcome> result;
    var time0 = DateTime.UtcNow;
    if(SearchClear)
    {
      result = Model.BuildClearResult();
    }
    else
    {
      result = SearchKind switch {
        SearchKind.Tag => RunTagSearch(),
        SearchKind.Content => RunContentSearch(),
        SearchKind.Regex => RunRegexSearch(),
        _ => throw new InvalidOperationException("Invalid SearchKind"),
      };
    }
    var time1 = DateTime.UtcNow;
    var name = SearchClear ? "Clear" : SearchKind.ToString();
    Trace.TraceInformation(
      $"Search '{name}' took {(time1 - time0).TotalMilliseconds} ms");
    return result;
  }

  private ChunkMapping<SearchOutcome> RunRegexSearch()
  {
    //Trace.TraceInformation($"Search By Regex");
    var hits = Model.EntrySpace.CreateSet();
    var blocks = Model.EntrySpace.CreateSet(); // will stay empty

    try
    {
      var regex = new Regex(SearchText,
        RegexOptions.None,
        TimeSpan.FromMilliseconds(800));
      foreach(var entry in Model.EntrySpace.All)
      {
        if(regex.IsMatch(entry.Label))
        {
          hits.Add(entry.NodeId);
        }
        else
        {
          if(entry.Content.Blocks.Any(block =>
            block is PlainEntryBlock plainBlock
            && regex.IsMatch(plainBlock.Text)))
          {
            hits.Add(entry.NodeId);
          }
        }
      }
      return Model.BuildSearchResult(hits, blocks);
    }
    catch(Exception)
    {
      MessageBox.Show($"Invalid RegEx");
      return Model.BuildClearResult();
    }
  }

  private ChunkMapping<SearchOutcome> RunContentSearch()
  {
    //Trace.TraceInformation($"Search By Content");
    var hits = Model.EntrySpace.CreateSet();
    var blocks = Model.EntrySpace.CreateSet(); // will stay empty

    var search = SearchText.Trim();
    foreach(var entry in Model.EntrySpace.All)
    {
      // For now: only check plaintext blocks and label.
      if(entry.Label.Contains(
        search, StringComparison.InvariantCultureIgnoreCase))
      {
        hits.Add(entry.NodeId);
      }
      else
      {
        if(entry.Content.Blocks.Any(block =>
          block is PlainEntryBlock plainBlock
          && plainBlock.Text.Contains(
            search, StringComparison.InvariantCultureIgnoreCase)))
        {
          hits.Add(entry.NodeId);
        }
      }
    }
    return Model.BuildSearchResult(hits, blocks);
  }

  private ChunkMapping<SearchOutcome> RunTagSearch()
  {
    //Trace.TraceInformation($"Search By Tag");
    var search = TagSearch.ParseList(SearchText);
    if(search == null)
    {
      MessageBox.Show("Invalid search");
      return Model.BuildClearResult();
    }
    search.MatchTree(Model, out var hits, out var stops);
    return Model.BuildSearchResult(hits, stops);
  }
}
