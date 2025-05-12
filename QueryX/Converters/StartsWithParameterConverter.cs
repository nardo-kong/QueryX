using System;
using System.Globalization;
using System.Windows.Data;

namespace QueryX.Converters
{
    public class StartsWithParameterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? actualValue = value as string;
            string? param = parameter as string;

            if (actualValue != null && param != null)
            {
                return actualValue.StartsWith(param, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}