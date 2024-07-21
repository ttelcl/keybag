/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keybag3.WpfUtilities;

/// <summary>
/// A type that cannot be instantiated, so the only valid
/// value for a variable of this type is null.
/// </summary>
public class NullType
{
  /// <summary>
  /// Create a new NullType
  /// </summary>
  private NullType()
  {
    throw new NotSupportedException(
      "This class cannot be instatiated");
  }

  /// <summary>
  /// The only valid value for a variable of this type.
  /// </summary>
  public static NullType? Value { get; } = null;

}
