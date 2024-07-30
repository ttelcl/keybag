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
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender, TValue> typedChannel)
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
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender> typedChannel)
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

  public void Subscribe<TSender, TValue>(
    string channelName,
    Action<TSender, TValue> handler)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender, TValue> typedChannel)
      {
        typedChannel.MessageReceived += handler;
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

  public void Subscribe<TSender>(
    string channelName,
    Action<TSender> handler)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender> typedChannel)
      {
        typedChannel.MessageReceived += handler;
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

  public void Unsubscribe<TSender, TValue>(
    string channelName,
    Action<TSender, TValue> handler)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender, TValue> typedChannel)
      {
        typedChannel.MessageReceived -= handler;
      }
      else
      {
        throw new InvalidOperationException(
          $"Incompatible channel types for message id '{channelName}'");
      }
    }
    else
    {
      // ignore
    }
  }

  public void Unsubscribe<TSender>(
    string channelName,
    Action<TSender> handler)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender> typedChannel)
      {
        typedChannel.MessageReceived -= handler;
      }
      else
      {
        throw new InvalidOperationException(
          $"Incompatible channel types for message id '{channelName}'");
      }
    }
    else
    {
      // ignore
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
  public IMessageChannel<TSender, TValue> RegisterChannel<TSender, TValue>(
    string channelName)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender, TValue> typedChannel)
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
  public IMessageChannel<TSender> RegisterNoValueChannel<TSender>(
    string channelName)
  {
    if(_channels.TryGetValue(channelName, out var channel))
    {
      if(channel is IMessageChannel<TSender> typedChannel)
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

  /// <summary>
  /// Register a message channel, creating it if it does not exist already.
  /// The sender type is implied as being <see cref="object"/>.
  /// </summary>
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
  public IMessageChannel<object, TValue> RegisterValueChannel<TValue>(
    string channelName)
  {
    return RegisterChannel<object, TValue>(channelName);
  }

  /// <summary>
  /// Register a no-value message channel, creating it if it does not
  /// exist already.
  /// The sender type is implied as being <see cref="object"/>.
  /// </summary>
  /// <param name="channelName">
  /// The channel name (message ID)
  /// </param>
  /// <returns>
  /// The new or pre-existing channel
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the preexisting channel is not compatible
  /// </exception>
  public IMessageChannel<object> RegisterChannel(
    string channelName)
  {
    return RegisterNoValueChannel<object>(channelName);
  }

}
