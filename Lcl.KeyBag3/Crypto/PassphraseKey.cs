/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Crypto;

/// <summary>
/// Stores key data derived from a passphrase (or password), using Rfc2898DeriveBytes.
/// The constructor is private, use one of the static From{Bytes|Characters|SecureString}
/// methods to create an instance.
/// </summary>
/// <remarks>
/// <para>
/// This class was adapted from a class with the same name in the FldVault.Core project.
/// </para>
/// <para>
/// In this project here a <see cref="PassphraseKey"/> instance is obtained in one of
/// the following two ways, using a passphrase obtained from the
/// user as a <see cref="SecureString"/>:
/// </para>
/// <list type="bullet">
/// <item>
/// Use <see cref="KeyData.ReadFrom(Stream)"/> to read a <see cref="KeyData"/>
/// from a keybag stream, then use
/// <see cref="TryPassphrase(SecureString, KeyData, int)"/> to create the
/// <see cref="PassphraseKey"/> to decrypt the key bag.
/// </item>
/// <item>
/// Use <see cref="TryNewFromSecureStringPair(SecureString, SecureString)"/> to create
/// a brand new key for a new keybag file.
/// </item>
/// </list>
/// </remarks>
public class PassphraseKey: KeyBuffer
{
  private readonly byte[] _salt;

  // 600000 and SHA256 as found in
  // https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2
  private const int __iterationCount = 600000;
  private static readonly HashAlgorithmName __algorithm = HashAlgorithmName.SHA256;

  /// <summary>
  /// The number of bytes generated for the salt
  /// </summary>
  public const int Saltlength = 64;

  /// <summary>
  /// The default key length in bytes (32 for AES256)
  /// </summary>
  public const int DefaultKeyLength = 32;

