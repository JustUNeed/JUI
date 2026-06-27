using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JUI.Controls
{
    /// <summary>把单个 double 转成四边相等的 Thickness, 用于把 ItemSpacing 应用为项的 Margin。</summary>
    public class DoubleToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => new Thickness(value is double d ? d : 0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is Thickness t) ? t.Left : 0.0;
    }
}
