using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;

using Xunit;
using Xunit.Abstractions;

using Lcl.KeyBag3.Model;
using Lcl.KeyBag3.Crypto;
using Lcl.KeyBag3.Utilities;
using Lcl.KeyBag3.Model.Contents;
using Lcl.KeyBag3.Model.Tags;
using Lcl.KeyBag3.Storage;
using Lcl.KeyBag3.Model.Contents.Blocks;

namespace UnitTest.KeyBag3;

public class UnitTest1
{
  private readonly ITestOutputHelper _outputHelper;

  public UnitTest1(ITestOutputHelper outputHelper)
  {
    _outputHelper=outputHelper;
  }

  private SecureString SecureStringFromString(string str)
  {
    SecureString? s = null;
    try
    {
      s = new SecureString();
      foreach(char c in str)
      {
        s.AppendChar(c);
      }
      return s;
    }
    catch(Exception)
    {
      s?.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Deterministically create a buffer with bytes to be used as salt.
  /// By being not-random this VIOLATES what a "salt" is, so only use this
  /// in unit tests
  /// </summary>
  private void SaltForUnitTest(Span<byte> buffer)
  {
    for(var i = 0; i < buffer.Length; i++)
    {
      buffer[i] = (byte)i;
    }
  }

  private SecureString UnitTestPassphrase(string? passphrase = null)
  {
    return SecureStringFromString(passphrase ?? "Hello World!");
  }

  private PassphraseKey UnitTestPassKey()
  {
    Span<byte> passphraseSalt = stackalloc byte[PassphraseKey.Saltlength];
    SaltForUnitTest(passphraseSalt);
    using var passphrase = UnitTestPassphrase();
    return PassphraseKey.FromSecureString(passphrase, passphraseSalt);
  }

  private ChunkCryptor UnitTestCryptor()
  {
    using var key = UnitTestPassKey();
    return new ChunkCryptor(key);
  }


  [Fact]
  public void CanEncryptDecrypt()
  {
    using var cryptor = UnitTestCryptor();
    var keyDescriptor = cryptor.KeyDescriptor;
    _outputHelper.WriteLine($"Key ID is {keyDescriptor.KeyId}.");
    _outputHelper.WriteLine($"Salt is {Convert.ToHexString(keyDescriptor.Salt)}");
    Assert.Equal(Guid.Parse("4ff79c46-0e0a-4488-9efc-1a18747d9d11"), keyDescriptor.KeyId);

    var fileId = new ChunkId(0x018F023FFEF0L); // 2024-04-21 20:01:39.5688794 +00:00
    var empty = Array.Empty<byte>();
    var fileNode =
      cryptor.EncryptContent(
        fileId,
        empty,
        ChunkKind.File,
        ChunkFlags.None,
        fileId,
        ChunkId.Zero);
    Assert.NotNull(fileNode);
    _outputHelper.WriteLine($"Auth code for unit test file node is {fileNode.AuthCode:X32}");
    // There is no syntax for UInt128 literals yet!!
    var authExpected = UInt128.Parse(
      "7A5DBBB1267C5FEE9957A06804B2C2AB", NumberStyles.HexNumber);
    Assert.Equal(authExpected, fileNode.AuthCode);

    var emptyOut = Array.Empty<byte>();
    // Would throw on failure to decrypt:
    cryptor.DecryptContent(fileNode, emptyOut);
  }

  [Fact]
  void CanConvertToBase26()
  {
    var id1 = ChunkIds.Next();
    var id2 = ChunkIds.Next();
    Assert.NotEqual(id1, id2);
    var b26_1 = id1.ToBase26();
    var b26_2 = id2.ToBase26();
    Assert.NotEqual(b26_1, b26_2);
    _outputHelper.WriteLine($"0x{id1:X12} -> {b26_1}");
    _outputHelper.WriteLine($"0x{id2:X12} -> {b26_2}");
    var id1b = ChunkId.FromBase26(b26_1);
    Assert.Equal(id1, id1b);
    var id2b = ChunkId.FromBase26(b26_2);
    Assert.Equal(id2, id2b);
  }

  [Fact]
  void CanWriteAndReadKeybag()
  {
    var fileName = "UnitTestKeyBag.kb3";

    using var cryptor = UnitTestCryptor();
    var fileId = new ChunkId(0x018F023FFEF0L); // 2024-04-21 20:01:39.5688794 +00:00
    var empty = Array.Empty<byte>();
    var fileNode =
      cryptor.EncryptContent(
        fileId,
        empty,
        ChunkKind.File,
        ChunkFlags.None,
        fileId,
        ChunkId.Zero);
    Assert.NotNull(fileNode);

    var header = new KeybagHeader(
      KeybagHeader.SignatureKb3,
      KeybagHeader.CurrentFormatVersion,
      fileId,
      cryptor.KeyDescriptor,
      fileNode);

    if(File.Exists(fileName))
    {
      File.Delete(fileName);
    }
    Assert.False(File.Exists(fileName));

    // For now save header only. That also implies EditId == FileId
    // (the logic for ordering nodes and validating that order is not in place yet)
    using(var trx = new FileWriteTransaction(fileName))
    {
      _outputHelper.WriteLine($"Writing {trx.FinalName}");
      header.WriteToFile(trx.Target, header.FileId);
      trx.Commit();
    }

    Assert.True(File.Exists(fileName));

    // read it back in blind mode
    _outputHelper.WriteLine($"Reading {fileName}");
    var keybag2 = Keybag.FromFile(fileName);
    Assert.NotNull(keybag2);
    Assert.Equal(fileId, keybag2.FileChunk.NodeId);
    Assert.Equal(
      Guid.Parse("4ff79c46-0e0a-4488-9efc-1a18747d9d11"),
      keybag2.Header.KeyDescriptor.KeyId);
    Assert.Equal(1, keybag2.Chunks.ChunkCount);

    // Try recovering the key and using the key to validate the file header node
    _outputHelper.WriteLine($"Validating key");
    using var pp = UnitTestPassphrase();
    using var cryptor2 = keybag2.TryMakeCryptor(pp);
    Assert.NotNull(cryptor2);
    using var content = cryptor2.TryDecryptContent(keybag2.FileChunk);
    Assert.NotNull(content);
    Assert.Equal(0, content.Length);
  }

  private void EntriesEqual(EntryContent e1, EntryContent e2)
  {
    Assert.Equal(e1.Label, e2.Label);
    var d1 = GetEntryDecription(e1);
    var d2 = GetEntryDecription(e2);
    Assert.Equal(d1, d2);
    Assert.Equal(e1.Tags.Count, e2.Tags.Count);
    foreach(var tag in e1.Tags)
    {
      Assert.True(e2.Tags.Contains(tag));
    }
  }

  private void DumpContent(string name, ContentBuilder builder)
  {
    var span = builder.GetContent();
    name = Path.GetFullPath(name);
    _outputHelper.WriteLine($"Saving {name}");
    using(var file = File.Create(name))
    {
      file.Write(span);
    }
  }

  private static void AddEntryDescription(
    EntryContent ec,
    string content)
  {
    if(!String.IsNullOrEmpty(content))
    {
      var block = new PlainEntryBlock() {
        Text = content,
      };
      ec.AppendBlock(block);
    }
  }

  private static string GetEntryDecription(
    EntryContent ec)
  {
    var block = ec.Blocks.FirstOrDefault(b => b is PlainEntryBlock);
    if(block is PlainEntryBlock peb)
    {
      return peb.Text;
    }
    return String.Empty;
  }

  [Fact]
  public void CanCreateEntryContent()
  {
    var entry1 = new EntryContent(
      "Entry 1",
      ["tag1", "foo", "bar"],
      []);
    AddEntryDescription(entry1, "Entry 1 description\nYadda, yadda!");
    Assert.Equal("Entry 1", entry1.Label);
    Assert.True(entry1.Tags.Count == 3);
    Assert.True(entry1.Tags.Contains("tag1"));
    Assert.True(entry1.Tags.Contains("foo"));
    Assert.True(entry1.Tags.Contains("bar"));
    Assert.True(entry1.Tags.Contains("FOO"));
    Assert.Equal(
      "Entry 1 description\nYadda, yadda!", GetEntryDecription(entry1));

    var entry2 = new EntryContent(
      "Entry 2",
      ["tag2", "foo", "FOO"],
      []);
    AddEntryDescription(entry2, "Entry 2 description!");
    Assert.Equal("Entry 2", entry2.Label);
    Assert.True(entry2.Tags.Count == 2);
    Assert.Equal("Entry 2 description!", GetEntryDecription(entry2));

    var entry3 = new EntryContent(
      "Entry 3",
      ["tag2", "empty"],
      []);
    Assert.Equal("Entry 3", entry3.Label);
    Assert.True(entry3.Tags.Count == 2);
    Assert.Equal(String.Empty, GetEntryDecription(entry3));

    using(var contentBuilder = new ContentBuilder())
    {
      entry1.Serialize(contentBuilder);
      DumpContent("entry1.entrybinary", contentBuilder);
      using(var lck = contentBuilder.Buffer.LockBuffer())
      {
        var slice = new ContentSlice(lck);
        var entryReadBack = EntryContent.FromModel(slice);
        EntriesEqual(entry1, entryReadBack);
      }

      entry2.Serialize(contentBuilder);
      DumpContent("entry2.entrybinary", contentBuilder);
      using(var lck = contentBuilder.Buffer.LockBuffer())
      {
        var slice = new ContentSlice(lck);
        var entryReadBack = EntryContent.FromModel(slice);
        EntriesEqual(entry2, entryReadBack);
      }

      entry3.Serialize(contentBuilder);
      DumpContent("entry3.entrybinary", contentBuilder);
      using(var lck = contentBuilder.Buffer.LockBuffer())
      {
        var slice = new ContentSlice(lck);
        var entryReadBack = EntryContent.FromModel(slice);
        EntriesEqual(entry3, entryReadBack);
      }
    }
  }

  [Fact]
  public void CanCreateKb3WithEntries()
  {
    var fileName = "UnitTestKeyBagEx.kb3";

    using var cryptor = UnitTestCryptor();

    var idgen = ChunkIds.Default;

    var fileId = idgen.NextId();
    var empty = Array.Empty<byte>();
    var fileNode =
      cryptor.EncryptContent(
        fileId,
        empty,
        ChunkKind.File,
        ChunkFlags.None,
        fileId,
        ChunkId.Zero);
    Assert.NotNull(fileNode);

    var header = new KeybagHeader(
      KeybagHeader.SignatureKb3,
      KeybagHeader.CurrentFormatVersion,
      fileId,
      cryptor.KeyDescriptor,
      fileNode);

    var keybag = new Keybag(header);

    var entry1 = new EntryContent(
      "Entry 1",
      ["tag1", "foo", "bar"], []);
    AddEntryDescription(entry1, "Entry 1 description\nYadda, yadda!");
    var chunk1 = new ContentChunk<EntryContent>(
      ChunkKind.Entry,
      ChunkFlags.None,
      idgen.NextId(),
      idgen.NextId(),
      fileId, // top level
      fileId,
      entry1);

    var entry2 = new EntryContent(
      "Entry 2",
      ["tag2", "foo", "FOO"], []);
    var chunk2 = new ContentChunk<EntryContent>(
      ChunkKind.Entry,
      ChunkFlags.None,
      idgen.NextId(),
      idgen.NextId(),
      fileId, // top level
      fileId,
      entry1);

    var entry3 = new EntryContent(
      "Entry 3",
      ["tag2", "empty"], []);
    var chunk3 = new ContentChunk<EntryContent>(
      ChunkKind.Entry,
      ChunkFlags.None,
      idgen.NextId(),
      idgen.NextId(),
      chunk1.NodeId, // child of entry 1
      fileId,
      entry1);

    if(File.Exists(fileName))
    {
      File.Delete(fileName);
    }
    Assert.False(File.Exists(fileName));

    var stored1 = chunk1.Serialize(cryptor);
    var stored2 = chunk2.Serialize(cryptor);
    var stored3 = chunk3.Serialize(cryptor);

    keybag.Chunks.PutChunk(stored1);
    keybag.Chunks.PutChunk(stored2);
    keybag.Chunks.PutChunk(stored3);

    using(var trx = new FileWriteTransaction(fileName))
    {
      // Use explicit transaction instead of keybag.WriteFull(string)
      // so we have easy access to the full output path.
      _outputHelper.WriteLine($"Writing {trx.FinalName}");
      keybag.WriteFull(trx.Target, cryptor);
      trx.Commit();
    }

    Assert.True(File.Exists(fileName));

    // now read it back
    _outputHelper.WriteLine($"reading back {fileName}");
    var keybag2 = Keybag.FromFile(fileName);

    Assert.Equal(4, keybag2.Chunks.ChunkCount);

    var rc0 = keybag2.Chunks.FindChunk(fileId);
    Assert.NotNull(rc0);
    var rc1 = keybag2.Chunks.FindChunk(chunk1.NodeId);
    Assert.NotNull(rc1);
    var rc2 = keybag2.Chunks.FindChunk(chunk2.NodeId);
    Assert.NotNull(rc2);
    var rc3 = keybag2.Chunks.FindChunk(chunk3.NodeId);
    Assert.NotNull(rc3);

    var registry = AdapterRegistry.Default;

    // strong typed
    var rcc1 = ContentChunk<EntryContent>.Deserialize(cryptor, rc1, registry);
    Assert.Equal(chunk1.Kind, rcc1.Kind);
    Assert.Equal(chunk1.Flags, rcc1.Flags);
    Assert.Equal(chunk1.FileId, rcc1.FileId);
    Assert.Equal(chunk1.NodeId, rcc1.NodeId);
    Assert.Equal(chunk1.EditId, rcc1.EditId);
    Assert.Equal(chunk1.ParentId, rcc1.ParentId);
    var rccc1 = rcc1.Content;
    Assert.Equal("Entry 1", rccc1.Label);
    Assert.Equal("Entry 1 description\nYadda, yadda!", GetEntryDecription(rccc1));
    Assert.Equal(3, rccc1.Tags.Count);
    Assert.True(rccc1.Tags.Contains("tag1"));
    Assert.True(rccc1.Tags.Contains("foo"));
    Assert.True(rccc1.Tags.Contains("bar"));

    // Rough typed
    var rcc1b = ContentChunk.DeserializeUntyped(cryptor, rc1, registry);
    Assert.Equal(chunk1.Kind, rcc1b.Kind);
    Assert.Equal(chunk1.Flags, rcc1b.Flags);
    Assert.Equal(chunk1.FileId, rcc1b.FileId);
    Assert.Equal(chunk1.NodeId, rcc1b.NodeId);
    Assert.Equal(chunk1.EditId, rcc1b.EditId);
    Assert.Equal(chunk1.ParentId, rcc1b.ParentId);
    var rccc1b = rcc1b.GetContentAs<EntryContent>();
    Assert.Equal("Entry 1", rccc1b.Label);
    Assert.Equal("Entry 1 description\nYadda, yadda!", GetEntryDecription(rccc1b));
    Assert.Equal(3, rccc1b.Tags.Count);
    Assert.True(rccc1b.Tags.Contains("tag1"));
    Assert.True(rccc1b.Tags.Contains("foo"));
    Assert.True(rccc1b.Tags.Contains("bar"));

    // Validate seal
    _outputHelper.WriteLine("Validating seal");
    Assert.NotNull(keybag2.LastSeal);
    Assert.True(keybag2.LastChunkIsSeal);
    Assert.False(keybag2.IsSealValidated);
    keybag2.ValidateSeals(cryptor);
    Assert.True(keybag2.IsSealValidated);
  }

  [Fact]
  public void CanUseContextTags()
  {
    ContextTag? ct;
    ct = ContextTag.TryParse("kb2[123].foo=bar");
    Assert.NotNull(ct);
    Assert.Equal("kb2", ct.Name);
    Assert.False(ct.Hidden);
    Assert.Equal("123", ct.Context);
    Assert.Equal("foo", ct.Field);
    Assert.Equal("bar", ct.Value);
    Assert.Equal("kb2[123].foo=bar", ct.ToString());
    ct = ContextTag.TryParse("?kb2[123]");
    Assert.NotNull(ct);
    Assert.Equal("kb2", ct.Name);
    Assert.True(ct.Hidden);
    Assert.Equal("123", ct.Context);
    Assert.Equal("", ct.Field);
    Assert.Null(ct.Value);
    Assert.Equal("?kb2[123]", ct.ToString());
    ct = ContextTag.TryParse("?kb2.foo=");
    Assert.NotNull(ct);
    Assert.Equal("kb2", ct.Name);
    Assert.True(ct.Hidden);
    Assert.Equal("", ct.Context);
    Assert.Equal("foo", ct.Field);
    Assert.Equal("", ct.Value);
    Assert.Equal("?kb2.foo=", ct.ToString());
    ct = ContextTag.TryParse("!kb2[123].foo=bar");
    Assert.Null(ct);
    ct = ContextTag.TryParse("kb2[123].foo=bar", null, true, true, null, true);
    Assert.NotNull(ct);
    ct = ContextTag.TryParse("kb2[123].foo", null, true, true, null, true);
    Assert.Null(ct);
    ct = ContextTag.TryParse("kb2[123].foo=", null, true, true, null, true);
    Assert.NotNull(ct);
    ct = ContextTag.TryParse("kb2[123]=bar", null, true, true, null, true);
    Assert.Null(ct);
    ct = ContextTag.TryParse("kb2.foo=bar", null, true, true, null, true);
    Assert.Null(ct);
  }

  [Fact]
  public void DriveApiTest()
  {
    TestDir(@"C:\");
    TestDir(@"E:\");
    TestDir(@"F:\");
    TestDir(@"K:\");
    TestDir(@"E:\src");
    TestDir(@"E:\src\vs2022");
  }

  private void TestDir(string dir)
  {
    var fid = FileIdentifier.FromPath(dir);
    if(fid==null)
    {
      _outputHelper.WriteLine($"{dir} -> NOT FOUND");
    }
    else
    {
      _outputHelper.WriteLine($"{dir} -> {fid.VolumeSerial}:{fid.FileIndex:X16}");
    }
  }

}
