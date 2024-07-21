/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// An extension of <see cref="IHasSpan{T}"/> that adds a
/// <see cref="ReadOnlyMemory{T}"/> accessor
/// </summary>
public interface IHasReadOnlyMemory<T>: IHasReadOnlySpan<T> where T : struct
{
  /// <summary>
  /// Get the content as <see cref="ReadOnlyMemory{T}"/>.
  /// Will fail if <see cref="IHasReadOnlySpan{T}.HasSpan"/>
  /// is false.
  /// </summary>
  ReadOnlyMemory<T> ReadOnlyMemory { get; }
}

/// <summary>
/// An extension of <see cref="IHasSpan{T}"/> that adds a <see cref="Memory{T}"/>
/// accessor
/// </summary>
public interface IHasMemory<T>: IHasSpan<T>, IHasReadOnlyMemory<T> where T : struct
{
  /// <summary>
  /// Get the content as <see cref="Memory{T}"/>.
  /// Will fail if <see cref="IHasReadOnlySpan{T}.HasSpan"/>
  /// is false.
  /// </summary>
  Memory<T> Memory { get; }
}

