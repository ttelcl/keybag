/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keybag3.WpfUtilities;

/// <summary>
/// An object having a MessageHub
/// </summary>
public interface IHasMessageHub
{
  /// <summary>
  /// The MessageHub
  /// </summary>
  MessageHub MessageHub { get; }
}

public static class MessageHubExtensions
{
  public static IHasMessageHub SendMessage<TSender, TValue>(
    this IHasMessageHub hasMessageHub,
    string channelName,
    TSender sender,
    TValue value)
  {
    hasMessageHub.MessageHub.Send(channelName, sender, value);
    return hasMessageHub;
  }

  public static IHasMessageHub SendMessage<TSender>(
    this IHasMessageHub hasMessageHub,
    string channelName,
    TSender sender)
  {
    hasMessageHub.MessageHub.Send(channelName, sender);
    return hasMessageHub;
  }

  public static IHasMessageHub RegisterChannel<TSender, TValue>(
    this IHasMessageHub hasMessageHub,
    string channelName)
  {
    hasMessageHub.MessageHub.RegisterChannel<TSender, TValue>(channelName);
    return hasMessageHub;
  }

  public static IHasMessageHub RegisterNoValueChannel<TSender>(
    this IHasMessageHub hasMessageHub,
    string channelName)
  {
    hasMessageHub.MessageHub.RegisterNoValueChannel<TSender>(channelName);
    return hasMessageHub;
  }

  public static IHasMessageHub Subscribe<TSender, TValue>(
    this IHasMessageHub hasMessageHub,
    string channelName,
    Action<TSender, TValue> action,
    bool register = false)
  {
    if(register)
    {
       hasMessageHub.MessageHub.RegisterChannel<TSender, TValue>(channelName);
    }
    hasMessageHub.MessageHub.Subscribe(channelName, action);
    return hasMessageHub;
  }

  public static IHasMessageHub Subscribe<TSender>(
    this IHasMessageHub hasMessageHub,
    string channelName,
    Action<TSender> action,
    bool register = false)
  {
    if(register)
    { 
      hasMessageHub.MessageHub.RegisterNoValueChannel<TSender>(channelName);
    }
    hasMessageHub.MessageHub.Subscribe(channelName, action);
    return hasMessageHub;
  }

//--
}

