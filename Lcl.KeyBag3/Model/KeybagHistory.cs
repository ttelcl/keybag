/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model;

/// <summary>
/// Manages the history file for a keybag file
/// </summary>
public class KeybagHistory
{
  private readonly Dictionary<UInt128, KeybagChunkStub> _knownChunks;

  /// <summary>
  /// Create a new KeybagHistory. If necessary this will also create
  /// the history file.
  /// </summary>
  public KeybagHistory(
    Keybag mainKeybag,
    string keybagFileName)
  {
    _knownChunks = [];
    KeybagFileName = Path.GetFullPath(keybagFileName);
    HistoryFileName = Path.ChangeExtension(KeybagFileName, ".kb3his");
    MainKeybag = mainKeybag;
    if(File.Exists(HistoryFileName))
    {
      var historyHeader = KeybagHeader.FromFile(HistoryFileName);
      mainKeybag.Header.ValidateMatchingHeader(historyHeader);
      HistoryHeader = historyHeader;
    }
    else
    {
      HistoryHeader = mainKeybag.Header.CreateHistoryHeader();
      using(var fs = File.Create(HistoryFileName))
      {
        HistoryHeader.WriteToFile(fs, HistoryHeader.FileEdit);
      }
    }
    Reload();
  }

  /// <summary>
  /// The name of the main keybag file
  /// </summary>
  public string KeybagFileName { get; }

  /// <summary>
  /// The name of the keybag history file for the main keybag file
  /// </summary>
  public string HistoryFileName { get; }

  /// <summary>
  /// The main keybag to track the history of
  /// </summary>
  public Keybag MainKeybag { get; }

  /// <summary>
  /// The header of the history file
  /// </summary>
  public KeybagHeader HistoryHeader { get; }

  internal void RegisterChunk(IKeybagChunk chunk)
  {
    _knownChunks[chunk.LongId()] = chunk.ToStub();
  }

  /// <summary>
  /// Reload the list of known chunks directly from the history file
  /// </summary>
  public void Reload()
  {
    _knownChunks.Clear();
    using(var stream = File.OpenRead(HistoryFileName))
    {
      var header = KeybagHeader.FromFile(stream, KeybagMode.Kbx);
      RegisterChunk(header.FileChunk);
      KeybagChunkStub? stub;
      while((stub = KeybagChunkStub.TryReadFrom(stream, header.FileId)) != null)
      {
        RegisterChunk(stub);
      }
    }
  }

  /// <summary>
  /// Save the history present in <see cref="MainKeybag"/> and
  /// clear the history from it.
  /// </summary>
  public void SaveHistory()
  {
    // Only save chunks to history that are at least 16 hours old.
    // Rationale: other candidates have already been replaced within a
    // few hours of creation, so probably accidental entries that were in
    // need of correction.
    var maximumEdit =
      ChunkId.FromStamp(DateTimeOffset.UtcNow.AddHours(-16));
    using(var fs = File.Open(HistoryFileName, FileMode.Open, FileAccess.ReadWrite))
    {
      // append only
      fs.Seek(0L, SeekOrigin.End);
      foreach(var chunkList in
        MainKeybag.Chunks.GetHistoryLists()
        .Where(list => list.Count > 1))
      {
        foreach(var chunk in chunkList.Skip(1))
        {
          var longId = chunk.LongId();
          if(!_knownChunks.ContainsKey(longId))
          {
            if(chunk.EditId.Value < maximumEdit.Value)
            {
              _knownChunks[longId] = chunk.ToStub();
              // Remember that WriteToHistory does not modify the chunk,
              // so there is no need to Clone() it.
              chunk.WriteToHistory(fs);
            }
            else
            {
              Trace.TraceWarning(
                $"Not saving chunk {chunk.NodeId}:{chunk.EditId.ToStampText()} to "+
                "history because it is too new");
            }
          }
        }
      }
    }
    MainKeybag.Chunks.RemoveAllHistory();
  }

  // --
}
