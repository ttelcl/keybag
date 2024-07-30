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

/// <summary>
/// Owner object for loosely coupled message passing
/// </summary>
public class MessageHub
{
  private Dictionary<string, IMessageChannelBase> _channels;

  /// <summary>
  /// Create a new MessageHub
  /// </summary>
  public MessageHub()
  {
    _channels = [];
  }

  public static bool VerboseSend { get; set; }

  /// <summary>
  /// Send a message with a sender and a value to a registered channel
  /// </summary>
  /// <typeparam name="TSender">
  /// The sender type
  /// </typeparam>
  /// <typeparam name="TValue">
  /// The value type
  /// </typeparam>
  /// <param name="channelName">
  /// The identifier of the channel
  /// </param>
  /// <param name="sender">
  /// The sender object
  /// </param>
  /// <param name="value">
  /// The value
  /// </param>
  /// <exception cref="InvalidOperationException"></exception>
  public void Send<TSender, TValue>(
    string channelName,
    TSender sender,
    TValue value)
  {
    if(VerboseSend)
    {
      var senderType = typeof(TSender).Name;
      var valueType = typeof(TValue).Name;
      Trace.TraceInformation(
        $"Send<{senderType},{valueType}> to channel '{channelName}'");
    }
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is MessageChannel<TSender, TValue> typedChannel)
      {
        typedChannel.Send(sender, value);
      }
      else
      {
        throw new InvalidOperationException(
          $"Incompatible channel types for message id '{channelName}'");
      }
    }
    else
    {
      throw new InvalidOperationException(
        $"Channel '{channelName}' is not registered");
    }
  }

  /// <summary>
  /// Send a message with a sender and no value to a registered channel
  /// </summary>
  /// <typeparam name="TSender">
  /// The sender type
  /// </typeparam>
  /// <param name="channelName">
  /// The identifier of the channel
  /// </param>
  /// <param name="sender">
  /// The sender object
  /// </param>
  /// <exception cref="InvalidOperationException"></exception>
  public void Send<TSender>(
    string channelName,
    TSender sender)
  {
    if(VerboseSend)
    {
      var senderType = typeof(TSender).Name;
      Trace.TraceInformation(
        $"Send<{senderType}> to channel '{channelName}'");
    }
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is MessageChannel<TSender> typedChannel)
      {
        typedChannel.Send(sender);
      }
      else
      {
        throw new InvalidOperationException(
          $"Incompatible channel types for message id '{channelName}'");
      }
    }
    else
    {
      throw new InvalidOperationException(
        $"Channel '{channelName}' is not registered");
    }
  }

  public Subscription<TSender, TValue> Subscribe<TSender, TValue>(
    string channelName,
    Action<TSender, TValue> handler)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is MessageChannel<TSender, TValue> typedChannel)
      {
        return typedChannel.Subscribe(handler);
      }
      else
      {
        throw new InvalidOperationException(
          $"Incompatible channel types for message id '{channelName}'");
      }
    }
    else
    {
      throw new InvalidOperationException(
        $"Channel '{channelName}' does not exist");
    }
  }

  public Subscription<TSender> Subscribe<TSender>(
    string channelName,
    Action<TSender> handler)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is MessageChannel<TSender> typedChannel)
      {
        return typedChannel.Subscribe(handler);
      }
      else
      {
        throw new InvalidOperationException(
          $"Incompatible channel types for message id '{channelName}'");
      }
    }
    else
    {
      throw new InvalidOperationException(
        $"Channel '{channelName}' does not exist");
    }
  }

  /// <summary>
  /// Register a message channel, creating it if it does not exist already
  /// </summary>
  /// <typeparam name="TSender">
  /// The sender object type for the channel
  /// </typeparam>
  /// <typeparam name="TValue">
  /// The value type for the channel
  /// </typeparam>
  /// <param name="channelName">
  /// The channel name (message ID)
  /// </param>
  /// <returns>
  /// The new or pre-existing channel
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the preexisting channel is not compatible
  /// </exception>
  public MessageChannel<TSender, TValue> RegisterChannel<TSender, TValue>(
    string channelName)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is MessageChannel<TSender, TValue> typedChannel)
      {
        return typedChannel;
      }
      throw new InvalidOperationException(
        $"Incompatible channel types for message id '{channelName}'");
    }
    else
    {
      var newChannel = new MessageChannel<TSender, TValue>(channelName);
      _channels[channelName] = newChannel;
      return newChannel;
    }
  }

  /// <summary>
  /// Register a no-value message channel, creating it if it
  /// does not exist already
  /// </summary>
  /// <typeparam name="TSender">
  /// The sender object type for the channel
  /// </typeparam>
  /// <param name="channelName">
  /// The channel name (message ID)
  /// </param>
  /// <returns>
  /// The new or pre-existing channel
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the preexisting channel is not compatible
  /// </exception>
  public MessageChannel<TSender> RegisterChannel<TSender>(
    string channelName)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is MessageChannel<TSender> typedChannel)
      {
        return typedChannel;
      }
      throw new InvalidOperationException(
        $"Incompatible channel types for message id '{channelName}'");
    }
    else
    {
      var newChannel = new MessageChannel<TSender>(channelName);
      _channels[channelName] = newChannel;
      return newChannel;
    }
  }

}
