/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Model.TreeMath;
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// Models a key bag and its nodes at low level, without trying
/// to understand the node contents and without a model for
/// the node tree.
/// </summary>
public class Keybag
{

  /// <summary>
  /// Create a new Keybag, setting the header and header node,
  /// but no other nodes yet
  /// </summary>
  public Keybag(
    KeybagHeader header)
  {
    Chunks = new StoredChunkMap();
    Header = header;
    SealedCodeTracker = [];
    Seals = new ChunkSpace<SealedChunkList>();
    Chunks.PutChunk(Header.FileChunk);
    SealedCodeTracker.Add(Header.FileChunk.AuthCode);
    if(Chunks.FileChunk == null)
    {
      throw new InvalidOperationException(
        "Internal error");
    }
  }

  /// <summary>
  /// Create a new <see cref="Keybag"/> by reading
  /// a keybag file stream
  /// </summary>
  public static Keybag FromFile(Stream file)
  {
    var header = KeybagHeader.FromFile(file);
    var keybag = new Keybag(header);
    keybag.ReadChunks(file);
    return keybag;
  }

  /// <summary>
  /// Create a new <see cref="Keybag"/> by reading
  /// the named keybag file
  /// </summary>
  public static Keybag FromFile(string fileName)
  {
    using(var stream = File.OpenRead(fileName))
    {
      return FromFile(stream);
    }
  }

  /// <summary>
  /// The key bag header
  /// </summary>
  public KeybagHeader Header { get; }

  /// <summary>
  /// The collection of all nodes (and their history in this session).
  /// Initially this contains just the file header node.
  /// </summary>
  public StoredChunkMap Chunks { get; }

  /// <summary>
  /// The file header node
  /// </summary>
  public StoredChunk FileChunk { get => Header.FileChunk; }

  internal List<UInt128> SealedCodeTracker { get; }

  /// <summary>
  /// The collection of all seals in the keybag.
  /// </summary>
  public ChunkSpace<SealedChunkList> Seals { get; }

  /// <summary>
  /// The last encountered seal.
  /// </summary>
  public SealedChunkList? LastSeal { get; private set; }

  /// <summary>
  /// The chunk set change counter at the time of the last seal registration.
  /// </summary>
  public int LastSealChangeCount { get; private set; }

  /// <summary>
  /// True if the last chunk is a seal chunk
  /// </summary>
  public bool LastChunkIsSeal { get => LastSealChangeCount == Chunks.ChangeCounter; }

  /// <summary>
  /// True after the seals have been validated
  /// </summary>
  public bool IsSealValidated { get; private set; }

  /// <summary>
  /// Validate seal(s) in the keybag, setting <see cref="IsSealValidated"/>
  /// to true on success, or throwing an exception on failure.
  /// The case of an empty keybag is treated special.
  /// </summary>
  /// <param name="cryptor">
  /// The decryption context to use for validation
  /// </param>
  /// <exception cref="InvalidOperationException">
  /// Thrown when there are no seals at all or when the last chunk
  /// is not a seal.
  /// </exception>
  /// <exception cref="InvalidDataException">
  /// Thrown when a seal is invalid.
  /// </exception>
  public void ValidateSeals(ChunkCryptor cryptor)
  {
    Trace.TraceInformation(
      $"Validating seals for keybag {FileId}");
    IsSealValidated = false;
    if(LastSeal == null)
    {
      if(Chunks.ChunkCount == 1 && Chunks.FileChunk != null)
      {
        // Special case: allow an empty keybag containing just
        // the file header if it decrypts fine.
        using(var _ = cryptor.DecryptContent(Chunks.FileChunk))
        {
          Trace.TraceWarning(
            "Treating seal-less empty keybag with valid file chunk as valid");
          IsSealValidated = true;
          return;
        }
      }
      throw new InvalidOperationException(
        "No seal found in keybag");
    }
    if(!LastChunkIsSeal)
    {
      throw new InvalidOperationException(
        "Last chunk is not a seal");
    }
    foreach(var sealChunk in Seals.All)
    {
      if(!sealChunk.IsValid(cryptor))
      {
        throw new InvalidDataException(
          $"Seal invalid: {sealChunk.NodeId}");
      }
    }
    IsSealValidated = true;
  }

  /// <summary>
  /// The file ID for this file
  /// </summary>
  public ChunkId FileId { get => Header.FileId; }

  /// <summary>
  /// The key ID for this file
  /// </summary>
  public Guid KeyId { get => Header.KeyId; }

  /// <summary>
  /// Try to create the cryptor to decrypt and modify this keybag from the
  /// given passphrase, returning null if the passphrase is wrong.
  /// This is a CPU-intensive operation by design.
  /// </summary>
  /// <param name="passphrase">
  /// The passphrase to try
  /// </param>
  /// <returns>
  /// Either the successfully created <see cref="ChunkCryptor"/> or null
  /// if the passphrase was not correct
  /// </returns>
  public ChunkCryptor? TryMakeCryptor(SecureString passphrase)
  {
    return Header.TryMakeCryptor(passphrase);
  }

