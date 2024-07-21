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

namespace Lcl.KeyBag3.Utilities;

/// <summary>
/// Wraps file creation and writing to a temporary file before
/// moving the fully written file to its final name in transaction
/// semantics
/// </summary>
public class FileWriteTransaction: IDisposable
{

  /// <summary>
  /// Create a new FileWriteTransaction
  /// </summary>
  public FileWriteTransaction(string finalName)
  {
    FinalName = Path.GetFullPath(finalName);
    TempName = finalName + ".tmp";
    BackupName = finalName + ".bak";
    if(File.Exists(TempName))
    {
      File.Delete(TempName);
    }
    Target = File.Create(TempName);
  }

  /// <summary>
  /// The file that ultimately will be written after committing
  /// </summary>
  public string FinalName { get; }

  /// <summary>
  /// The name of the temporary name used while writing
  /// </summary>
  public string TempName { get; }

  /// <summary>
  /// The name of the file the previously existing file will be copied to
  /// </summary>
  public string BackupName { get; }

  /// <summary>
  /// The file to write to
  /// </summary>
  public FileStream Target { get; }

  /// <summary>
  /// True if this transaction was disposed (including the case it was
  /// committed)
  /// </summary>
  public bool Disposed { get; private set; }

  /// <summary>
  /// True if this transaction was committed
  /// </summary>
  public bool Committed { get; private set; }

  /// <summary>
  /// Commit the transaction, closing the temporary file and moving
  /// it to the final name (and moving the previous file to the backup
  /// if it existed)
  /// </summary>
  public void Commit()
  {
    if(Committed)
    {
      throw new InvalidOperationException(
        "Attempt to commit twice");
    }
    if(Disposed)
    {
      ObjectDisposedException.ThrowIf(
        Disposed, this);
    }
    Committed = true;
    Target.Close();
    if(File.Exists(FinalName))
    {
      if(File.Exists(BackupName))
      {
        File.Delete(BackupName);
      }
      File.Replace(TempName, FinalName, BackupName);
    }
    else
    {
      File.Move(TempName, FinalName);
    }
    Dispose();
  }

  /// <summary>
  /// Clean up. If not committed this acts as a rollback.
  /// </summary>
  public void Dispose()
  {
    if(!Disposed)
    {
      Disposed = true;
      if(!Committed)
      {
        Trace.TraceWarning($"Transaction rolled back: {FinalName}");
      }
      Target.Dispose();
      GC.SuppressFinalize(this);
    }
  }
}
