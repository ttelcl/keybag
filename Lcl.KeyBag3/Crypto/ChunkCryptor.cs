/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Crypto;

/// <summary>
/// Wrapper around AESGCM with an API focussing on Encrypting and Decrypting
/// <see cref="StoredChunk"/>s.
/// </summary>
public sealed class ChunkCryptor: IDisposable
{
  /// <summary>
  /// Access safely via the Cryptor property
  /// </summary>
  private AesGcm? _aesgcm;

  /// <summary>
  /// Create a new NodeCryptor from a given prepared key.
  /// </summary>
  /// <param name="key">
  /// The raw key. Can be safely erased after this constructor returns.
  /// </param>
  /// <param name="keyDescriptor">
  /// The salt used to derive the key plus the key id that is used to validate
  /// correct derivation.
  /// </param>
  /// <param name="idGenerator">
  /// The ID generator to use, or null to use <see cref="ChunkIds.Default"/>
  /// </param>
  /// <param name="majorVersion">
  /// The major file version, default <see cref="KeybagHeader.CurrentFormatVersionMajor"/>.
  /// This value is part of the authentication tag (checksum) of each encrypted node.
  /// The only reason to ever pass a different value is in format conversion
  /// scenarios. 
  /// </param>
  public ChunkCryptor(
    KeyBuffer key,
    KeyData keyDescriptor,
    ChunkIds? idGenerator = null,
    short majorVersion = KeybagHeader.CurrentFormatVersionMajor)
  {
    var id2 = key.GetId();
    if(id2 != keyDescriptor.KeyId)
    {
      throw new InvalidOperationException(
        "The key and key descriptor do not match");
    }
    _aesgcm = new AesGcm(key.Bytes, 16);
    KeyDescriptor = keyDescriptor;
    IdGenerator = idGenerator ?? ChunkIds.Default;
    MajorFileVersion = majorVersion;
  }

  /// <summary>
  /// Create a new NodeCryptor from a <see cref="PassphraseKey"/>
  /// </summary>
  /// <param name="key">
  /// The key. Can be disposed after this constructor returns.
  /// </param>
  /// <param name="idGenerator">
  /// If not null: an explicit id generator (for unit test scenarios).
  /// Normally null (default) to indicate using the default id generator.
  /// </param>
  /// <param name="majorVersion">
  /// The major file version, default <see cref="KeybagHeader.CurrentFormatVersionMajor"/>.
  /// </param>
  public ChunkCryptor(
    PassphraseKey key,
    ChunkIds? idGenerator = null,
    short majorVersion = KeybagHeader.CurrentFormatVersionMajor)
    : this(key, key.KeyDescriptor, idGenerator, majorVersion)
  {
  }

  /// <summary>
  /// Try to create a NodeCryptor from a key descriptor and a matching
  /// passphrase, returning null if the passphrase does not match the descriptor.
  /// </summary>
  /// <param name="keyDescriptor">
  /// Provides the salt for key derivation and the key id for validating
  /// key correct key derivation
  /// </param>
  /// <param name="passphrase">
  /// The passphrase used together with <paramref name="keyDescriptor"/> to
  /// derive the key.
  /// </param>
  /// <param name="idGenerator">
  /// The ID generator to use to generate salt values (edit IDs). Defaults
  /// to <see cref="ChunkIds.Default"/>.
  /// </param>
  /// <returns>
  /// The successfully created NodeCryptor or null if the passphrase did not match.
  /// </returns>
  public static ChunkCryptor? TryFromPassphrase(
    KeyData keyDescriptor,
    SecureString passphrase,
    ChunkIds? idGenerator = null)
  {
    using(var key = PassphraseKey.TryPassphrase(passphrase, keyDescriptor))
    {
      if(key == null)
      {
        return null;
      }
      return new ChunkCryptor(key, keyDescriptor, idGenerator);
    }
  }

  /// <summary>
  /// Try to create a brand new <see cref="ChunkCryptor"/> based on a passphrase
  /// </summary>
  /// <param name="passphrase1">
  /// The passphrase to turn into a new key
  /// </param>
  /// <param name="passphrase2">
  /// A copy of the passphrase, entered separately by the user, used to ensure
  /// that the user typed the passphrase as intended.
  /// </param>
  /// <param name="idGenerator">
  /// The ID generator to use to generate salt values (edit IDs). Defaults
  /// to <see cref="ChunkIds.Default"/>.
  /// </param>
  /// <returns>
  /// The created <see cref="ChunkCryptor"/>, or null to indicate that the
  /// passphrases did not match.
  /// </returns>
  public static ChunkCryptor? TryCreateNew(
    SecureString passphrase1,
    SecureString passphrase2,
    ChunkIds? idGenerator = null)
  {
    using(var key = PassphraseKey.TryNewFromSecureStringPair(passphrase1, passphrase2))
    {
      if(key == null)
      {
        return null;
      }
      return new ChunkCryptor(key, key.KeyDescriptor, idGenerator);
    }
  }

