/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Crypto;

/// <summary>
/// Data that together with a passphrase determines a key and helps
/// verifying correctness of that passphrase.
/// </summary>
/// <remarks>
/// <para>
/// You normally get a KeyData instance via the <see cref="ReadFrom(Stream)"/>
/// method, reading it from a stream previously written by <see cref="WriteTo(Stream)"/>.
/// </para>
/// <para>
/// To create a brand new KeyData, use <see cref="PassphraseKey.TryNewFromSecureStringPair"/>
/// to create a <see cref="PassphraseKey"/>, and retrieve its <see cref="PassphraseKey.KeyDescriptor"/>
/// property.
/// </para>
/// <para>
/// Use a KeyData to obtain and validate a <see cref="PassphraseKey"/> instance using
/// its <see cref="PassphraseKey.FromSecureString(SecureString, int)"/> method.
/// </para>
/// </remarks>
public class KeyData
{
  private readonly byte[] _salt;

  /// <summary>
  /// Create a new KeyData instance.
  /// </summary>
  public KeyData(Guid keyId, ReadOnlySpan<byte> salt)
  {
    KeyId = keyId;
    if(salt.Length != 64)
    {
      throw new ArgumentOutOfRangeException(nameof(salt), "Expecting a 64 byte salt value");
    }
    _salt = salt.ToArray();
  }

  /// <summary>
  /// Read a KeyData instance from a binary stream (reading the next 80 bytes)
  /// </summary>
  /// <param name="stream">
  /// The stream to read from
  /// </param>
  /// <returns>
  /// A new KeyData instance
  /// </returns>
  public static KeyData ReadFrom(Stream stream)
  {
    Span<byte> bytes = stackalloc byte[80];
    stream.ReadExactly(bytes);
    return FromBytes(bytes);
  }

  /// <summary>
  /// Write this KeyDaya instance to a binary stream (writing 80 bytes)
  /// </summary>
  /// <param name="stream">
  /// The stream to write to
  /// </param>
  public void WriteTo(Stream stream)
  {
    Span<byte> bytes = stackalloc byte[80];
    SerializeTo(bytes);
    stream.Write(bytes);
  }

  /// <summary>
  /// Serialize this <see cref="KeyData"/> to an 80 byte binary buffer
  /// </summary>
  /// <param name="bytes"></param>
  public void SerializeTo(Span<byte> bytes)
  {
    KeyId.TryWriteBytes(bytes[..16]);
    _salt.CopyTo(bytes[16..]);
  }

  /// <summary>
  /// The key identifier, uniquely determined by the key and used
  /// for validating the correctness.
  /// </summary>
  public Guid KeyId { get; }

  /// <summary>
  /// The salt for key derivation
  /// </summary>
  public ReadOnlySpan<byte> Salt { get => _salt; }

  /// <summary>
  /// Serialize this <see cref="KeyData"/> to a base64 string
  /// </summary>
  /// <returns></returns>
  public string ToBase64()
  {
    Span<byte> bytes = stackalloc byte[80];
    SerializeTo(bytes);
    return Base64Url.EncodeToString(bytes);
  }

  /// <summary>
  /// Create a KeyData instance from its serialized byte sequence
  /// </summary>
  /// <param name="bytes">
  /// The 80 bytes if key descriptor data.
  /// </param>
  /// <returns>
  /// A new KeyData instance
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  public static KeyData FromBytes(ReadOnlySpan<byte> bytes)
  {
    if(bytes.Length != 80)
    {
      throw new ArgumentOutOfRangeException(
        nameof(bytes), "Expecting 80 bytes of key descriptor data");
    }
    var keyId = new Guid(bytes[..16]);
    var salt = bytes[16..];
    return new KeyData(keyId, salt);
  }

  /// <summary>
  /// Create a <see cref="KeyData"/> from 80 bytes worth of its base64 encoded serialized binary form
  /// </summary>
  /// <param name="keydata64"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static KeyData FromBase64(string keydata64)
  {
    Span<byte> keydata = stackalloc byte[80];
    var n = Base64Url.DecodeFromChars(keydata64, keydata);
    if(n!=80)
    {
      throw new InvalidOperationException(
        $"Expecting 80 bytes worth of base64 data");
    }
    return FromBytes(keydata);
  }

}
