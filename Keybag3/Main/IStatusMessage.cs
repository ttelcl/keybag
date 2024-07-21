/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keybag3.Main;

/// <summary>
/// Abstraction of the status bar message implemented by the
/// top level window, to be passed to child controls
/// </summary>
public interface IStatusMessage
{
  string StatusMessage { get; set; }
}

