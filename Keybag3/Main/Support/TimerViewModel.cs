/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

using Keybag3.MessageUtilities;
using Keybag3.WpfUtilities;

namespace Keybag3.Main.Support;

public enum AutoHideState
{
  /// <summary>
  /// The current keybag is visible and the auto-hide timer is not running
  /// </summary>
  StaticVisible,

  /// <summary>
  /// The current keybag is visible and the auto-hide timer is running
  /// </summary>
  Ticking,

  /// <summary>
  /// The current keybag is hidden (and the timer is irrelevant)
  /// </summary>
  Hidden,
}

public interface IAutoHideTimerListener
{
  void AutoHideStateChanged(AutoHideState state);

  void AutoHideProgressChanged(double fraction);

  bool CanHideAnything();
}

/// <summary>
/// Support class for the auto-hide timer
/// </summary>
public class TimerViewModel: ViewModelBase
{
  private DispatcherTimer _timer;

  public TimerViewModel(
    TimeSpan timeOut,
    IAutoHideTimerListener? initialListener)
  {
    _timeOut = timeOut;
    _startTime = DateTimeOffset.UtcNow;
    Listener = initialListener;
    _timer = new DispatcherTimer() {
      Interval = TimeSpan.FromMilliseconds(333),
      IsEnabled = false,
    };
    _timer.Tick += (s, e) => Tick();
  }

  public IAutoHideTimerListener? Listener { get; set; }

  public AutoHideState State {
    get => _state;
    private set {
      if(SetValueProperty(ref _state, value))
      {
        Trace.TraceInformation($"Auto Hide timer State: {value}");
        switch(_state)
        {
          case AutoHideState.StaticVisible:
            _timer.IsEnabled = false;
            Fraction = 0;
            break;
          case AutoHideState.Ticking:
            _timer.IsEnabled = Listener?.CanHideAnything() ?? false;
            break;
          case AutoHideState.Hidden:
            Fraction = 1;
            _timer.IsEnabled = false;
            break;
          default:
            throw new InvalidOperationException(
              $"Unknown AutoHideState: {value}");
        }
        Listener?.AutoHideStateChanged(_state);
      }
    }
  }
  private AutoHideState _state;

  public bool IsArmed {
    get => _isArmed;
    set {
      if(SetValueProperty(ref _isArmed, value))
      {
        Trace.TraceInformation($"Auto Hide timer IsArmed: {value}");
        if(!value)
        {
          if(TimedOut)
          {
            Fraction = 1;
            State = AutoHideState.Hidden;
          }
          else
          {
            _startTime = DateTimeOffset.UtcNow;
            Fraction = 0;
            State = AutoHideState.StaticVisible;
          }
        }
        else
        {
          _startTime = DateTimeOffset.UtcNow;
          State = TimedOut ? AutoHideState.Hidden : AutoHideState.Ticking;
        }
      }
    }
  }
  private bool _isArmed;

  public void ManualShowHide(bool show)
  {
    if(show)
    {
      TimedOut = false;
      IsArmed = false;
    }
    else
    {
      TimedOut = true;
    }
  }

  public double Fraction {
    get => _fraction;
    set {
      if(value < 0.0)
      {
        value = 0.0;
      }
      else if(value > 1.0)
      {
        value = 1.0;
      }
      if(SetValueProperty(ref _fraction, value))
      {
        Listener?.AutoHideProgressChanged(_fraction);
      }
    }
  }
  private double _fraction;

  public bool TimedOut {
    get => _timedOut;
    set {
      if(SetValueProperty(ref _timedOut, value))
      {
        Trace.TraceInformation($"Auto Hide timer TimedOut: {value}");
        if(value)
        {
          var maxStart = DateTimeOffset.UtcNow - TimeOut;
          if(maxStart < _startTime)
          {
            // Something other than the timer marked this as timed out,
            // make sure the timer isn't going to disagree
            _startTime = maxStart-TimeSpan.FromSeconds(1);
          }
          State = AutoHideState.Hidden;
          Fraction = 1.0;
        }
        else
        {
          State = IsArmed ? AutoHideState.Ticking : AutoHideState.StaticVisible;
        }
      }
    }
  }
  private bool _timedOut;

  public void Tick()
  {
    if(IsArmed && !TimedOut)
    {
      var elapsed = DateTimeOffset.UtcNow - _startTime;
      var fraction = elapsed.Ticks / (double)_timeOut.Ticks;
      if(fraction >= 1.0)
      {
        fraction = 1.0;
        TimedOut = true;
      }
      Fraction = fraction;
    }
  }

  public TimeSpan TimeOut {
    get => _timeOut;
    set {
      if(SetValueProperty(ref _timeOut, value))
      {
      }
    }
  }
  private TimeSpan _timeOut;

  private DateTimeOffset _startTime;
}
