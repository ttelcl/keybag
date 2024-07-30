/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace Keybag3.MessageUtilities;

public class MessageChannel<TSender, TValue>: IMessageChannelBase
{
  private Dictionary<Guid, WeakReference<Subscription<TSender, TValue>>>
    _subscriptions;

  internal MessageChannel(
    string channelName)
  {
    _subscriptions = [];
    ChannelName = channelName;
  }

  public string ChannelName { get; }

  // public event Action<TSender, TValue>? MessageReceived;

  /// <summary>
  /// Send the message to all subscribers.
  /// If any subscriber throws an exception, the exceptions are
  /// collected and after invoking all subscribers thrown as an
  /// <see cref="AggregateException"/>.
  /// If any registrations have been garbage collected, they are
  /// automatically removed.
  /// </summary>
  /// <param name="sender">
  /// The argument to pass to the subscribers.
  /// </param>
  /// <exception cref="AggregateException">
  /// Thrown if any subscriber throws an exception.
  /// </exception>
  public void Send(TSender sender, TValue value)
  {
    List<Exception>? errors = null;
    List<Guid>? expiredGuids = null;
    foreach(var kvp in _subscriptions)
    {
      if(kvp.Value.TryGetTarget(out var subscription))
      {
        try
        {
          subscription.Action(sender, value);
        }
        catch(Exception ex)
        {
          errors ??= [];
          errors.Add(ex);
        }
      }
      else
      {
        expiredGuids ??= [];
        expiredGuids.Add(kvp.Key);
      }
    }
    if(expiredGuids != null)
    {
      foreach(var guid in expiredGuids)
      {
        _subscriptions.Remove(guid);
      }
    }
    if(errors != null)
    {
      throw new AggregateException(errors);
    }
  }

  /// <summary>
  /// Subscribe to the channel and return a
  /// <see cref="Subscription{TSender, TValue}"/>.
  /// It is important to keep a reference to the subscription object,
  /// since the channel only weakly references it and unregisters it
  /// if it detects that the subscription has been garbage collected.
  /// </summary>
  /// <returns>
  /// A <see cref="Subscription{TSender, TValue}"/> object that the subscriber
  /// must hold on to. It can be used to explicitly unsubscribe from
  /// the channel.
  /// </returns>
  public Subscription<TSender, TValue> Subscribe(
    Action<TSender, TValue> action)
  {
    var subscription = new Subscription<TSender, TValue>(this, action);
    _subscriptions[subscription.SubscriptionId] =
      new WeakReference<Subscription<TSender, TValue>>(subscription);
    return subscription;
  }

  public void Unsubscribe(
    Subscription<TSender, TValue> subscription)
  {
    // There is not much point in validating that the id matches
    // the passed subscription, so skip that.
    _subscriptions.Remove(subscription.SubscriptionId);
  }

}

public class MessageChannel<TSender>: IMessageChannelBase
{
  private Dictionary<Guid, WeakReference<Subscription<TSender>>>
    _subscriptions;

  internal MessageChannel(
    string channelName)
  {
    _subscriptions = [];
    ChannelName = channelName;
  }

  /// <inheritdoc/>
  public string ChannelName { get; }

  // public event Action<TSender>? MessageReceived;

  /// <summary>
  /// Send the message to all subscribers.
  /// If any subscriber throws an exception, the exceptions are
  /// collected and after invoking all subscribers thrown as an
  /// <see cref="AggregateException"/>.
  /// If any registrations have been garbage collected, they are
  /// automatically removed.
  /// </summary>
  /// <param name="sender">
  /// The argument to pass to the subscribers.
  /// </param>
  /// <exception cref="AggregateException">
  /// Thrown if any subscriber throws an exception.
  /// </exception>
  public void Send(TSender sender)
  {
    List<Exception>? errors = null;
    List<Guid>? expiredGuids = null;
    foreach(var kvp in _subscriptions)
    {
      if(kvp.Value.TryGetTarget(out var subscription))
      {
        try
        {
          subscription.Action(sender);
        }
        catch(Exception ex)
        {
          errors ??= [];
          errors.Add(ex);
        }
      }
      else
      {
        expiredGuids ??= [];
        expiredGuids.Add(kvp.Key);
      }
    }
    if(expiredGuids != null)
    {
      foreach(var guid in expiredGuids)
      {
        _subscriptions.Remove(guid);
      }
    }
    if(errors != null)
    {
      throw new AggregateException(errors);
    }
  }

  /// <summary>
  /// Subscribe to the channel and return a
  /// <see cref="Subscription{TSender}"/>.
  /// It is important to keep a reference to the subscription object,
  /// since the channel only weakly references it and unregisters it
  /// if it detects that the subscription has been garbage collected.
  /// </summary>
  /// <returns>
  /// A <see cref="Subscription{TSender}"/> object that the subscriber
  /// must hold on to. It can be used to explicitly unsubscribe from
  /// the channel.
  /// </returns>
  public Subscription<TSender> Subscribe(Action<TSender> action)
  {
    var subscription = new Subscription<TSender>(this, action);
    _subscriptions[subscription.SubscriptionId] =
      new WeakReference<Subscription<TSender>>(subscription);
    return subscription;
  }

  internal void Unsubscribe(Subscription<TSender> subscription)
  {
    // There is not much point in validating that the id matches
    // the passed subscription, so skip that.
    _subscriptions.Remove(subscription.SubscriptionId);
  }

}
