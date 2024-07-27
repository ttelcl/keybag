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
/// A specialization of JObjectView that includes specialized
/// alternate views, plus a default view as nullable <see cref="JToken"/>?.
/// The default accessor value is null.
/// </summary>
public class JObjectViewEx:
  JObjectView<JToken?>
{
  /// <inheritdoc/>
  public JObjectViewEx(
    JObject target) : base(target, null)
  {
    Booleans = new JObjectBooleanView(this, false);
    Strings = new JObjectNullableStringView(this);
  }

  /// <inheritdoc/>
  public JObjectViewEx(
    Func<JObject> targetProvider)
    : base(targetProvider, null)
  {
    Booleans = new JObjectBooleanView(this, false);
    Strings = new JObjectNullableStringView(this);
  }

  /// <inheritdoc/>
  public JObjectViewEx(
    JObjectView other)
    : base(other, null)
  {
    Booleans = new JObjectBooleanView(this, false);
    Strings = new JObjectNullableStringView(this);
  }

  /// <inheritdoc/>
  public override JToken? this[string key, JToken? defaultValue] {
    get => Target[key] ?? defaultValue;
    set {
      Target[key] = value;
    }
  }

  /// <summary>
  /// A view on boolean properties in the underlying <see cref="JObject"/>.
  /// </summary>
  public JObjectView<bool> Booleans {
    get;
  }

  /// <summary>
  /// A view on string properties in the underlying <see cref="JObject"/>,
  /// treating null as a valid value.
  /// </summary>
  public JObjectView<string?> Strings {
    get;
  }

  /// <summary>
  /// Return a new view on enum properties of type <typeparamref name="T"/>
  /// in the underlying <see cref="JObject"/>.
  /// </summary>
  /// <typeparam name="T">
  /// The enum type
  /// </typeparam>
  /// <param name="defaultValue">
  /// The value returned for missing or invalid properties
  /// </param>
  public JObjectView<T> GetEnumView<T>(
    T defaultValue) where T : struct, Enum
  {
    return new JObjectEnumView<T>(this, defaultValue);
  }

  // --
}
