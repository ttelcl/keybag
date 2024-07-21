using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using MahApps.Metro.Controls;

namespace Keybag3.Main;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow: MetroWindow
{
  public MainWindow()
  {
    InitializeComponent();
  }

  protected override void OnClosing(CancelEventArgs e)
  {
    if(DataContext is MainViewModel mainModel)
    {
      var ok = mainModel.DbViewModel?.AppTerminating() ?? true;
      if(!ok)
      {
        e.Cancel = true;
        return; // don't call base class
      }
    }
    base.OnClosing(e);
  }

  private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
  {
  }
}
