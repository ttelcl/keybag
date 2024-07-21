/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// Implemented by objects that wrap a well-defined
/// <see cref="Span{T}"/>
/// </summary>
public interface IHasSpan<T>: IHasReadOnlySpan<T> where T: struct
{
  /// <summary>
  /// Returns the wrapped <see cref="Span{T}"/>
  /// </summary>
  /// <exception cref="InvalidOperationException">
  /// Thrown if <see cref="IHasReadOnlySpan{T}.HasSpan"/> is false
  /// </exception>
  Span<T> Span { get; }
}

/// <summary>
/// Implemented by objects that wrap a well-defined
/// <see cref="ReadOnlySpan{T}"/>
/// </summary>
public interface IHasReadOnlySpan<T> where T : struct
{
  /// <summary>
  /// Returns true if the wrapped span is available for retrieval
  /// </summary>
  bool HasSpan { get; }

  /// <summary>
  /// Returns the wrapped <see cref="ReadOnlySpan{T}"/>.
  /// </summary>
  /// <exception cref="InvalidOperationException">
  /// Thrown if <see cref="HasSpan"/> is false
  /// </exception>
  ReadOnlySpan<T> ReadOnlySpan { get; }
}

