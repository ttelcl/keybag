/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lcl.KeyBag3.Model.Contents;

/// <summary>
/// Static helpers for interpreting and generating 
/// data in our "standard content model"
/// </summary>
/// <remarks>
/// <para>
/// The "standard content model" is a byte buffer that contains UTF8 encoded
/// characters, defining a tree of "segments". It makes heavy use of separator
/// characters taken from the ASCII control character range (characters that
/// don't see much normal use these days).
/// </para>
/// <para>
/// Content in the model consists of a top level "segment".
/// Each "segment" consists of a "tag character" followed
/// by a series of sub-segments separated by that segment's "separator"
/// character. That way the entire data forms a tree of segments.
/// That separator character cannot appear anywhere in any of the sub-segments.
/// </para>
/// <para>
/// The "tag" character and "separator" characters follow special rules:
/// </para>
/// <list type="bullet">
/// <item>
/// Both must encode to a single byte in UTF8. That is: they must be
/// ASCII characters and therefore can be treated safely as either a character
/// or as a single byte.
/// </item>
/// <item>
/// The tag character should be a printable ASCII character. Use
/// <see cref="IsValidTag(char)"/> to chek validity
/// </item>
/// <item>
/// The separator character should be a non-printable ASCII character
/// (one of the ASCII control characters), and must not appear anywhere
/// in the content of the child segments. Implication: child segments
/// must use a different separator than any of their ancestors.
/// Use <see cref="IsValidSeparator(char)"/> to check validity.
/// </item>
/// </list>
/// </remarks>

public static class ContentModel
{

  /// <summary>
  /// Test if a character is valid for use as a "separator"
  /// in a <see cref="ContentSlice"/>. Any ASCII control character
  /// is accepted, even though it may be unwise to use characters
  /// like '\r', '\n' or '\t' in many cases.
  /// </summary>
  /// <param name="separator">
  /// The character to check
  /// </param>
  public static bool IsValidSeparator(char separator)
  {
    return separator >= Ascii.NUL && separator <= Ascii.US;
  }

  /// <summary>
  /// Test if a character is valid for use as "tag character" in
  /// a <see cref="ContentSlice"/>. Any "normal" ASCII character
  /// (printable and SPACE) are accepted. These characters encode
  /// to a single byte in UTF8.
  /// </summary>
  /// <param name="tag">
  /// the character to check
  /// </param>
  public static bool IsValidTag(char tag)
  {
    return tag >= ' ' && tag < '\u007F';
  }

  /// <summary>
  /// The tag used to indicate standard leaf segments without children.
  /// Segments with this tag should declare their separator as
  /// <see cref="Ascii.NUL"/> even though that separator isn't used
  /// </summary>
  public const char LeafTag = '.';

  /// <summary>
  /// A special marker to indicate a 'separator' for leaf segments
  /// (i.e. that the segment has only one child and the separator is not used)
  /// </summary>
  public const char LeafSeparator = Ascii.NUL;

}
