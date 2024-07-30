/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keybag3.MessageUtilities;

public interface IMessageChannelBase
{
    string ChannelName { get; }
}

public interface IMessageChannel<TSender> : IMessageChannelBase
{
    void Send(TSender sender);

    event Action<TSender>? MessageReceived;
}

public interface IMessageChannel : IMessageChannel<object>
{
}

public interface IMessageChannel<TSender, TValue> : IMessageChannelBase
{
    void Send(TSender sender, TValue value);

    event Action<TSender, TValue>? MessageReceived;
}
