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
  /// <summary>
  /// The channel's name
  /// </summary>
  string ChannelName { get; }
}
