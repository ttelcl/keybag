/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Keybag3.Main.Database;
using Keybag3.Main.KeybagContent;

namespace Keybag3.Main;

/// <summary>
/// Names for message channels
/// </summary>
public static class MessageChannels
{
  /// <summary>
  /// The channel for scope filter changes, hosted in
  /// <see cref="KeybagViewModel"/>
  /// </summary>
  public const string ScopeFilterChanged = "scope-filter-changed";

  /// <summary>
  /// The channel for auto-hide timer changes, hosted in
  /// <see cref="KeybagSetViewModel"/>
  /// </summary>
  public const string AutoHideTimerChanged = "auto-hide-timer-changed";
}
