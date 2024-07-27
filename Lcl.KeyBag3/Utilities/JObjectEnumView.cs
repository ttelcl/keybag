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

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// A view on the properties of a <see cref="JObject"/> that are
/// string representations of the enum type <typeparamref name="T"/>.
/// </summary>
public class JObjectEnumView<T>:
  JObjectView<T> where T : struct, Enum
{
  /// <inheritdoc/>
  public JObjectEnumView(JObject target, T defaultValue)
    : base(target, defaultValue)
  {
  }

  /// <inheritdoc/>
  public JObjectEnumView(Func<JObject> targetProvider, T defaultValue)
    : base(targetProvider, defaultValue)
  {
  }

  /// <inheritdoc/>
  public JObjectEnumView(JObjectView other, T defaultValue)
    : base(other, defaultValue)
  {
  }

  /// <summary>
  /// Get or set a property in the underlying <see cref="JObject"/>
  /// to the string representation of the enum value.
  /// </summary>
  /// <param name="key">
  /// The property name
  /// </param>
  /// <param name="defaultValue">
  /// The value to return if the property is not found or is not a string
  /// </param>
  /// <returns></returns>
  public override T this[string key, T defaultValue] {
    get {
      var token = Target[key];
      if(token != null
        && token is JValue jv
        && jv.Type == JTokenType.String
        && jv.Value is string sv)
      {
        if(Enum.TryParse<T>(sv, out var result))
        {
          return result;
        }
        else
        {
          return defaultValue;
        }
      }
      else
      {
        return defaultValue;
      }
    }
    set { 
      var text = value.ToString();
      Target[key] = text;
    }
  }
}
