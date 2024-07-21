using System;
using System.Collections.Generic;
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
  /// Interaction logic for EntryEditOverlay.xaml
  /// </summary>
  public partial class EntryEditOverlay: UserControl
  {
    public EntryEditOverlay()
    {
      InitializeComponent();
    }

    private void Label_TextBox_Loaded(object sender, RoutedEventArgs e)
    {
      if(sender is TextBox tb)
      {
        // Ugly hack to steal initial focus
        tb.Focus();
        Keyboard.Focus(tb);
      }
    }
  }
}
