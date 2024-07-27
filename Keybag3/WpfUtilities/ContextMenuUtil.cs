/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace Keybag3.WpfUtilities;

/*
 * See https://stackoverflow.com/a/62916106/271323
 * 
 * Usage example:
<Button Content="Button with ContextMenu" ContextMenuService.Placement="Bottom"
    utils:ContextMenuUtil.OpenOnLeftClick="True">
    <Button.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Do A" />
        </ContextMenu>
    </Button.ContextMenu>
</Button>
 */

static class ContextMenuTools
{
  public static readonly DependencyProperty OpenOnLeftClickProperty =
      DependencyProperty.RegisterAttached(
          "OpenOnLeftClick",
          typeof(bool),
          typeof(ContextMenuTools),
          new PropertyMetadata(false, OpenOnLeftClickChanged));

  public static void SetOpenOnLeftClick(UIElement element, bool value)
      => element.SetValue(OpenOnLeftClickProperty, value);

  public static bool GetOpenOnLeftClick(UIElement element)
      => (bool)element.GetValue(OpenOnLeftClickProperty);

  private static void OpenOnLeftClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
  {
    if(d is IInputElement element && (bool)e.NewValue)
    {
      element.PreviewMouseLeftButtonDown += ElementOnMouseLeftButtonDown;
    }
  }

  private static void ElementOnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
  {
    if(sender is UIElement element
        && ContextMenuService.GetContextMenu(element) is ContextMenu contextMenu)
    {
      contextMenu.Placement = ContextMenuService.GetPlacement(element);
      contextMenu.PlacementTarget = element;
      contextMenu.IsOpen = true;
    }
  }
}

