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

/// <summary>
/// Support class for the auto-hide timer
/// </summary>
public class TimerViewModel: ViewModelBase
{
  private DispatcherTimer _timer;

  public TimerViewModel(
    IHasMessageHub messageHost,
    TimeSpan timeOut)
  {
    _timeOut = timeOut;
    _startTime = DateTimeOffset.UtcNow;
    _timer = new DispatcherTimer() {
      Interval = TimeSpan.FromMilliseconds(1000),
      IsEnabled = false,
    };
    _timer.Tick += (s, e) => Tick();
    MessageHost = messageHost;
  }

  public const string AutoHideStateChanged = "auto-hide-state-changed";
  public const string AutoHideProgressChanged = "auto-hide-state-changed";

  public IHasMessageHub MessageHost { get; }

  public AutoHideState State
  {
    get => _state;
    set
    {
      if(SetValueProperty(ref _state, value))
      {
        Trace.TraceInformation($"Auto Hide timer State: {value}");
        MessageHost.SendMessage(AutoHideStateChanged, this);
      }
    }
  }
  private AutoHideState _state;

  public bool IsArmed
  {
    get => _isArmed;
    set
    {
      if(SetValueProperty(ref _isArmed, value))
      {
        Trace.TraceInformation($"Auto Hide timer IsArmed: {value}");
        if(!value)
        {
          Fraction = 0;
          _startTime = DateTimeOffset.UtcNow;
          State = AutoHideState.StaticVisible;
        }
        if(value)
        {
          _startTime = DateTimeOffset.UtcNow;
          State = TimedOut ? AutoHideState.Hidden : AutoHideState.Ticking;
        }
        // Already available via State:
        //MessageHost.SendMessage(MessageChannels.AutoHideTimerChanged, this);
      }
    }
  }
  private bool _isArmed;

  public double Fraction
  {
    get => _fraction;
    set
    {
      if(SetValueProperty(ref _fraction, value))
      {
        MessageHost.SendMessage(AutoHideProgressChanged, this);
      }
    }
  }
  private double _fraction;

  public bool TimedOut
  {
    get => _timedOut;
    set
    {
      if(SetValueProperty(ref _timedOut, value))
      {
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
        }
        else
        {
          State = IsArmed ? AutoHideState.Ticking : AutoHideState.StaticVisible;
        }
        //MessageHost.SendMessage(MessageChannels.AutoHideTimerChanged, this);
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
