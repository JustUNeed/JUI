using System.Windows;
using System.Windows.Controls.Primitives;

namespace JUI.Controls
{
    /// <summary>JUI 图标切换按钮: 选中态用强调色填充 + 反白前景。</summary>
    public class JuiIconToggleButton : ToggleButton
    {
        static JuiIconToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiIconToggleButton),
                new FrameworkPropertyMetadata(typeof(JuiIconToggleButton)));
        }

        /// <summary>圆角半径。</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(JuiIconToggleButton), new PropertyMetadata(new CornerRadius(6)));
    }
}
