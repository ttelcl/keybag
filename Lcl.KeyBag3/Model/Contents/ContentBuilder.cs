/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Utilities;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Buffer and API for building a new ContentSlice.
/// </summary>
public class ContentBuilder: IDisposable
{

  /// <summary>
  /// Create a new ContentBuilder
  /// </summary>
  public ContentBuilder()
  {
    Buffer = new ZapBuffer<byte>();
  }

  /// <summary>
  /// The buffer into which the content slice is being built
  /// </summary>
  public ZapBuffer<byte> Buffer { get; }

  /// <summary>
  /// True after this is disposed
  /// </summary>
  public bool Disposed { get; private set; }

  /// <summary>
  /// Return the content built so far
  /// </summary>
  public Span<byte> GetContent()
  {
    ObjectDisposedException.ThrowIf(Disposed, this);
    return Buffer.All;
  }

  /// <summary>
  /// Clear the buffer and start building a new content model
  /// </summary>
  /// <param name="rootTag">
  /// The segment tag for the root segment
  /// </param>
  /// <param name="rootSeparator">
  /// The segment separator for the root segment
  /// </param>
  public SegmentBuilder StartBuilding(
    char rootTag, char rootSeparator)
  {
    Clear();
    return new SegmentBuilder(this, null, rootTag, rootSeparator);
  }

  /// <summary>
  /// Clear the state of this builder so it can be reused
  /// </summary>
  public void Clear()
  {
    ObjectDisposedException.ThrowIf(Disposed, this);
    Buffer.Clear();
  }

  internal void AppendBytes(ReadOnlySpan<byte> bytes)
  {
    Buffer.AppendSlice(bytes);
  }

  internal void AppendText(string text)
  {
    AppendBytes(Encoding.UTF8.GetBytes(text));
  }

  internal void AppendAsciiCharacter(char c)
  {
    if(c > '\u007F')
    {
      throw new ArgumentOutOfRangeException(
        nameof(c),
        "This method only allows ASCII characters as argument");
    }
    Buffer.AppendToSlice(1)[0] = (byte)c;
  }

  /// <summary>
  /// Clean up, clearing the buffer bytes to 0
  /// </summary>
  public void Dispose()
  {
    if(!Disposed)
    {
      Buffer.Dispose();
      GC.SuppressFinalize(this);
    }
  }
}
