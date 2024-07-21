/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Base class for chunk content models
/// </summary>
public abstract class ContentBase
{
  /// <summary>
  /// Create a new ContentBase
  /// </summary>
  protected ContentBase()
  {
  }

  /// <summary>
  /// A flag indicating that the content has been modfied
  /// and needs reserialization
  /// </summary>
  public bool Modified { get; set; }
}
