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
using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// The purpose of the keybag file.
/// </summary>
public enum KeybagMode
{
  /// <summary>
  /// A normal keybag file
  /// </summary>
  /// <remarks>
  /// These files have the extension *.kb3.
  /// They contain "normal" keybag data.
  /// They end with a seal chunk.
  /// Saving them rewrites them fully (discarding removed chunks).
  /// </remarks>
  Kb3 = 1,

  /// <summary>
  /// A keybag history file
  /// </summary>
  /// <remarks>
  /// These files have the extension *.kb3his.
  /// They contain chunks that have been removed from a normal keybag,
  /// acting as a last resort backup.
  /// They do not have a seal chunk.
  /// They are never rewritten, only appended to.
  /// </remarks>
  Kbx = 2,
}

/// <summary>
/// Content of a keybag file at a low level: header content,
/// the file header chunk, and all other chunks.
/// </summary>
public class KeybagHeader
{
  /// <summary>
  /// Create a new KeyBagHeader
  /// </summary>
  /// <param name="signature">
  /// The file "signature" (the first 4 bytes of the file, identifies the file as a keybag3 file).
  /// Value expected to be 0x3347424B ("KBG3").
  /// </param>
  /// <param name="formatVersion">
  /// The full format version (major and minor)
  /// </param>
  /// <param name="fileEdit">
  /// The edit ID of the most recently edited node in the file. Used as a quick check
  /// to detect changes when comparing multiple copies of the same keybag.
  /// </param>
  /// <param name="keyDescriptor">
  /// Information to derive the key from a passphrase and to validate the derived key.
  /// </param>
  /// <param name="fileNode">
  /// The file header node. Provides the file id and a checksum for the checksummed
  /// fields of the file header.
  /// </param>
  /// <param name="expectedMode">
  /// The expected mode of the keybag file (normal *.kb3 or history *.kb3his).
  /// Defaults to <see cref="KeybagMode.Kb3"/>
  /// </param>
  public KeybagHeader(
    int signature,
    int formatVersion,
    ChunkId fileEdit,
    KeyData keyDescriptor,
    StoredChunk fileNode,
    KeybagMode expectedMode = KeybagMode.Kb3)
  {
    Mode = CheckSignature(signature, expectedMode);
    var minorFormatVersion = (short)(formatVersion & 0x0000FFFF);
    var majorFormatVersion = (short)((formatVersion >> 16) & 0x0000FFFF);
    if(majorFormatVersion != CurrentFormatVersionMajor)
    {
      throw new InvalidOperationException(
        $"Incompatible keybag file format (expecting {CurrentFormatVersionMajor}.X"+
        $" but got {majorFormatVersion}.{minorFormatVersion})");
    }
    MinorFormatVersion = minorFormatVersion;
    FileEdit = fileEdit;
    KeyDescriptor = keyDescriptor;
    FileChunk = fileNode;
    if(minorFormatVersion < 0x0001 || minorFormatVersion > 0x0001)
    {
      throw new InvalidDataException(
        $"Unsupported file format version {majorFormatVersion}.{minorFormatVersion}");
    }
    var editStamp = fileEdit.ToStamp();
    if(!ChunkId.LooksValid(fileEdit.Value))
    {
      throw new InvalidDataException(
        "Invalid file edit stamp in header");
    }
    if(editStamp >= DateTimeOffset.UtcNow)
    {
      throw new InvalidDataException(
        "Invalid keybag file: file edit stamp is in the future");
    }
    if(fileEdit.Value < fileNode.NodeId.Value)
    {
      throw new InvalidDataException(
        "Invalid keybag file: file edit stamp is before file creation stamp");
    }
    if(fileNode.Kind != ChunkKind.File)
    {
      throw new InvalidDataException(
        "Invalid file header node - incorrect node type");
    }
    if(fileNode.Content.Length != 0)
    {
      throw new InvalidDataException(
        "Invalid file header node - not expecting node content");
    }
    if(fileNode.ParentId.Value != 0L)
    {
      throw new InvalidDataException(
        "Invalid keybag file: expecting file header node to have no parent");
    }
    if(FileChunk.Flags != ChunkFlags.None)
    {
      throw new InvalidDataException(
        "Not expecting node flags in the file header");
    }
  }

  /// <summary>
  /// The file minor format version. (<see cref="CurrentFormatVersionMinor"/>).
  /// Note that the major version is not stored explicitly in memory.
  /// If that would change, the code handling it needs updating anyway.
  /// </summary>
  public short MinorFormatVersion { get; }

  /// <summary>
  /// The mode of the keybag file (normal *.kb3 or trashcan *.kb3his).
  /// </summary>
  public KeybagMode Mode { get; }

  /// <summary>
  /// The current expected keybag file minor format version (0x0001).
  /// Used when creating a new keybag.
  /// </summary>
  public const short CurrentFormatVersionMinor = 0x0001;

