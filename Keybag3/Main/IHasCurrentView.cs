/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Keybag3.WpfUtilities;

namespace Keybag3.Main;

/// <summary>
/// An object that has a current view
/// </summary>
public interface IHasCurrentView
{
  /// <summary>
  /// The current view
  /// </summary>
  ViewModelBase? CurrentView { get; set; }
}

