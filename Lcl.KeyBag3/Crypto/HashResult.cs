/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lcl.KeyBag3.Utilities;
using Microsoft.VisualBasic;

namespace Lcl.KeyBag3.Crypto;

/// <summary>
/// Utility class for storing the result of applying a cryptographic hash
/// algorithm
/// </summary>
public sealed class HashResult
{
  private readonly byte[] _hash;

  /// <summary>
  /// Create a new HashResult
  /// </summary>
  public HashResult(ReadOnlySpan<byte> hash)
  {
    _hash = hash.ToArray();
  }

  /// <summary>
  /// Calculates the SHA256 hash of the provided bytes and stores the result
  /// in a new HashResult instance
  /// </summary>
  public static HashResult FromSha256(ReadOnlySpan<byte> bytesToHash)
  {
    return new HashResult(SHA256.HashData(bytesToHash));
  }

  /// <summary>
  /// Calculates the SHA256 hash of the bytes in the buffer and stores the result
  /// in a new HashResult instance
  /// </summary>
  public static HashResult FromSha256(CryptoBuffer<byte> bufferToHash)
  {
    return FromSha256(bufferToHash.Span);
  }

  /// <summary>
  /// Return the stored hash
  /// </summary>
  public ReadOnlySpan<byte> HashBytes { get => _hash; }

  /// <summary>
  /// Derive a type 4 GUID from the first 16 bytes of the stored hash
  /// </summary>
  public Guid AsGuid { get => BytesToGuid(_hash.AsSpan(0, 16)); }

  /// <summary>
  /// Create a type 4 GUID from a span of 16 bytes.
  /// Of the 128 input bits, 6 will be adjusted to make
  /// a type 4 GUID.
  /// </summary>
  /// <param name="bytes">
  /// The 16 input bytes
  /// </param>
  /// <returns>
  /// A new GUID
  /// </returns>
  public static Guid BytesToGuid(ReadOnlySpan<byte> bytes)
  {
    if(bytes.Length != 16)
    {
      throw new ArgumentException(
        "Expecting 16 bytes as input", nameof(bytes));
    }
    // We need a temporary copy of the input to be able to set
    // the 6 bits that makes a type 4 GUID
    Span<byte> span = stackalloc byte[16];
    bytes.CopyTo(span);
    span[7] = (byte)(span[7] & 0x0F | 0x40);
    span[8] = (byte)(span[8] & 0x3F | 0x80);
    return new Guid(span);
  }

}