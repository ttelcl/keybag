/*
 * (c) 2021  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Keybag3.Services;

/// <summary>
/// Catalogs the theme colors and their names used in MahApps
/// </summary>
public class ThemeColorHelper  // cannot derive from ColorHelper yet!!
{
  // This needs "Color?" as key to be compatible with the mahApps color picker
  // But that is illegal in C# 9.0, so we need to fiddle with error suppression
  // (see https://stackoverflow.com/q/74544989/271323)
#pragma warning disable CS8714
  private readonly Dictionary<Color?, string> _colorNames;
#pragma warning restore CS8714
  private readonly Dictionary<string, Color> _colorByName;

  /// <summary>
  /// Create a new ThemeColorHelper
  /// </summary>
  public ThemeColorHelper()
  {
    // Sorted by increasing Saturation, then Lightness, then Hue
#pragma warning disable CS8714
    _colorNames = new Dictionary<Color?, string>() {
      {HtmlColor("#FF6D8764"), "Olive" },
      {HtmlColor("#FF647687"), "Steel" },
      {HtmlColor("#FF76608A"), "Mauve" },
      {HtmlColor("#FF87794E"), "Taupe" },
      {HtmlColor("#FF825A2C"), "Brown" },
      {HtmlColor("#FFA0522D"), "Sienna" },
      //{HtmlColor("#FF6459DF"), "Purple" },
      //{HtmlColor("#FF60A917"), "Green" },
      //{HtmlColor("#FF1BA1E2"), "Cyan" },
      //{HtmlColor("#FFF472D0"), "Pink" },
      //{HtmlColor("#FFF0A30A"), "Amber" },
      //{HtmlColor("#FFFEDE06"), "Yellow" },
      //{HtmlColor("#FF008A00"), "Emerald" },
      //{HtmlColor("#FFA20025"), "Crimson" },
      //{HtmlColor("#FF00ABA9"), "Teal" },
      //{HtmlColor("#FFA4C400"), "Lime" },
      //{HtmlColor("#FF0078D7"), "Blue" },
      //{HtmlColor("#FFD80073"), "Magenta" },
      //{HtmlColor("#FFE51400"), "Red" },
      //{HtmlColor("#FF0050EF"), "Cobalt" },
      //{HtmlColor("#FFFA6800"), "Orange" },
      //{HtmlColor("#FF6A00FF"), "Indigo" },
      //{HtmlColor("#FFAA00FF"), "Violet" },
    };
#pragma warning restore CS8714
    _colorByName = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase);
    foreach(var kvp in _colorNames)
    {
      _colorByName[kvp.Value] = kvp.Key!.Value;
    }
  }

  public IReadOnlyCollection<string> ThemeNames { get => _colorByName.Keys; }

  public IReadOnlyCollection<Color> Colors { get => _colorByName.Values; }

  // ref https://stackoverflow.com/q/74544989/271323
#pragma warning disable CS8714
  public Dictionary<Color?, string> ThemeColorNameMap { get => _colorNames; }
#pragma warning restore CS8714

  public string? this[Color? color] {
    get {
      return color!=null && _colorNames.TryGetValue(color.Value, out var name) ? name : null;
    }
  }

  public Color? this[string colorName] {
    get {
      return _colorByName.TryGetValue(colorName, out var color) ? color : null;
    }
  }

  private static Color HtmlColor(string htmlColorString)
  {
    var result = ColorConverter.ConvertFromString(htmlColorString);
    if(result == null)
    {
      throw new InvalidOperationException(
        $"Invalid HTML color conversion: {htmlColorString}");
    }
    return (Color)ColorConverter.ConvertFromString(htmlColorString);
  }

}