  /// <summary>
  /// The current keybag file major format version (0x0004). Included
  /// in node authentication tags to ensure that incompatible nodes are
  /// rejected.
  /// </summary>
  /// <remarks>
  /// <para>
  /// If this ever needs to increment, files in the old version need to be
  /// reencoded. Since you probably want to preserve existing Edit IDs, and
  /// therefore salts don't change, the file key must be changed (by creating
  /// a new key using a different key salt but the same passphrase). Otherwise
  /// the "never reuse a salt with the same key" rule of AESGCM would be
  /// violated.
  /// </para>
  /// </remarks>
  public const short CurrentFormatVersionMajor = 0x0004;

  /// <summary>
  /// The prefix before each chunk in V4 keybags: "CHNK" (0x4B4E4843).
  /// </summary>
  public const int ChunkPrefix = 0x4B4E4843;

  /// <summary>
  /// The signature value ('KBG3') for normal keybag files
  /// </summary>
  public const int SignatureKb3 = 0x3347424B;

  /// <summary>
  /// The signature value ('KBX3') for keybag history files
  /// </summary>
  public const int SignatureKbx = 0x3358424B;

  /// <summary>
  /// The current expected keybag file format version, combining major and minor
  /// versions.
  /// </summary>
  public const int CurrentFormatVersion =
    (((int)CurrentFormatVersionMajor) << 16) | CurrentFormatVersionMinor;

  /// <summary>
  /// Edit ID for the file: the maximum Edit ID of all nodes,
  /// (excluding seals)
  /// implying the most recent edit time stamp. This is used as
  /// a quick check if two key bag files are likely equal.
  /// This value is updated when the keybag is written to file.
  /// </summary>
  /// <remarks>
  /// This field acting as "last modified" marker is aided by the fact that
  /// chunks (other than seals) are never truly deleted, only marked as
  /// deleted ("erased") in a final edit. This means that the Edit ID of
  /// the whole keybag increases whenever a chunk is added, modified, or
  /// erased.
  /// </remarks>
  public ChunkId FileEdit { get; private set; }

  /// <summary>
  /// The File ID (alias for <see cref="FileChunk"/>.NodeId)
  /// </summary>
  public ChunkId FileId { get => FileChunk.NodeId; }

  /// <summary>
  /// The key ID for this file
  /// </summary>
  public Guid KeyId { get => KeyDescriptor.KeyId; }

  /// <summary>
  /// Key ID, passphrase validation, and key derivation data
  /// </summary>
  public KeyData KeyDescriptor { get; }

