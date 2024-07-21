﻿using System;
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

namespace Keybag3.Main.Database
{
  /// <summary>
  /// Interaction logic for ImportConnectView.xaml
  /// </summary>
  public partial class ImportConnectView: UserControl
  {
    public ImportConnectView()
    {
      InitializeComponent();
    }

    private void Pass_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if(sender is PasswordBox pwb)
      {
        if(pwb.DataContext is ImportConnectViewModel icvm)
        {
          icvm.BindPassBox(pwb);
        }
        else if(pwb.DataContext == null)
        {
          Trace.TraceInformation("PWB detached");
        }
        else
        {
          Trace.TraceError("Failed to bind PWB: type error");
        }
      }
      else
      {
        Trace.TraceError("Failed to bind PWB");
      }
    }
  }
}
