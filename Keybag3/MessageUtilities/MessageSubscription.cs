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
/// An object representing a registration of a message listener
/// to a message channel. The listener must hold on to this
/// registration because the channel only weakly references it.
/// </summary>
public abstract class MessageSubscription
{
  /// <summary>
  /// Create a new MessageListenerRegistration
  /// </summary>
  protected MessageSubscription(
    IMessageChannelBase channelBase)
  {
    ChannelBase = channelBase;
    SubscriptionId = Guid.NewGuid();
  }

  public IMessageChannelBase ChannelBase { get; }

  /// <summary>
  /// A randomply created unique identifier for this registration
  /// </summary>
  public Guid SubscriptionId { get; }


  /// <summary>
  /// Explicitly unscribe the listener from the channel
  /// </summary>
  public abstract void Unsubscribe();

}
