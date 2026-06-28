using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JUI.Controls
{
    /// <summary>JUI 删除角标: 叠在项右上角的小圆形按钮, 点击触发 Click 执行删除。</summary>
    public class JuiDeleteBadge : Button
    {
        static JuiDeleteBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiDeleteBadge), new FrameworkPropertyMetadata(typeof(JuiDeleteBadge)));
        }

        /// <summary>角标直径(宽高相同), 圆角自动取其一半。</summary>
        public double BadgeSize
        {
            get => (double)GetValue(BadgeSizeProperty);
            set => SetValue(BadgeSizeProperty, value);
        }
        public static readonly DependencyProperty BadgeSizeProperty =
            DependencyProperty.Register(nameof(BadgeSize), typeof(double),
                typeof(JuiDeleteBadge), new PropertyMetadata(18.0));

        /// <summary>角标内的字形(默认 Segoe MDL2 的关闭符号)。</summary>
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string),
                typeof(JuiDeleteBadge), new PropertyMetadata("\uE711"));   // ChromeClose

        /// <summary>正常态背景。</summary>
        public Brush BadgeBackground
        {
            get => (Brush)GetValue(BadgeBackgroundProperty);
            set => SetValue(BadgeBackgroundProperty, value);
        }
        public static readonly DependencyProperty BadgeBackgroundProperty =
            DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush),
                typeof(JuiDeleteBadge),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))));

        /// <summary>悬停态背景。</summary>
        public Brush HoverBackground
        {
            get => (Brush)GetValue(HoverBackgroundProperty);
            set => SetValue(HoverBackgroundProperty, value);
        }
        public static readonly DependencyProperty HoverBackgroundProperty =
            DependencyProperty.Register(nameof(HoverBackground), typeof(Brush),
                typeof(JuiDeleteBadge),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23))));
    }
}
