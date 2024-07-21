/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// An ordered list of non-seal chunks plus a seal chunk
/// for them.
/// </summary>
public class SealedChunkList: IKeybagChunk
{
  private readonly byte[] _sealedHash;

  /// <summary>
  /// Create a new SealedChunkList
  /// </summary>
  /// <param name="sealedCodes">
  /// The list of sealed chunk authentication codes
  /// </param>
  /// <param name="seal">
  /// The seal chunk
  /// </param>
  public SealedChunkList(
    IEnumerable<UInt128> sealedCodes,
    StoredChunk seal)
  {
    Seal = seal;
    SealedCodes = sealedCodes.ToList().AsReadOnly();
    _sealedHash = CalculateHash(SealedCodes);
  }

  /// <summary>
  /// Create a new SealedChunkList including a new seal chunk
  /// </summary>
  /// <param name="sealedCodes">
  /// The authentication codes to seal
  /// </param>
  /// <param name="cryptor">
  /// The cryptor to use for sealing
  /// </param>
  /// <param name="fileId">
  /// The file ID of the keybag the seal is in
  /// </param>
  public SealedChunkList(
    IEnumerable<UInt128> sealedCodes,
    ChunkCryptor cryptor,
    ChunkId fileId)
    : this(sealedCodes, CreateSeal(sealedCodes, cryptor, fileId))
  {
  }

  /// <summary>
  /// The seal for this list
  /// </summary>
  public StoredChunk Seal { get; }

  /// <summary>
  /// The chunks being sealed
  /// </summary>
  public IReadOnlyList<UInt128> SealedCodes { get; }

  /// <summary>
  /// Implements <see cref="IKeybagChunk.Kind"/> (proxying <see cref="Seal"/>)
  /// </summary>
  public ChunkKind Kind => Seal.Kind;

  /// <summary>
  /// Implements <see cref="IKeybagChunk.Flags"/> (proxying <see cref="Seal"/>)
  /// </summary>
  public ChunkFlags Flags => Seal.Flags;

  /// <summary>
  /// Implements <see cref="IKeybagChunk.NodeId"/> (proxying <see cref="Seal"/>)
  /// </summary>
  public ChunkId NodeId => Seal.NodeId;

  /// <summary>
  /// Implements <see cref="IKeybagChunk.EditId"/> (proxying <see cref="Seal"/>)
  /// </summary>
  public ChunkId EditId => Seal.EditId;

  /// <summary>
  /// Implements <see cref="IKeybagChunk.ParentId"/> (proxying <see cref="Seal"/>)
  /// </summary>
  public ChunkId ParentId => Seal.ParentId;

  /// <summary>
  /// Implements <see cref="IKeybagChunk.FileId"/> (proxying <see cref="Seal"/>)
  /// </summary>
  public ChunkId FileId => Seal.FileId;

  /// <summary>
  /// Return the base64 encoded seal hash
  /// </summary>
  public string GetSealHash()
  {
    return Convert.ToBase64String(_sealedHash);
  }

  /// <summary>
  /// Test if this seal is valid
  /// </summary>
  public bool IsValid(ChunkCryptor cryptor)
  {
    if(SealedCodes.Count == 0)
    {
      Trace.TraceError("Invalid Seal: chunk list is empty");
      return false;
    }
    using(var decrypted = cryptor.DecryptContent(Seal))
    {
      if(!decrypted.IsSame(_sealedHash))
      {
        Trace.TraceError("Invalid Seal: hash mismatch");
        return false;
      }
      else
      {
        return true;
      }
    }
  }

  /// <summary>
  /// Calculate the hash of the sealed chunks' authentication codes
  /// </summary>
  public static byte[] CalculateHash(IEnumerable<UInt128> codes)
  {
    var buffer = new byte[16];
    using(var hasher = SHA256.Create())
    {
      foreach(var authCode in codes)
      {
        BinaryPrimitives.WriteUInt128LittleEndian(buffer, authCode);
        hasher.TransformBlock(buffer, 0, 16, null, 0);
      }
      return hasher.TransformFinalBlock([], 0, 0);
    }
  }

  /// <summary>
  /// Create a new seal chunk
  /// </summary>
  /// <param name="sealedCodes">
  /// The authentication codes to seal
  /// </param>
  /// <param name="cryptor">
  /// The cryptor to use for sealing
  /// </param>
  /// <param name="fileId">
  /// The file ID of the keybag the seal is in
  /// </param>
  /// <returns>
  /// A new chunk
  /// </returns>
  public static StoredChunk CreateSeal(
    IEnumerable<UInt128> sealedCodes,
    ChunkCryptor cryptor,
    ChunkId fileId)
  {
    var hash = CalculateHash(sealedCodes);
    var chunk = cryptor.EncryptContent(
      fileId,
      hash,
      ChunkKind.Seal,
      ChunkFlags.Infrastructure,
      cryptor.IdGenerator.NextId(),
      fileId);
    return chunk;
  }

}
