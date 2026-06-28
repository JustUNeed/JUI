using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JUI.Controls
{
    /// <summary>把一个 double 取一半, 再转成 CornerRadius(用于把方形角标变正圆)。</summary>
    public class HalfDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double d = value is double v ? v : 0;
            double half = d / 2.0;
            return targetType == typeof(CornerRadius)
                ? new CornerRadius(half)
                : half;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
