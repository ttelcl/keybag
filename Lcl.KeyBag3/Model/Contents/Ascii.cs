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
/// Static class with constants for selected ASCII control characters
/// </summary>
public static class Ascii
{

  /// <summary>
  /// ASCII NUL character
  /// </summary>
  public const char NUL = '\u0000';

  /// <summary>
  /// ASCII Start of Heading
  /// </summary>
  public const char SOH = '\u0001';

  /// <summary>
  /// ASCII Start of Text
  /// </summary>
  public const char STX = '\u0002';

  /// <summary>
  /// ASCII End of Text
  /// </summary>
  public const char ETX = '\u0003';

  /// <summary>
  /// ASCII End of Transmission
  /// </summary>
  public const char EOT = '\u0004';

  /// <summary>
  /// ASCII Line Feed (a.k.a. '\n')
  /// </summary>
  public const char LF = '\u000A';

  /// <summary>
  /// ASCII Vertical Tab (a.k.a. '\v')
  /// </summary>
  public const char VT = '\u000B';

  /// <summary>
  /// ASCII Form Feed (a.k.a. '\f')
  /// </summary>
  public const char FF = '\u000C';

  /// <summary>
  /// ASCII Carriage Return (a.k.a. '\r')
  /// </summary>
  public const char CR = '\u000D';

  /// <summary>
  /// ASCII Escape
  /// </summary>
  public const char ESC = '\u001B';

  /// <summary>
  /// ASCII File Separator
  /// </summary>
  public const char FS = '\u001C';

  /// <summary>
  /// ASCII Group Separator
  /// </summary>
  public const char GS = '\u001D';

  /// <summary>
  /// ASCII Record Separator
  /// </summary>
  public const char RS = '\u001E';

  /// <summary>
  /// ASCII Unit Separator
  /// </summary>
  public const char US = '\u001F';
}
