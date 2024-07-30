/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keybag3.MessageUtilities;

/// <summary>
/// A specialized <see cref="MessageSubscription"/> for channels
/// that only pass a sender and no value in their messages.
/// </summary>
/// <typeparam name="TSender">
/// The sender type
/// </typeparam>
public class Subscription<TSender>: MessageSubscription
{
  /// <summary>
  /// Create a new Subscription
  /// </summary>
  internal Subscription(
    MessageChannel<TSender> channel,
    Action<TSender> action)
    : base(channel)
  {
    Channel = channel;
    Action = action;
  }

  public MessageChannel<TSender> Channel { get; }

  public Action<TSender> Action { get; }

  public override void Unsubscribe()
  {
    Channel.Unsubscribe(this);
  }
}

public class Subscription<TSender, TValue>: MessageSubscription
{
  internal Subscription(
    MessageChannel<TSender, TValue> channel,
    Action<TSender, TValue> action)
    : base(channel)
  {
    Channel = channel;
    Action = action;
  }

  public MessageChannel<TSender, TValue> Channel { get; }

  public Action<TSender, TValue> Action { get; }

  public override void Unsubscribe()
  {
    Channel.Unsubscribe(this);
  }
}