  /// <summary>
  /// Create a new PassphraseKey, copying the key and salt.
  /// The copy of the key will be cleared upon disposal
  /// (the salt isn't since it isn't considered to be a secret)
  /// </summary>
  private PassphraseKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> salt)
    : base(key)
  {
    _salt = salt.ToArray();
    KeyDescriptor = new KeyData(GetId(), Salt);
  }

  /// <summary>
  /// Create a PassphraseKey from a "password" given as byte span and
  /// a predefined salt (for reconstructing a previously used key).
  /// </summary>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key
  /// </param>
  /// <param name="passbytes">
  /// The bytes used as "password"
  /// </param>
  /// <param name="salt">
  /// The salt bytes
  /// </param>
  /// <returns>
  /// A new PassphraseKey instance
  /// </returns>
  public static PassphraseKey FromBytes(
    CryptoBuffer<byte> passbytes,
    ReadOnlySpan<byte> salt,
    int keyLength = DefaultKeyLength)
  {
    using(var keyBuffer = new CryptoBuffer<byte>(keyLength))
    {
      Rfc2898DeriveBytes.Pbkdf2(passbytes.Span, salt, keyBuffer.Span, __iterationCount, __algorithm);
      return new PassphraseKey(keyBuffer.Span, salt);
    }
  }

  /// <summary>
  /// Create a PassphraseKey from a "password" given as byte span and
  /// a newly created salt (for first use of a new key).
  /// </summary>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key
  /// </param>
  /// <param name="passbytes">
  /// The bytes used as "password"
  /// </param>
  /// <returns>
  /// A new PassphraseKey instance
  /// </returns>
  public static PassphraseKey FromBytes(
    CryptoBuffer<byte> passbytes,
    int keyLength = DefaultKeyLength)
  {
    var salt = new byte[Saltlength];
    RandomNumberGenerator.Fill(salt);
    return FromBytes(passbytes, salt, keyLength);
  }

  /// <summary>
  /// Create a PassphraseKey from the given bytes and salt in the info object
  /// and verify it matches the key id in the info object
  /// </summary>
  /// <param name="passbytes">
  /// The bytes used as "password"
  /// </param>
  /// <param name="info">
  /// The object containing the salt and the expected resulting key id
  /// </param>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key (default 32)
  /// </param>
  /// <returns>
  /// The key object if successful, or null if validation failed. The caller
  /// is responsible for disposing it (if not null)
  /// </returns>
  public static PassphraseKey? TryPassphrase(
    CryptoBuffer<byte> passbytes,
    KeyData info,
    int keyLength = DefaultKeyLength)
  {
    var pk = FromBytes(passbytes, info.Salt, keyLength);
    if(pk.GetId() != info.KeyId)
    {
      pk.Dispose();
      return null;
    }
    else
    {
      return pk;
    }
  }

  /// <summary>
  /// Create a PassphraseKey from a "password" given as character span and
  /// a predefined salt (for reconstructing a previously used key).
  /// </summary>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key
  /// </param>
  /// <param name="passchars">
  /// The characters used as "password"
  /// </param>
  /// <param name="salt">
  /// The salt bytes
  /// </param>
  /// <returns>
  /// A new PassphraseKey instance
  /// </returns>
  public static PassphraseKey FromCharacters(
    CryptoBuffer<char> passchars,
    ReadOnlySpan<byte> salt,
    int keyLength = DefaultKeyLength)
  {
    using(var keyBuffer = new CryptoBuffer<byte>(keyLength))
    {
      Rfc2898DeriveBytes.Pbkdf2(passchars.Span, salt, keyBuffer.Span, __iterationCount, __algorithm);
      return new PassphraseKey(keyBuffer.Span, salt);
    }
  }

  /// <summary>
  /// Create a PassphraseKey from a "password" given as character span and
  /// a newly created salt (for first use of a new key).
  /// </summary>
  /// <param name="passchars">
  /// The characters used as "password"
  /// </param>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key (default 32)
  /// </param>
  /// <returns>
  /// A new PassphraseKey instance
  /// </returns>
  public static PassphraseKey FromCharacters(
    CryptoBuffer<char> passchars,
    int keyLength = DefaultKeyLength)
  {
    var salt = new byte[Saltlength];
    RandomNumberGenerator.Fill(salt);
    return FromCharacters(passchars, salt, keyLength);
  }

  /// <summary>
  /// Create a PassphraseKey from the given characters and salt in the info object
  /// and verify it matches the key id in the info object
  /// </summary>
  /// <param name="passchars">
  /// The characters used as "password"
  /// </param>
  /// <param name="info">
  /// The object containing the salt and the expected resulting key id
  /// </param>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key (default 32)
  /// </param>
  /// <returns>
  /// The key object if successful, or null if validation failed. The caller
  /// is responsible for disposing it (if not null)
  /// </returns>
  public static PassphraseKey? TryPassphrase(
    CryptoBuffer<char> passchars,
    KeyData info,
    int keyLength = DefaultKeyLength)
  {
    var pk = FromCharacters(passchars, info.Salt, keyLength);
    if(pk.GetId() != info.KeyId)
    {
      pk.Dispose();
      return null;
    }
    else
    {
      return pk;
    }
  }

  /// <summary>
  /// Create a PassphraseKey from a "passphrase" given as SecureString and
  /// a predefined salt (for reconstructing a previously used key).
  /// </summary>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key
  /// </param>
  /// <param name="passphrase">
  /// The passphrase / password
  /// </param>
  /// <param name="salt">
  /// The salt bytes
  /// </param>
  /// <returns>
  /// A new PassphraseKey instance
  /// </returns>
  public static PassphraseKey FromSecureString(
    SecureString passphrase,
    ReadOnlySpan<byte> salt,
    int keyLength = DefaultKeyLength)
  {
    using(var characters = UnpackSecureString(passphrase))
    {
      return FromCharacters(characters, salt, keyLength);
    }
  }

  /// <summary>
  /// Create a PassphraseKey from a "passphrase" given as SecureString and
  /// a newly created salt (for first use of a new key).
  /// </summary>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key
  /// </param>
  /// <param name="passphrase">
  /// The passphrase / password
  /// </param>
  /// <returns>
  /// A new PassphraseKey instance
  /// </returns>
  public static PassphraseKey FromSecureString(
    SecureString passphrase,
    int keyLength = DefaultKeyLength)
  {
    using(var characters = UnpackSecureString(passphrase))
    {
      return FromCharacters(characters, keyLength);
    }
  }

  /// <summary>
  /// Create a PassphraseKey from the given SecureString and salt in the info object
  /// and verify it matches the key id in the info object
  /// </summary>
  /// <param name="passphrase">
  /// The passphrase / password
  /// </param>
  /// <param name="info">
  /// The object containing the salt and the expected resulting key id
  /// </param>
  /// <param name="keyLength">
  /// The number of bytes to derive as resulting key (default 32)
  /// </param>
  /// <returns>
  /// The key object if successful, or null if validation failed. The caller
  /// is responsible for disposing it (if not null)
  /// </returns>
  public static PassphraseKey? TryPassphrase(
    SecureString passphrase,
    KeyData info,
    int keyLength = DefaultKeyLength)
  {
    var pk = FromSecureString(passphrase, info.Salt, keyLength);
    if(pk.GetId() != info.KeyId)
    {
      pk.Dispose();
      return null;
    }
    else
    {
      return pk;
    }
  }

  /// <summary>
  /// Return a new <see cref="PassphraseKey"/> from a passphrase
  /// and a newly generated salt.
  /// Both arguments contain the passphrase and must be equal.
  /// </summary>
  /// <param name="ssPrimary">
  /// The primary passphrase
  /// </param>
  /// <param name="ssVerify">
  /// The re-entered passphrase which must be equal to <paramref name="ssPrimary"/>
  /// </param>
  /// <returns>
  /// The newly created <see cref="PassphraseKey"/> if both arguments were
  /// equal or null otherwise.
  /// </returns>
  public static PassphraseKey? TryNewFromSecureStringPair(
    SecureString ssPrimary,
    SecureString ssVerify)
  {
    using(var cbc1 = UnpackSecureString(ssPrimary))
    using(var cbc2 = UnpackSecureString(ssVerify))
    {
      if(cbc1.Length != cbc2.Length)
      {
        return null;
      }
      var span1 = cbc1.Span;
      var span2 = cbc2.Span;
      for(var i = 0; i < cbc1.Length; i++)
      {
        if(span1[i] != span2[i])
        {
          return null;
        }
      }
      return FromCharacters(cbc1);
    }
  }

  /// <summary>
  /// Generate a brand new salt
  /// </summary>
  public static byte[] GenerateSalt()
  {
    var salt = new byte[Saltlength];
    RandomNumberGenerator.Fill(salt);
    return salt;
  }

  /// <summary>
  /// Return a view on the salt bytes stored in this object
  /// </summary>
  public ReadOnlySpan<byte> Salt { get => _salt; }

  /// <summary>
  /// Returns the <see cref="KeyData"/> instance
  /// matching this <see cref="PassphraseKey"/>
  /// </summary>
  public KeyData KeyDescriptor { get; }

  /// <summary>
  /// Convert a <see cref="SecureString"/> to a <see cref="CryptoBuffer{T}"/>(char)
  /// </summary>
  public static CryptoBuffer<char> UnpackSecureString(SecureString ss)
  {
    var characters = new CryptoBuffer<char>(ss.Length);
    UnpackSecureString(ss, characters);
    return characters;
  }

  private static void UnpackSecureString(SecureString ss, CryptoBuffer<char> characters)
  {
    if(ss.Length != characters.Length)
    {
      throw new ArgumentException(
        "Expecting both arguments to have the same length");
    }
    // https://stackoverflow.com/a/819705/271323
    var valuePtr = IntPtr.Zero;
    var charspan = characters.Span;
    try
    {
      valuePtr = Marshal.SecureStringToGlobalAllocUnicode(ss);
      for(int i = 0; i < ss.Length; i++)
      {
        var ch = (char)(ushort)Marshal.ReadInt16(valuePtr, i * 2);
        charspan[i] = ch;
      }
    }
    finally
    {
      Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
    }
  }


}
