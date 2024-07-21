/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Keybag3.WpfUtilities;

namespace Keybag3.Main;

/// <summary>
/// A viewmodel that supports displaying an overlay in some way
/// </summary>
public interface ISupportsOverlay
{
  ViewModelBase? Overlay { get; }

  Visibility OverlayVisibility { get; }

  void PushOverlay(ViewModelBase overlay);

  void PopOverlay(ViewModelBase overlay);
}

