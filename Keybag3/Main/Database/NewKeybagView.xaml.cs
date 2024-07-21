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

namespace Keybag3.Main.Database
{
  /// <summary>
  /// Interaction logic for NewKeybagView.xaml
  /// </summary>
  public partial class NewKeybagView: UserControl
  {
    public NewKeybagView()
    {
      InitializeComponent();
    }

    private void Primary_DataContextChanged(
      object sender, DependencyPropertyChangedEventArgs e)
    {
      if(sender is PasswordBox pwb)
      {
        if(pwb.DataContext is NewKeybagViewModel nkvm)
        {
          nkvm.BindPrimary(pwb);
        }
        else if(pwb.DataContext == null)
        {
          Trace.TraceInformation("Primary PWB detached");
        }
        else
        {
          Trace.TraceError("Failed to bind primary PWB: type error");
        }
      }
      else
      {
        Trace.TraceError("Failed to bind primary PWB");
      }
    }

    private void Verify_DataContextChanged(
      object sender, DependencyPropertyChangedEventArgs e)
    {
      if(sender is PasswordBox pwb)
      {
        if(pwb.DataContext is NewKeybagViewModel nkvm)
        {
          nkvm.BindVerify(pwb);
        }
        else if(pwb.DataContext == null)
        {
          Trace.TraceInformation("Verification PWB detached");
        }
        else
        {
          Trace.TraceError("Failed to bind verification PWB: type error");
        }
      }
      else
      {
        Trace.TraceError("Failed to bind verification PWB");
      }
    }

    private void Primary_PasswordChanged(object sender, RoutedEventArgs e)
    {
      if(sender is PasswordBox pwb && 
        pwb.DataContext is NewKeybagViewModel nkvm)
      {
        using(var passphrase = pwb.SecurePassword)
        {
          nkvm.PrimaryChanged(passphrase);
        }
      }
    }

    private void Verify_PasswordChanged(object sender, RoutedEventArgs e)
    {
      if(sender is PasswordBox pwb &&
        pwb.DataContext is NewKeybagViewModel nkvm)
      {
        using(var passphrase = pwb.SecurePassword)
        {
          nkvm.VerifyChanged(passphrase);
        }
      }
    }
  }
}
