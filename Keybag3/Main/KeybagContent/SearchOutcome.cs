/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keybag3.Main.KeybagContent;

/// <summary>
/// The effect of a search operation on an entry.
/// </summary>
public enum SearchOutcome
{
  /// <summary>
  /// No search was active (all entries visible).
  /// </summary>
  NoSearch,

  /// <summary>
  /// The search matched the entry exactly.
  /// </summary>
  Hit,

  /// <summary>
  /// Not an exact match, but at least one descendent entry matched.
  /// i.e.: this is an ancestor of a match.
  /// </summary>
  Support,

  /// <summary>
  /// Not an exact match, and no descendent entry matched, but
  /// an ancestor did.
  /// i.e.: this is a descendent of a match.
  /// </summary>
  Indirect,

  /// <summary>
  /// An anti-match. This entry and descendents are excluded
  /// </summary>
  Blocker,

  /// <summary>
  /// An ancestor is a <see cref="Blocker"/>.
  /// </summary>
  Blocked,

  /// <summary>
  /// Not a match, and no ancestor or descendent matched.
  /// </summary>
  NoMatch,
}
