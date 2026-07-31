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
public sealed class Subscription<TSender>: MessageSubscription
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

  /// <summary>
  /// The strongly typed channel this is a subscription to
  /// </summary>
  public MessageChannel<TSender> Channel { get; }

  /// <summary>
  /// The callback invoked when a message is sent to the <see cref="Channel"/>
  /// </summary>
  public Action<TSender> Action { get; }

  /// <inheritdoc/>
  public override void Unsubscribe()
  {
    Channel.Unsubscribe(this);
  }
}

/// <summary>
/// Concrete implementation of <see cref="MessageSubscription"/> for channels
/// that carry values in their messages.
/// </summary>
/// <typeparam name="TSender"></typeparam>
/// <typeparam name="TValue"></typeparam>
public sealed class Subscription<TSender, TValue>: MessageSubscription
{
  internal Subscription(
    MessageChannel<TSender, TValue> channel,
    Action<TSender, TValue> action)
    : base(channel)
  {
    Channel = channel;
    Action = action;
  }

  /// <summary>
  /// The strongly typed channel this is a subscription to
  /// </summary>
  public MessageChannel<TSender, TValue> Channel { get; }

  /// <summary>
  /// The callback invoked when a message is sent to the <see cref="Channel"/>
  /// </summary>
  public Action<TSender, TValue> Action { get; }

  /// <inheritdoc/>
  public override void Unsubscribe()
  {
    Channel.Unsubscribe(this);
  }
}
