/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keybag3.Main;

/// <summary>
/// A ViewModel that exposes a user understandable name for itself
/// </summary>
public interface IHasViewTitle
{
  string Title { get; }
}