  /// <summary>
  /// Read raw nodes from the file until the file is exhausted
  /// or a zero node size marker is encountered as terminator of the
  /// node list
  /// </summary>
  /// <param name="file">
  /// The file to read from
  /// </param>
  /// <returns>
  /// the number of nodes read
  /// </returns>
  public int ReadChunks(Stream file)
  {
    var chunkCount = 0;
    Span<byte> sizeBuffer = stackalloc byte[4];
    var fileId = Header.FileId;
    while(true)
    {
      if(!StoredChunk.TrySkipChunkPrefix(file))
      {
        break;
      }
      var n = file.Read(sizeBuffer);
      if(n == 0)
      {
        // this is an error, but we can ignore it
        Trace.TraceError("Key bag file corrupt: Unexpected EOF");
        break;
      }
      var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
      if(size == 0)
      {
        Trace.TraceError("Key bag file corrupt: Unexpected empty chunk");
        break;
      }
      if(size < 40)
      {
        throw new InvalidDataException(
          "Key bag file corrupt: Invalid node size (too small)");
      }
      if(size > 0x00800000)
      {
        // Actual limit should be far smaller, but for now just
        // prevent maliciously large values
        throw new InvalidDataException(
          "Key bag file corrupt: Invalid node size (too large)");
      }
      var chunk = StoredChunk.ReadFromFile(file, fileId, size);
      if(chunk.Kind == ChunkKind.Seal)
      {
        // NOT inserted in 'Chunks'!
        var sealSeed = new SealedChunkList(SealedCodeTracker, chunk);
        Seals.Register(sealSeed);
        var sealHash = sealSeed.GetSealHash();
        Trace.TraceInformation(
          $"Seal {chunk.NodeId} hash: {sealHash}");
        LastSealChangeCount = Chunks.ChangeCounter;
        LastSeal = sealSeed;
      }
      else
      {
        chunkCount++;
        Chunks.PutChunk(chunk);
        SealedCodeTracker.Add(chunk.AuthCode);
      }
    }
    return chunkCount;
  }

  /// <summary>
  /// Create a new seal based on the latest content.
  /// This will also rebuild <see cref="SealedCodeTracker"/>
  /// </summary>
  public void Reseal(ChunkCryptor cryptor)
  {
    SealedCodeTracker.Clear();
    SealedCodeTracker.AddRange(EnumerateChunkAuthCodes());
    var seal = new SealedChunkList(SealedCodeTracker, cryptor, FileId);
    LastSeal = seal;
    LastSealChangeCount = Chunks.ChangeCounter;
  }

  /// <summary>
  /// Enumerate all chunk auth codes in the keybag that are included
  /// in a seal, in the correct order.
  /// </summary>
  internal IEnumerable<UInt128> EnumerateChunkAuthCodes()
  {
    yield return Header.FileChunk.AuthCode;
    foreach(var chunk in Chunks.CurrentChunks)
    {
      // Same order as will be written; that's why the header
      // is handled separately.
      // Note that Seals themselves do not appear in Chunks.CurrentChunks!
      if(chunk.NodeId.Value != FileId.Value)
      {
        yield return chunk.AuthCode;
      }
    }
  }

  /// <summary>
  /// Recalculate the seal hash based on the latest content.
  /// This is not used for calculating seals, but helps comparing
  /// different keybag file instances during synchronization.
  /// </summary>
  /// <returns>
  /// A base64 encoded SHA256 hash of the sealed content.
  /// </returns>
  public string CalculateSealHash()
  {
    var hash = SealedChunkList.CalculateHash(EnumerateChunkAuthCodes());
    return Convert.ToBase64String(hash);
  }

  /// <summary>
  /// Write the entire key bag (header and chunks) to a stream
  /// </summary>
  /// <param name="file">
  /// The stream to write to
  /// </param>
  /// <param name="cryptor">
  /// The cryptor used to create the new seal.
  /// </param>
  public void WriteFull(Stream file, ChunkCryptor cryptor)
  {
    // exclude seals from calculation of latest chunk update time
    var latestChunk =
      Chunks.CurrentChunks.MaxBy(sc => sc.EditId.Value)
      ?? throw new InvalidOperationException(
        "Internal error (not expecting an empty key bag)");
    if(!IsSealValidated || LastSeal==null)
    {
      if(LastSeal == null || !LastChunkIsSeal)
      {
        Reseal(cryptor);
      }
      // validate past seals and new seal
      ValidateSeals(cryptor);
    }
    Header.WriteToFile(file, latestChunk.EditId);
    // This implementation does not order chunks in any way, except for
    // skipping the header node (because it was written as part of the header already)
    foreach(var chunk in Chunks.CurrentChunks)
    {
      if(chunk.NodeId.Value != FileId.Value)
      {
        chunk.WriteToFile(file, true);
      }
    }
    // Trace.TraceWarning("Seal writing NYI");
    LastSeal!.Seal.WriteToFile(file, true);
  }

  /// <summary>
  /// Write the entire key bag (header and chunks) to a file.
  /// If <paramref name="fileName"/> already exists, a backup
  /// copy of it is made.
  /// </summary>
  public void WriteFull(string fileName, ChunkCryptor cryptor)
  {
    if(!IsSealValidated || LastSeal==null)
    {
      if(LastSeal == null || !LastChunkIsSeal)
      {
        Reseal(cryptor);
      }
      // validate past seals and new seal
      ValidateSeals(cryptor);
    }
    using(var trx = new FileWriteTransaction(fileName))
    {
      WriteFull(trx.Target, cryptor);
      trx.Commit();
    }
  }
}