  /// <summary>
  /// Describes the key being held
  /// </summary>
  public KeyData KeyDescriptor { get; }

  /// <summary>
  /// The key ID
  /// </summary>
  public Guid KeyId { get => KeyDescriptor.KeyId; }

  /// <summary>
  /// The salt (edit ID) generator to use when encrypting
  /// </summary>
  public ChunkIds IdGenerator { get; }

  /// <summary>
  /// The major file format version, which is included in auth tag
  /// calculations.
  /// </summary>
  public short MajorFileVersion { get; }

  /// <summary>
  /// Decrypt the content of a node
  /// </summary>
  /// <param name="node">
  /// The node whose content is to be decrypted
  /// </param>
  /// <param name="plaintext">
  /// The buffer for storing the decrypted content, with a size equal
  /// to <paramref name="node"/>'s <see cref="StoredChunk.Content"/> size.
  /// </param>
  /// <exception cref="CryptographicException">
  /// Thrown if the content decryption failed
  /// </exception>
  public void DecryptContent(
    StoredChunk node,
    Span<byte> plaintext)
  {
    ObjectDisposedException.ThrowIf(_aesgcm==null, this);
    Span<byte> tmp = stackalloc byte[8];
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> aad = stackalloc byte[16];
    Span<byte> authcode = stackalloc byte[16];
    ReadOnlySpan<byte> ciphertext = node.Content;

    var fileId = node.FileId;

    BinaryPrimitives.WriteUInt128LittleEndian(authcode, node.AuthCode);

    BinaryPrimitives.WriteInt64LittleEndian(tmp, node.EditId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(tmp[6..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of Edit ID to be 0");
    }
    tmp[..6].CopyTo(nonce[..6]);

    BinaryPrimitives.WriteInt64LittleEndian(tmp, node.NodeId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(tmp[6..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of Node ID to be 0");
    }
    tmp[..6].CopyTo(nonce[6..]);

    BinaryPrimitives.WriteInt64LittleEndian(aad[..8], node.ParentId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(aad[6..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of Parent ID to be 0");
    }

    // BinaryPrimitives.WriteInt16LittleEndian(aad[6..8], (short)node.Kind);
    aad[6] = (byte)node.Kind;
    aad[7] = (byte)node.Flags;

    BinaryPrimitives.WriteInt64LittleEndian(aad[8..], fileId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(aad[14..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of File ID to be 0");
    }

    BinaryPrimitives.WriteInt16LittleEndian(aad[14..], MajorFileVersion);

    Cryptor.Decrypt(nonce, ciphertext, authcode, plaintext, aad);
  }

  /// <summary>
  /// Decrypt the content of the node to a newly allocated
  /// <see cref="CryptoBuffer{T}"/>, throwing a <see cref="CryptographicException"/>
  /// if decryption fails.
  /// </summary>
  /// <param name="node">
  /// The node to decrypt
  /// </param>
  /// <returns>
  /// The raw decrypted content wrapped in a <see cref="CryptoBuffer{T}"/>
  /// </returns>
  /// <exception cref="CryptographicException">
  /// When decryption fails
  /// </exception>
  /// <exception cref="ArgumentException">
  /// When the arguments are not correct
  /// </exception>
  public CryptoBuffer<byte> DecryptContent(
    StoredChunk node)
  {
    ObjectDisposedException.ThrowIf(_aesgcm==null, this);
    var buffer = new CryptoBuffer<byte>(node.Content.Length);
    try
    {
      DecryptContent(node, buffer.Span);
      return buffer;
    }
    catch(Exception)
    {
      buffer.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Try to decrypt the node, returning null on cryptographic failure
  /// </summary>
  /// <param name="node">
  /// The node to decrypt
  /// </param>
  /// <returns>
  /// The decrypted content wrapped in a <see cref="CryptoBuffer{T}"/>
  /// on success, or null if decryption failed
  /// </returns>
  public CryptoBuffer<byte>? TryDecryptContent(
    StoredChunk node)
  {
    ObjectDisposedException.ThrowIf(_aesgcm==null, this);
    try
    {
      return DecryptContent(node);
    }
    catch(CryptographicException)
    {
      return null;
    }
  }

  /// <summary>
  /// Try to decrypt the <paramref name="source"/> chunk, storing the result in
  /// the <paramref name="sink"/> buffer upon success.
  /// </summary>
  /// <param name="source">
  /// The chunk containing the data to be decrypted
  /// </param>
  /// <param name="sink">
  /// The buffer for storing the result. Existing content will be
  /// destroyed. On failure the intermediate content will be
  /// destroyed too.
  /// </param>
  /// <returns>
  /// True on success, false on failure
  /// </returns>
  public bool TryDecryptContent(
    StoredChunk source,
    ZapBuffer<byte> sink)
  {
    ObjectDisposedException.ThrowIf(_aesgcm==null, this);
    sink.Clear();
    try
    {
      sink.Resize(source.Content.Length);
      DecryptContent(source, sink.All);
      return true;
    }
    catch(CryptographicException)
    {
      sink.Clear();
      return false;
    }
  }

  /// <summary>
  /// Encrypts the content (plaintext) for a node and wraps it
  /// into a new <see cref="StoredChunk"/>.
  /// </summary>
  /// <param name="fileId">
  /// The keybag's file ID
  /// </param>
  /// <param name="plaintext">
  /// The content to encrypt
  /// </param>
  /// <param name="kind">
  /// The node kind
  /// </param>
  /// <param name="flags">
  /// The node flags
  /// </param>
  /// <param name="nodeId">
  /// The node ID
  /// </param>
  /// <param name="parentId">
  /// The parent node ID (or 0 for a file header node)
  /// </param>
  /// <param name="editIdDangerous">
  /// Optionally an override for the edit ID. If null, a new edit
  /// ID is generated by this method. Providing a non-null value
  /// is a "here be dragons!" area, see the remarks!
  /// </param>
  /// <returns></returns>
  /// <remarks>
  /// <para>
  /// Providing an edit ID is dangerous and should be avoided, since
  /// it likely causes violation the AESGCM security requirement to never
  /// reuse a salt value for the same key.
  /// The most likely case where you would want to pass a value here is
  /// when you are re-encoding existing content and want to preserve
  /// edit IDs (timestamps). Doing so WILL reuse a previously
  /// used salt, so make sure to use a new key in that case.
  /// </para>
  /// </remarks>
  public StoredChunk EncryptContent(
    ChunkId fileId,
    ReadOnlySpan<byte> plaintext,
    ChunkKind kind,
    ChunkFlags flags,
    ChunkId nodeId,
    ChunkId parentId,
    ChunkId? editIdDangerous = null
    )
  {
    ObjectDisposedException.ThrowIf(_aesgcm==null, this);
    Span<byte> tmp = stackalloc byte[8];
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> aad = stackalloc byte[16];
    Span<byte> authcode = stackalloc byte[16];
    var editId = editIdDangerous ?? IdGenerator.NextId();

    if(kind == ChunkKind.File)
    {
      if(parentId.Value != 0)
      {
        throw new ArgumentException(
          "A file header node must have a parent ID 0");
      }
      if(plaintext.Length != 0)
      {
        throw new ArgumentException(
          "A file header node is not expected to have encrypted content");
      }
      if(editIdDangerous != null)
      {
        throw new ArgumentException(
          "A file header node must not override the edit ID");
      }
      if(fileId.Value != nodeId.Value)
      {
        throw new ArgumentException(
          "A file header node's ID is expected to be equal to the file ID");
      }
      // special case override:
      editId = nodeId;
    }
    else
    {
      if(parentId.Value == 0)
      {
        throw new ArgumentException(
          "Expecting a non-0 parent ID for non-file-header nodes");
      }
    }
    if(nodeId.Value == 0)
    {
      throw new ArgumentException(
        "Invalid node id. 0 is not valid");
    }
    if(!ChunkId.LooksValid(nodeId.Value))
    {
      throw new ArgumentException(
        "Invalid node id: out of validity range");
    }

    BinaryPrimitives.WriteInt64LittleEndian(tmp, editId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(tmp[6..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of Edit ID to be 0");
    }
    tmp[..6].CopyTo(nonce[..6]);

    BinaryPrimitives.WriteInt64LittleEndian(tmp, nodeId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(tmp[6..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of Node ID to be 0");
    }
    tmp[..6].CopyTo(nonce[6..]);

    BinaryPrimitives.WriteInt64LittleEndian(aad[..8], parentId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(aad[6..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of Parent ID to be 0");
    }

    aad[6] = (byte)kind;
    aad[7] = (byte)flags;

    BinaryPrimitives.WriteInt64LittleEndian(aad[8..], fileId.Value);
    if(BinaryPrimitives.ReadInt16LittleEndian(aad[14..]) != 0)
    {
      throw new ArgumentException(
        "Expecting upper two bytes of File ID to be 0");
    }

    BinaryPrimitives.WriteInt16LittleEndian(aad[14..], MajorFileVersion);

    var ciphertext = new byte[plaintext.Length];

    Cryptor.Encrypt(nonce, plaintext, ciphertext, authcode, aad);

    var auth128 = BinaryPrimitives.ReadUInt128LittleEndian(authcode);

    return new StoredChunk(
      kind, flags, nodeId, editId, parentId, fileId, auth128, ciphertext);
  }

  private AesGcm Cryptor {
    get {
      ObjectDisposedException.ThrowIf(_aesgcm==null, this);
      return _aesgcm;
    }
  }

  /// <summary>
  /// Clean up
  /// </summary>
  public void Dispose()
  {
    if(_aesgcm != null)
    {
      _aesgcm.Dispose();
      _aesgcm = null;
      GC.SuppressFinalize(this);
    }
  }

}
