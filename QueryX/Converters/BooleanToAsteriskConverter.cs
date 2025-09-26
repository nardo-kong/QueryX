using System;
using System.Globalization;
using System.Windows.Data;

namespace QueryX.Converters
{
    /// <summary>
    /// Converts a boolean value to an asterisk (*) string for indicating unsaved changes
    /// </summary>
    public class BooleanToAsteriskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDirty && isDirty)
            {
                return " *";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}