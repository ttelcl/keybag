/*
 * (c) 2021  ttelcl / ttelcl
 */

using System;
using System.Globalization;
using System.Windows.Data;

namespace Keybag3.Converters;

public class StringMatchConverter: IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(value is string sValue && parameter is string sParameter)
    {
      return StringComparer.InvariantCultureIgnoreCase.Equals(sValue, sParameter);
    }
    else
    {
      return false;
    }
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
