/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using ControlzEx.Theming;

using Keybag3.Main.Database;
using Keybag3.Main.Support;
using Keybag3.MessageUtilities;
using Keybag3.Services;
using Keybag3.WpfUtilities;

namespace Keybag3.Main;

public class MainViewModel:
  ViewModelBase, IStatusMessage, ISupportsOverlay, IHasCurrentView, IHasMessageHub
{
  private Stack<ViewModelBase> _overlayStack;

  public MainViewModel(KeybagServices services)
  {
    _overlayStack = new Stack<ViewModelBase>();
    MessageHub = new MessageHub();
    Services = services;
    AutoHideTimer = new TimerViewModel(this, TimeSpan.FromSeconds(180));
    DbViewModel = new KeybagDbViewModel(this);
    if(DbViewModel.DefaultKeybag == null)
    {
      CurrentView = DbViewModel;
    }
    else
    {
      // Auto-open the default keybag if defined
      CurrentView = DbViewModel.DefaultKeybag;
      DbViewModel.DefaultKeybag.ViewThisSet();
    }

    ResetViewCommand = new DelegateCommand(p => {
      CurrentView = DbViewModel; 
    });

    BadViewModelCommand = new DelegateCommand(p => {
      CurrentView = new TestOverlayViewModel(this);
    });

    CloseOverlayCommand =new DelegateCommand(p => {
      if(Overlay!=null)
      {
        PopOverlay(Overlay);
      }
    });

    SetThemeCommand = new DelegateCommand(p => {
      if(p is String s)
      {
        ThemeColor = s;
      }
    });

    ToggleVerboseChannelCommand = new DelegateCommand(p => {
      MessageHub.VerboseSend = !MessageHub.VerboseSend;
      Trace.TraceInformation($"VerboseSend is now: {MessageHub.VerboseSend}");
    });

    DbgToggleTimerArmed = new DelegateCommand(p => {
      AutoHideTimer.IsArmed = !AutoHideTimer.IsArmed;
    });
    
    _themePaletteItem = Services.ThemeHelper[ThemeColor];
  }

  public KeybagServices Services { get; }

  public KeybagDbViewModel DbViewModel { get; }

  public MessageHub MessageHub { get; }
  
  public ICommand ExitCommand { get; } = new DelegateCommand(p => {
    var w = Application.Current.MainWindow;
    w?.Close();
  });

  public ICommand CrashTestCommand { get; } = new DelegateCommand(p => {
    throw new InvalidOperationException("Crash Test!");
  });

  public ICommand BadViewModelCommand { get; }

  /// <summary>
  /// Reset <see cref="CurrentView"/> to <see cref="DbViewModel"/>
  /// </summary>
  public ICommand ResetViewCommand { get; }

  public ICommand CloseOverlayCommand { get; }

  public ICommand SetThemeCommand { get; }

  public ICommand ToggleVerboseChannelCommand { get; }

  public ICommand DbgToggleTimerArmed { get; }

  public ViewModelBase? Overlay {
    get => _overlay;
    private set {
      if(SetNullableInstanceProperty(ref _overlay, value))
      {
        RaisePropertyChanged(nameof(OverlayVisibility));
        RaisePropertyChanged(nameof(NoOverlay));
      }
    }
  }
  private ViewModelBase? _overlay;

  public Visibility OverlayVisibility {
    get => Overlay==null ? Visibility.Collapsed : Visibility.Visible;
  }

  public void PushOverlay(ViewModelBase overlay)
  {
    _overlayStack.Push(overlay);
    Overlay = overlay;
  }

  public void PopOverlay(ViewModelBase overlay)
  {
    if(overlay != Overlay || overlay != _overlayStack.Peek())
    {
      throw new InvalidOperationException(
        "Inconsistent overlay manipulation");
    }
    _overlayStack.Pop();
    if(_overlayStack.TryPeek(out var newOverlay))
    {
      Overlay = newOverlay;
    }
    else
    {
      Overlay = null;
    }
  }

  public TimerViewModel AutoHideTimer { get; }

  public bool NoOverlay { 
    get => _overlay == null;
  }

  /// <summary>
  /// The ViewModel to show as main content
  /// </summary>
  public ViewModelBase? CurrentView {
    get => _currentView;
    set {
      if(SetNullableInstanceProperty(ref _currentView, value))
      {
        var viewTitle = "(no view active)";
        if(_currentView != null)
        {
          if(_currentView is IHasViewTitle titled)
          {
            viewTitle = titled.Title;
          }
          else
          {
            viewTitle = $"(no title setup for {_currentView?.GetType().Name ?? "?"})";
          }
        }
        Trace.TraceInformation($"Changed View: {viewTitle}");
        ViewTitle = viewTitle;
      }
    }
  }
  private ViewModelBase? _currentView = null;

  public string ViewTitle {
    get => _viewTitle;
    private set {
      // Set via setting CurrentView
      if(SetInstanceProperty(ref _viewTitle, value))
      {
        RaisePropertyChanged(nameof(AppTitle));
      }
    }
  }
  private string _viewTitle = "(no view active)";

  public string AppTitle {
    get => $"{ViewTitle} \u2014 Keybag3";
  }

  public string StatusMessage {
    get => _statusMessage;
    set {
      if(SetInstanceProperty(ref _statusMessage, value))
      {
      }
    }
  }
  private string _statusMessage = "";

  public string ThemeColor {
    get => _themeColor;
    set {
      if(SetValueProperty(ref _themeColor, value ?? "Olive"))
      {
        var fullName = $"Dark.{_themeColor}";
        ThemeManager.Current.ChangeTheme(Application.Current.MainWindow, fullName);
        var color = Services.ThemeHelper[_themeColor];
        if(color.HasValue)
        {
          ThemePaletteItem = color;
        }
      }
    }
  }
  private string _themeColor = "Olive";


  public Color? ThemePaletteItem {
    get => _themePaletteItem;
    set {
      if(SetValueProperty(ref _themePaletteItem, value))
      {
        var themeName = Services.ThemeHelper[_themePaletteItem];
        if(themeName != null)
        {
          ThemeColor = themeName;
        }
      }
    }
  }
  private Color? _themePaletteItem;

}
