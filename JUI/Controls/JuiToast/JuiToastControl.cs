using System.Windows;
using System.Windows.Controls;

namespace JUI.Controls
{
    /// <summary>Toast 内容控件(lookless)。仅承载文本, 外观由 XAML 模板决定。</summary>
    public class JuiToastControl : Control
    {
        static JuiToastControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiToastControl), new FrameworkPropertyMetadata(typeof(JuiToastControl)));
        }

        /// <summary>通知文本。</summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string),
                typeof(JuiToastControl), new PropertyMetadata(string.Empty));
    }
}