  /// <summary>
  /// The file header node (node type <see cref="ChunkKind.File"/>)
  /// </summary>
  public StoredChunk FileChunk { get; }

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
    return ChunkCryptor.TryFromPassphrase(KeyDescriptor, passphrase);
  }

  /// <summary>
  /// Check correctness of the file signature bytes
  /// </summary>
  /// <param name="signature">
  /// The signature bytes to check (as little-endian integer)
  /// </param>
  /// <param name="expectedMode">
  /// The keybag mode expected (normal *.kb3 or history *.kb3his)
  /// </param>
  /// <returns>
  /// The recognized keybag mode on success (an exception is
  /// thrown on failure). In the current implementation this
  /// is equal to <paramref name="expectedMode"/>.
  /// </returns>
  public static KeybagMode CheckSignature(
    int signature,
    KeybagMode expectedMode)
  {
    switch(signature)
    {
      case SignatureKb3:
        if(expectedMode != KeybagMode.Kb3)
        {
          throw new ArgumentException(
            "Incorrect keybag file signature - this does not look like a valid *.kb3 file");
        }
        return KeybagMode.Kb3;
      case SignatureKbx:
        if(expectedMode != KeybagMode.Kbx)
        {
          throw new ArgumentException(
            "Incorrect keybag file signature - this does not look like a valid *.kb3his file");
        }
        return KeybagMode.Kbx;
      default:
        throw new ArgumentException(
          "Incorrect keybag file signature - this does not look like a valid keybag file of any kind");
    }
  }

  /// <summary>
  /// Load the header of a keybag file
  /// </summary>
  /// <param name="file">
  /// The file to read
  /// </param>
  /// <param name="expectedMode">
  /// The expected mode of the keybag file (normal *.kb3 or history *.kb3his).
  /// Defaults to <see cref="KeybagMode.Kb3"/>
  /// </param>
  /// <returns></returns>
  public static KeybagHeader FromFile(
    Stream file,
    KeybagMode expectedMode = KeybagMode.Kb3)
  {
    Span<byte> preheader = stackalloc byte[16];
    var reader = new SpanReader();
    file.ReadExactly(preheader);
    reader
      .ReadI32(preheader, out var signature)
      .ReadI32(preheader, out var format)
      .ReadChunkId(preheader, out var editId)
      .ReadI16(preheader, out var reserved)
      .CheckEmpty(preheader);
    CheckSignature(signature, expectedMode); // fail fast
    var major = (short)(format >> 16);
    if(major != CurrentFormatVersionMajor)
    {
      throw new InvalidOperationException(
        "Incompatible file format");
    }
    var keyDescriptor = KeyData.ReadFrom(file);
    if(!StoredChunk.TrySkipChunkPrefix(file))
    {
      throw new InvalidDataException(
        "Invalid file header chunk (missing prefix)");
    }
    var fileNode = StoredChunk.ReadFromFile(
      file, ChunkId.Zero /* ignored for file header */);
    return new KeybagHeader(
      signature, format, new ChunkId(editId), keyDescriptor, fileNode,
      expectedMode);
  }

  /// <summary>
  /// Create a new <see cref="KeybagHeader"/> by reading
  /// the header of the named keybag file
  /// </summary>
  /// <param name="fileName">
  /// The name of the keybag (*.kb3) or keybag history file
  /// (*.kb3his). Must end with either of these extensions.
  /// </param>
  public static KeybagHeader FromFile(string fileName)
  {
    var mode = Path.GetExtension(fileName).ToLowerInvariant() switch {
      ".kb3" => KeybagMode.Kb3,
      ".kb3his" => KeybagMode.Kbx,
      _ => throw new ArgumentException(
        "Unsupported keybag file extension")
    };
    using(var stream = File.OpenRead(fileName))
    {
      return FromFile(stream, mode);
    }
  }

  /// <summary>
  /// Try to read the header from a keybag file, returning null on failure.
  /// This is implemented as a simple exception trap around <see cref="FromFile(string)"/>
  /// </summary>
  public static KeybagHeader? TryFromFile(string fileName)
  {
    try
    {
      return FromFile(fileName);
    }
    catch(Exception ex)
    {
      Trace.TraceError(
        $"Error opening '{fileName}': {ex.GetType().FullName} : {ex.Message}");
      return null;
    }
  }

  /// <summary>
  /// Create a brand new <see cref="KeybagHeader"/>
  /// </summary>
  /// <param name="cryptor">
  /// The encryptor providing the necessary parameters,
  /// carrying the id generator and generator of the
  /// file header node.
  /// </param>
  /// <returns>
  /// A newly created instance.
  /// </returns>
  public static KeybagHeader InitNew(
    ChunkCryptor cryptor)
  {
    var fileId = cryptor.IdGenerator.NextId();
    var fileNode = cryptor.EncryptContent(
      fileId, [], ChunkKind.File, ChunkFlags.None, fileId, ChunkId.Zero);
    var format = CurrentFormatVersion;
    return new KeybagHeader(
      SignatureKb3, format, fileId, cryptor.KeyDescriptor, fileNode);
  }

  /// <summary>
  /// Write this file header to the given stream
  /// </summary>
  /// <param name="file">
  /// The stream to write to
  /// </param>
  /// <param name="maxEditId">
  /// The highest EditId amongst all nodes to be written. This will be
  /// saved in <see cref="FileEdit"/>.
  /// </param>
  public void WriteToFile(Stream file, ChunkId maxEditId)
  {
    FileEdit = maxEditId;
    Span<byte> buffer = stackalloc byte[16];
    var writer = new SpanWriter();
    var signature = 
      Mode switch {
        KeybagMode.Kb3 => SignatureKb3,
        KeybagMode.Kbx => SignatureKbx,
        _ => throw new InvalidOperationException(
          "Invalid keybag mode")
      };
    writer
      .WriteI32(buffer, signature)
      .WriteI16(buffer, CurrentFormatVersionMinor)
      .WriteI16(buffer, CurrentFormatVersionMajor)
      .WriteChunkId(buffer, FileEdit)
      .WriteI16(buffer, 0)
      .CheckFull(buffer);
    file.Write(buffer);
    KeyDescriptor.WriteTo(file);
    FileChunk.WriteToFile(file, true);
  }

  /// <summary>
  /// Create a new Keybag History header matching this keybag
  /// </summary>
  /// <returns></returns>
  public KeybagHeader CreateHistoryHeader()
  {
    if(Mode != KeybagMode.Kb3)
    {
      throw new InvalidOperationException(
        "Cannot create a history header for a history file");
    }
    return new KeybagHeader(
      SignatureKbx,
      CurrentFormatVersion,
      FileId, // (!) use File ID, not File Edit
      KeyDescriptor,
      FileChunk.Clone(),
      KeybagMode.Kbx);
  }

  /// <summary>
  /// Verify that the provided history file header matches this
  /// keybag header
  /// </summary>
  public void ValidateMatchingHeader(
    KeybagHeader historyHeader)
  {
    if(Mode != KeybagMode.Kb3)
    {
      throw new InvalidOperationException(
        "Expecting a keybag header as instance to call this method on");
    }
    if(historyHeader.Mode != KeybagMode.Kbx)
    {
      throw new InvalidOperationException(
        "Expecting a history header as argument");
    }
    if(historyHeader.FileId != FileId)
    {
      throw new InvalidOperationException(
        "History header ID does not match keybag header");
    }
    if(historyHeader.KeyId != KeyId)
    {
      throw new InvalidOperationException(
        "History header Key ID does not match keybag header");
    }

  }

  // ---
}
