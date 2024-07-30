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

public class MessageChannel<TSender, TValue> :
  IMessageChannel<TSender, TValue>
{
    internal MessageChannel(
      string channelName)
    {
        ChannelName = channelName;
    }

    public string ChannelName { get; }

    public event Action<TSender, TValue>? MessageReceived;

    public void Send(TSender sender, TValue value)
    {
        MessageReceived?.Invoke(sender, value);
    }
}

public class MessageChannel<TSender> :
  IMessageChannel<TSender>
{
    internal MessageChannel(
      string channelName)
    {
        ChannelName = channelName;
    }

    public string ChannelName { get; }

    public event Action<TSender>? MessageReceived;

    public void Send(TSender sender)
    {
        MessageReceived?.Invoke(sender);
    }
}
