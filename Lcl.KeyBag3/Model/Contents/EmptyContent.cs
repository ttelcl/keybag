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
/// Content model for chunks without any content at all
/// </summary>
public class EmptyContent: ContentBase
{
  /// <summary>
  /// Create a new EmptyContent
  /// </summary>
  public EmptyContent()
  {
  }

}

/// <summary>
/// Implements <see cref="ContentAdapter{T}"/> for <see cref="EmptyContent"/>
/// </summary>
public class EmptyContentAdapter: ContentAdapter<EmptyContent>
{
  /// <summary>
  /// Instantiate
  /// </summary>
  public EmptyContentAdapter(
    ChunkKind kind) : base(kind)
  {
  }

  /// <summary>
  /// Create the content adapter for <see cref="ChunkKind.File"/>.
  /// </summary>
  public static EmptyContentAdapter FileHeaderAdapter()
  {
    return new EmptyContentAdapter(ChunkKind.File);
  }

  /// <inheritdoc/>
  protected internal override EmptyContent DecodeTyped(
    ReadOnlySpan<byte> decryptedContent)
  {
    if(decryptedContent.Length != 0)
    {
      throw new ArgumentException(
        "Expecting an empty content for this chunk kind");
    }
    return new EmptyContent();
  }

  /// <inheritdoc/>
  protected internal override CryptoBuffer<byte> EncodeTyped(EmptyContent model)
  {
    return new CryptoBuffer<byte>(0);
  }
}
