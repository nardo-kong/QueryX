using System;
using System.Globalization;
using System.Windows.Data;

namespace QueryX.Converters // Ensure namespace matches
{
    [ValueConversion(typeof(object), typeof(bool))]
    public class NullToFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null; // Return true if value is NOT null, false otherwise
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack is not typically needed for this scenario
            throw new NotImplementedException();
        }
    }
}