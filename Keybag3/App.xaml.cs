using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

using ControlzEx.Theming;

using Keybag3.Converters;
using Keybag3.Main;
using Keybag3.Services;

namespace Keybag3;

public partial class App: Application
{
  private KeybagServices? _services;

  /// <summary>
  /// Instead of using a Startup Uri, create the window manually.
  /// This method is referenced in the header of app.xaml instead of
  /// a startup URI.
  /// </summary>
  private void App_Startup(object sender, StartupEventArgs e)
  {
    DispatcherUnhandledException += (s, e) =>
      ProcessUnhandledException(e);
    Trace.TraceInformation($"App.App_Startup enter");
    ThemeManager.Current.ChangeTheme(this, "Dark.Olive");
    _services = new KeybagServices();
    InitCommonColors();
    var mainWindow = new MainWindow();
    MainModel = new MainViewModel(_services);
    mainWindow.DataContext = MainModel;
    Trace.TraceInformation($"App.App_Startup showing main window");
    mainWindow.Show();
    Trace.TraceInformation($"App.App_Startup done");
  }

  private static void InitCommonColors()
  {
    var bc = BrushCache.Default;
    bc.AddAliases([
        "/Keybag3/Back/Good",
        "/Keybag3/Back/Unlocked",
        "/Keybag3/Back/OK",
      ], "#2866CC44");
    bc.AddAliases([
        "/Keybag3/Fore/Good",
        "/Keybag3/Fore/Unlocked",
        "/Keybag3/Fore/OK",
      ], "#EE66CC44");

    bc.AddAliases([
        "/Keybag3/Back/Bad",
        "/Keybag3/Back/Locked",
        "/Keybag3/Back/Error",
      ], "#28DD5533");
    bc.AddAliases([
        "/Keybag3/Fore/Bad",
        "/Keybag3/Fore/Locked",
        "/Keybag3/Fore/Error",
      ], "#EEDD5533");

    bc.AddAliases([
        "/Keybag3/Back/Warning",
      ], "#28DDCC33");
    bc.AddAliases([
        "/Keybag3/Fore/Warning",
      ], "#EEDDCC33");

    bc.AddAliases([
        "/Keybag3/Fore/LightGray",
      ], "#EEBBBBBB");

    bc.AddAliases([
        "/Keybag3/Fore/MidGray",
      ], "#EE888888");

    bc.AddAliases([
        "/Keybag3/Fore/DarkGray",
      ], "#EE555555");

    bc.AddAliases([
        "/Keybag3/Back/White",
        "/Keybag3/Back/Neutral"
      ], "#28FFFFFF");

    bc.AddAliases([
        "/Keybag3/Fore/White",
        "/Keybag3/Fore/Neutral"
      ], "#EEFFFFFF");

    bc.AddAliases([
        "/Keybag3/Back/Changed",
      ], "#288888EE");

    bc.AddAliases([
        "/Keybag3/Fore/Changed",
      ], "#EE8888EE");

    // Tag Class Colors (Fore is darker)

    bc.AddAliases([
        "/Keybag3/Back/Other",
      ], "#FF6D8764"); // Olive

    bc.AddAliases([
        "/Keybag3/Fore/Other",
      ], "#181818");

    bc.AddAliases([
        "/Keybag3/Back/Hidden",
      ], "#FF777777");

    bc.AddAliases([
        "/Keybag3/Fore/Hidden",
      ], "#181818");

    bc.AddAliases([
        "/Keybag3/Back/Valued",
      ], "#FF60A917"); // Green

    bc.AddAliases([
        "/Keybag3/Fore/Valued",
      ], "#181818");

    bc.AddAliases([
        "/Keybag3/Back/Section",
      ], "#FFF0A30A"); // Amber

    bc.AddAliases([
        "/Keybag3/Fore/Section",
      ], "#181818");

    bc.AddAliases([
        "/Keybag3/Back/Title",
      ], "#FF76608A"); // Mauve

    bc.AddAliases([
        "/Keybag3/Fore/Title",
      ], "#181818");

    bc.AddAliases([
        "/Keybag3/Fore/NoSearch",
        "/Keybag3/Fore/Indirect",
        "/Keybag3/Fore/NoMatch",
      ], "#F8F8F8");

    bc.AddAliases([
        "/Keybag3/Fore/Hit",
      ], "#CCFFBB");

    bc.AddAliases([
        "/Keybag3/Fore/Support",
      ], /*"#E8F8E0"*/ "#F8F8F8");

    bc.AddAliases([ // should never actually show
        "/Keybag3/Fore/Blocker",
        "/Keybag3/Fore/Blocked",
      ], "#FFF83838");

    bc.AddAliases([
        "/Keybag3/Fore/EntryDefault",
      ], "#FFFFFF");

    bc.AddAliases([
        "/Keybag3/Fore/EntryHit",
      ], "#CCFFBB");

    bc.AddAliases([
        "/Keybag3/Fore/EntryArchived",
        "/Keybag3/Fore/Archived",
      ], "#6688EE");

    bc.AddAliases([
        "/Keybag3/Fore/EntryErased",
      ], "#995555");

    bc.AddAliases([
        "/Keybag3/Fore/EntryOutOfScope",
      ], "#AA99DD");

    bc.AddAliases([
        "/Keybag3/Fore/EntrySealed",
      ], "#D0D0C8");

  }

  private void ProcessUnhandledException(
    System.Windows.Threading.DispatcherUnhandledExceptionEventArgs evt)
  {
    var ex = evt.Exception;
    Trace.TraceError($"Error: {ex}");
    MessageBox.Show(
      $"{ex.GetType().FullName}\n{ex.Message}",
      "Error",
      MessageBoxButton.OK,
      MessageBoxImage.Error);
    evt.Handled = MainWindow?.IsLoaded ?? false;
  }

  public MainViewModel? MainModel { get; private set; }

  private void Application_Exit(object sender, ExitEventArgs e)
  {
    Trace.TraceInformation("Application_Exit: Cleanup");
    if(_services != null)
    {
      var services = _services;
      _services = null;
      services.Dispose();
    }
  }

  private void Application_SessionEnding(
    object sender, SessionEndingCancelEventArgs e)
  {
    var ok = MainModel?.DbViewModel?.AppTerminating() ?? true;
    if(!ok)
    {
      e.Cancel = true;
    }
  }

  private void Application_Activated(object sender, EventArgs e)
  {
    MainModel?.ApplicationShowing(true);
  }

  private void Application_Deactivated(object sender, EventArgs e)
  {
    MainModel?.ApplicationShowing(false);
  }
}
