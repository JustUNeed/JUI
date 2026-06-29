using System.Windows;
using System.Windows.Controls;

namespace JUI.Controls
{
    /// <summary>JUI 文本框: 带圆角 / 悬停高亮 / 占位提示, 可被其它控件(如 JuiNumberBox)复用。</summary>
    public class JuiTextBox : TextBox
    {
        static JuiTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiTextBox), new FrameworkPropertyMetadata(typeof(JuiTextBox)));
        }

        /// <summary>圆角半径。</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(JuiTextBox), new PropertyMetadata(new CornerRadius(0)));

        /// <summary>占位提示文字(内容为空且未聚焦时显示)。</summary>
        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(JuiTextBox), new PropertyMetadata(string.Empty));
    }
}
