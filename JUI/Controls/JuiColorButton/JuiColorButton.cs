using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace JUI.Controls
{
    /// <summary>
    /// 色块按钮：外观为一个纯色方块，点击后弹出 HSV 取色面板。
    /// 选色结果通过 SelectedColor / HexColor 暴露，并触发 ColorChanged 事件。
    /// </summary>
    public class JuiColorButton : Button
    {
        private Popup? _popup;
        private ColorPickerPanel? _panel;

        static JuiColorButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiColorButton),
                new FrameworkPropertyMetadata(typeof(JuiColorButton)));
        }

        #region SelectedColor

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor), typeof(Color), typeof(JuiColorButton),
                new FrameworkPropertyMetadata(Colors.Red,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var btn = (JuiColorButton)d;
            var c = (Color)e.NewValue;
            // 同步只读的 HexColor
            btn.SetValue(HexColorPropertyKey, "#" +
                c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"));
            btn.ColorChanged?.Invoke(btn, c);
        }

        #endregion

        #region HexColor (只读)

        private static readonly DependencyPropertyKey HexColorPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(HexColor), typeof(string), typeof(JuiColorButton),
                new PropertyMetadata("#FF0000"));

        public static readonly DependencyProperty HexColorProperty =
            HexColorPropertyKey.DependencyProperty;

        public string HexColor => (string)GetValue(HexColorProperty);

        #endregion

        #region CornerRadius

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JuiColorButton),
                new PropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        #endregion

        /// <summary>选色变化事件，参数为最终颜色。</summary>
        public event EventHandler<Color>? ColorChanged;

        protected override void OnClick()
        {
            base.OnClick();
            OpenPopup();
        }

        private void OpenPopup()
        {
            if (_popup == null)
            {
                _panel = new ColorPickerPanel { Width = 240 };
                _panel.SelectedColor = SelectedColor;




                _panel.ColorChanged += (_, c) => SelectedColor = c;
                _panel.Confirmed += (_, c) =>
                {
                    SelectedColor = c;
                    if (_popup != null) _popup.IsOpen = false;
                };
                _panel.Cancelled += (_, __) =>
                {
                    if (_popup != null) _popup.IsOpen = false;
                };

                _popup = new Popup
                {
                    PlacementTarget = this,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = false,          // 点击外部自动关闭
                    AllowsTransparency = true,
                    PopupAnimation = PopupAnimation.Fade,
                    Child = _panel
                };



                _panel.setOldColor();


            }

            // 每次打开同步当前颜色
            if (_panel != null) {
                _panel.SelectedColor = SelectedColor;
                _panel.setOldColor();


            }
            _popup.IsOpen = true;
        }
    }
}
