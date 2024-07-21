using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Keybag3.Main.KeybagContent
{
  /// <summary>
  /// Interaction logic for KeybagView.xaml
  /// </summary>
  public partial class KeybagView: UserControl
  {
    public KeybagView()
    {
      InitializeComponent();
    }

    private void TreeView_Selected(object sender, RoutedEventArgs e)
    {
      var label = "Unknown";
      if(e.OriginalSource is TreeViewItem item)
      {
        if(item.DataContext is EntryViewModel entry)
        {
          label = entry.Label;
        }
        Trace.TraceInformation($"TreeView_Selected: bringing into view: '{label}'");
        item.BringIntoView();
      }
    }
  }
}
