using System.Windows;
using System.Windows.Controls;

namespace JUI.Controls
{
    /// <summary>JUI 图标按钮: 透明底, 悬停 / 按下显示表面色。默认用 Segoe MDL2 字形。</summary>
    public class JuiIconButton : Button
    {
        static JuiIconButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiIconButton), new FrameworkPropertyMetadata(typeof(JuiIconButton)));
        }

        /// <summary>圆角半径。</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(JuiIconButton), new PropertyMetadata(new CornerRadius(6)));
    }
}
