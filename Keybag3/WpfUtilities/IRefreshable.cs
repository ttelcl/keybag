/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keybag3.WpfUtilities;

/// <summary>
/// A ViewModel that needs an active operation to refresh its
/// state from its underlying model
/// </summary>
public interface IRefreshable
{
  void Refresh();
}

