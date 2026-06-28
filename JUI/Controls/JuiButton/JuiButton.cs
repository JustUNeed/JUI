using System.Windows;
using System.Windows.Controls;

namespace JUI.Controls
{
    /// <summary>JUI 普通按钮: 强调色填充, 带悬停 / 按下 / 禁用态。</summary>
    public class JuiButton : Button
    {
        static JuiButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiButton), new FrameworkPropertyMetadata(typeof(JuiButton)));
        }

        /// <summary>圆角半径。</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(JuiButton), new PropertyMetadata(new CornerRadius(6)));
    }
}
