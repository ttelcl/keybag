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
/// Wrapper around a JObject
/// </summary>
public class JObjectView
{
  private JObject? _directTarget;
  private Func<JObject>? _indirectTarget;

  /// <summary>
  /// Create a new JObjectView with a direct target
  /// </summary>
  public JObjectView(
    JObject target)
  {
    _directTarget = target;
    _indirectTarget = null;
  }

  /// <summary>
  /// Create a new JObjectView with an indirect target
  /// </summary>
  public JObjectView(
    Func<JObject> targetProvider)
  {
    _directTarget = null;
    _indirectTarget = targetProvider;
  }

  /// <summary>
  /// Create a new <see cref="JObjectView"/> with the same
  /// target as another.
  /// </summary>
  /// <param name="other">
  /// The other <see cref="JObjectView"/> to copy the target from
  /// </param>
  public JObjectView(
    JObjectView other)
  {
    _directTarget = other._directTarget;
    _indirectTarget = other._indirectTarget;
  }

  /// <summary>
  /// The target <see cref="JObject"/> being wrapped
  /// (direct or indirect, depending on the constructor used)
  /// </summary>
  public JObject Target => _directTarget ?? _indirectTarget!();

  /// <summary>
  /// Test if the target JObject contains a property with the given key.
  /// No type checks on the value are done.
  /// </summary>
  /// <param name="key"></param>
  /// <returns></returns>
  public bool IsPropertyKnown(string key)
  {
    return Target.ContainsKey(key);
  }

}

/// <summary>
/// A JObjectView with accessors for one specific value type.
/// This is an abstract class that requires type specific
/// implementations.
/// </summary>
public abstract class JObjectView<T>: JObjectView
{
  /// <inheritdoc/>
  public JObjectView(
    JObject target,
    T defaultValue)
    : base(target)
  {
    DefaultValue = defaultValue;
  }

  /// <inheritdoc/>
  public JObjectView(
    Func<JObject> targetProvider,
    T defaultValue)
    : base(targetProvider)
  {
    DefaultValue = defaultValue;
  }

  /// <inheritdoc/>
  public JObjectView(
    JObjectView other,
    T defaultValue)
    : base(other)
  {
    DefaultValue = defaultValue;
  }

  /// <summary>
  /// The "default default value" to use when retrieving a property
  /// that is not found when using the indexer without a default value.
  /// </summary>
  public T DefaultValue { get; set; }

  /// <summary>
  /// Get or set a property of type <typeparamref name="T"/>
  /// on the target JObject. If the property is not found,
  /// or it has an incorrect type, <paramref name="defaultValue"/>
  /// is returned.
  /// </summary>
  /// <param name="key">
  /// The property name
  /// </param>
  /// <param name="defaultValue">
  /// The default value to use if the property is not found
  /// (ignored when setting).
  /// </param>
  /// <returns></returns>
  public abstract T this[string key, T defaultValue] { get; set; }

  /// <summary>
  /// Get or set a property of type <typeparamref name="T"/>
  /// on the target JObject. If the property is not found,
  /// or it has an incorrect type, <see cref="DefaultValue"/>
  /// is returned.
  /// </summary>
  /// <param name="key">
  /// The property name
  /// </param>
  /// <returns></returns>
  public T this[string key]
  {
    get => this[key, DefaultValue];
    set {
      this[key, DefaultValue] = value;
    }
  }
}

/// <summary>
/// A view on a <see cref="JObject"/> for boolean value properties
/// </summary>
public class JObjectBooleanView: JObjectView<bool>
{
  /// <inheritdoc/>
  public JObjectBooleanView(
    JObject target,
    bool defaultValue = false)
    : base(target, defaultValue)
  {
  }

  /// <inheritdoc/>
  public JObjectBooleanView(
    Func<JObject> targetProvider,
    bool defaultValue = false)
    : base(targetProvider, defaultValue)
  {
  }

  /// <inheritdoc/>
  public JObjectBooleanView(
    JObjectView other,
    bool defaultValue = false)
    : base(other, defaultValue)
  {
  }

  /// <inheritdoc/>
  public override bool this[string key, bool defaultValue]
  {
    get
    {
      if(
        Target.TryGetValue(key, out var value)
        && value != null
        && value is JValue jv
        && jv.Value is bool b)
      {
        return b;
      }
      return defaultValue;
    }
    set => Target[key] = value;
  }
}
