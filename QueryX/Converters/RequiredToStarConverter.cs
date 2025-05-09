using System;
using System.Globalization;
using System.Windows.Data;

namespace QueryX.Converters
{
    public class RequiredToStarConverter : IValueConverter
    {
        // 将布尔值转换为 "*" 或空字符串
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRequired && isRequired)
                return "*";
            return string.Empty;
        }

        // 此处不需要实现反向转换
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
