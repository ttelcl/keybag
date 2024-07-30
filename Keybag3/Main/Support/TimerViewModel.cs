/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Keybag3.MessageUtilities;
using Keybag3.WpfUtilities;

namespace Keybag3.Main.Support;

/// <summary>
/// Support class for the auto-hide timer
/// </summary>
public class TimerViewModel: ViewModelBase
{
  public TimerViewModel(
    IHasMessageHub messageHost,
    TimeSpan timeOut)
  {
    _timeOut = timeOut;
    _startTime = DateTimeOffset.UtcNow;
    MessageHost = messageHost;
  }

  public IHasMessageHub MessageHost { get; }

  public bool IsArmed
  {
    get => _isArmed;
    set
    {
      if(SetValueProperty(ref _isArmed, value))
      {
        if(!value)
        {
          Fraction = 0;
          TimedOut = false;
          _startTime = DateTimeOffset.UtcNow;
        }
        if(value)
        {
          _startTime = DateTimeOffset.UtcNow;
        }
        MessageHost.SendMessage(MessageChannels.AutoHideTimerChanged, this);
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
        var maxStart = DateTimeOffset.UtcNow - TimeOut;
        if(maxStart < _startTime)
        {
          // Something other than the timer marked this as timed out,
          // make sure the timer isn't going to disagree
          _startTime = maxStart-TimeSpan.FromSeconds(1);
        }
        MessageHost.SendMessage(MessageChannels.AutoHideTimerChanged, this);
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
