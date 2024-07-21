/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace Keybag3.WpfUtilities;

// Inspired by https://stackoverflow.com/a/8204248/271323

/// <summary>
/// Attached property for moving focus to the next control on Enter key press
/// </summary>
public static class FocusUtil
{
  public static bool GetAdvancesByEnterKey(DependencyObject obj)
  {
    return (bool)obj.GetValue(AdvancesByEnterKeyProperty);
  }

  public static void SetAdvancesByEnterKey(DependencyObject obj, bool value)
  {
    obj.SetValue(AdvancesByEnterKeyProperty, value);
  }

  public static readonly DependencyProperty AdvancesByEnterKeyProperty =
      DependencyProperty.RegisterAttached("AdvancesByEnterKey",
        typeof(bool), typeof(FocusUtil),
        new UIPropertyMetadata(OnAdvancesByEnterKeyPropertyChanged));

  static void OnAdvancesByEnterKeyPropertyChanged(
    DependencyObject d, DependencyPropertyChangedEventArgs e)
  {
    if(d is UIElement element)
    {
      if((bool)e.NewValue)
        element.KeyDown += KeyDown;
      else
        element.KeyDown -= KeyDown;
    }
  }

  static void KeyDown(object sender, KeyEventArgs e)
  {
    if(e.Key.Equals(Key.Enter) && sender is UIElement element)
    {
      element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
  }

}
