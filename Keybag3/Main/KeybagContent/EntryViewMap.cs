/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Lcl.KeyBag3.Model;

namespace Keybag3.Main.KeybagContent;

/// <summary>
/// Mapping of node IDs to <see cref="EntryViewModel"/>s
/// </summary>
public class EntryViewMap
{
  private Dictionary<ChunkId, EntryViewModel> _mapping;

  public EntryViewMap()
  {
    _mapping = [];
  }

  public EntryViewModel? Find(ChunkId nodeId)
  {
    return _mapping.TryGetValue(nodeId, out var entry) ? entry : null;
  }

  public void Put(EntryViewModel entry)
  {
    _mapping[entry.NodeId] = entry;
  }

  public IReadOnlyCollection<EntryViewModel> EntryViews => _mapping.Values;

  public void PutMany(IEnumerable<EntryViewModel> entries)
  {
    foreach(var entry in entries)
    {
      Put(entry);
    }
  }
}
