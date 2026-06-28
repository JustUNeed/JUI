using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace JUI.Controls
{
    public class ColorPickerPanel : Control
    {
        private Border? _svPanel;        // 饱和度-明度区
        private Canvas? _svCanvas;
        private Ellipse? _svCursor;
        private Rectangle? _svColorLayer; // 横向 白->纯色相
        private Border? _hueBar;          // 色相条
        private Canvas? _hueCanvas;
        private Rectangle? _hueCursor;
        private Border? _oldcolor;
        private Border? _preview;
       
        private TextBox? _hexBox;
        private ButtonBase? _ok;
        private ButtonBase? _cancel;

        private bool _svDragging;
        private bool _hueDragging;
        private bool _updating;

        // HSV 状态
        private double _h;  // 0~360
        private double _s;  // 0~1
        private double _v;  // 0~1

        static ColorPickerPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ColorPickerPanel),
                new FrameworkPropertyMetadata(typeof(ColorPickerPanel)));
        }

        #region SelectedColor

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor), typeof(Color), typeof(ColorPickerPanel),
                new FrameworkPropertyMetadata(Colors.Red, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var p = (ColorPickerPanel)d;
            if (p._updating) return;
            var c = (Color)e.NewValue;
            RgbToHsv(c, out p._h, out p._s, out p._v);
            p.UpdateVisual();
        }

        #endregion

        public event EventHandler<Color>? ColorChanged;
        public event EventHandler<Color>? Confirmed;
        public event EventHandler? Cancelled;




        public void setOldColor()
        {
            if (_oldcolor != null)
            { 
                _oldcolor.Background = new SolidColorBrush(SelectedColor);
            }  
                    
          
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            DetachHandlers();

            _svPanel = GetTemplateChild("PART_SVPanel") as Border;
            _svCanvas = GetTemplateChild("PART_SVCanvas") as Canvas;
            _svCursor = GetTemplateChild("PART_SVCursor") as Ellipse;
            _svColorLayer = GetTemplateChild("PART_SVColorLayer") as Rectangle;
            _hueBar = GetTemplateChild("PART_HueBar") as Border;
            _hueCanvas = GetTemplateChild("PART_HueCanvas") as Canvas;
            _hueCursor = GetTemplateChild("PART_HueCursor") as Rectangle;
            _preview = GetTemplateChild("PART_Preview") as Border;
            _oldcolor = GetTemplateChild("PART_oldColor") as Border;
            _hexBox = GetTemplateChild("PART_HexBox") as TextBox;
            _ok = GetTemplateChild("PART_OK") as ButtonBase;
            _cancel = GetTemplateChild("PART_Cancel") as ButtonBase;

            if (_svPanel != null)
            {
                _svPanel.MouseLeftButtonDown += SvDown;
                _svPanel.MouseMove += SvMove;
                _svPanel.MouseLeftButtonUp += SvUp;
                _svPanel.SizeChanged += (_, __) => UpdateVisual();
            }
            if (_hueBar != null)
            {
                _hueBar.MouseLeftButtonDown += HueDown;
                _hueBar.MouseMove += HueMove;
                _hueBar.MouseLeftButtonUp += HueUp;
                _hueBar.SizeChanged += (_, __) => UpdateVisual();
            }
            if (_hexBox != null)
                _hexBox.KeyDown += HexKeyDown;
            if (_ok != null)
                _ok.Click += (_, __) => Confirmed?.Invoke(this, SelectedColor);
            if (_cancel != null)
                _cancel.Click += (_, __) => Cancelled?.Invoke(this, EventArgs.Empty);

            setOldColor();


            RgbToHsv(SelectedColor, out _h, out _s, out _v);
            Dispatcher.BeginInvoke(new Action(UpdateVisual),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void DetachHandlers()
        {
            if (_svPanel != null)
            {
                _svPanel.MouseLeftButtonDown -= SvDown;
                _svPanel.MouseMove -= SvMove;
                _svPanel.MouseLeftButtonUp -= SvUp;
            }
            if (_hueBar != null)
            {
                _hueBar.MouseLeftButtonDown -= HueDown;
                _hueBar.MouseMove -= HueMove;
                _hueBar.MouseLeftButtonUp -= HueUp;
            }
        }

        #region SV 区交互

        private void SvDown(object sender, MouseButtonEventArgs e)
        {
            _svDragging = true;
            _svPanel?.CaptureMouse();
            UpdateSvFromPoint(e.GetPosition(_svPanel));
        }

        private void SvMove(object sender, MouseEventArgs e)
        {
            if (_svDragging) UpdateSvFromPoint(e.GetPosition(_svPanel));
        }

        private void SvUp(object sender, MouseButtonEventArgs e)
        {
            _svDragging = false;
            _svPanel?.ReleaseMouseCapture();
        }

        private void UpdateSvFromPoint(Point p)
        {
            if (_svPanel == null) return;
            double w = _svPanel.ActualWidth, hgt = _svPanel.ActualHeight;
            if (w <= 0 || hgt <= 0) return;
            double x = Math.Clamp(p.X, 0, w);
            double y = Math.Clamp(p.Y, 0, hgt);
            _s = x / w;
            _v = 1 - y / hgt;
            CommitHsv();
        }

        #endregion

        #region 色相条交互

        private void HueDown(object sender, MouseButtonEventArgs e)
        {
            _hueDragging = true;
            _hueBar?.CaptureMouse();
            UpdateHueFromPoint(e.GetPosition(_hueBar));
        }

        private void HueMove(object sender, MouseEventArgs e)
        {
            if (_hueDragging) UpdateHueFromPoint(e.GetPosition(_hueBar));
        }

        private void HueUp(object sender, MouseButtonEventArgs e)
        {
            _hueDragging = false;
            _hueBar?.ReleaseMouseCapture();
        }

        private void UpdateHueFromPoint(Point p)
        {
            if (_hueBar == null) return;
            double hgt = _hueBar.ActualHeight;
            if (hgt <= 0) return;
            double y = Math.Clamp(p.Y, 0, hgt);
            _h = y / hgt * 360.0;
            if (_h >= 360) _h = 359.999;
            CommitHsv();
        }

        #endregion

        private void HexKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _hexBox == null) return;
            if (TryParseHex(_hexBox.Text, out var c))
            {
                RgbToHsv(c, out _h, out _s, out _v);
                CommitHsv();
            }
            e.Handled = true;
        }

        /// <summary>把当前 HSV 应用到 SelectedColor 并刷新界面。</summary>
        private void CommitHsv()
        {
            var c = HsvToRgb(_h, _s, _v);
            _updating = true;
            SelectedColor = c;
            _updating = false;
            ColorChanged?.Invoke(this, c);
            UpdateVisual();
        }

        /// <summary>根据当前 HSV 刷新光标位置、渐变层、预览、Hex 文本。</summary>
        private void UpdateVisual()
        {
            // SV 区的横向纯色相
            if (_svColorLayer != null)
                _svColorLayer.Fill = new SolidColorBrush(HsvToRgb(_h, 1, 1));

            // SV 光标
            if (_svPanel != null && _svCursor != null && _svCanvas != null)
            {
                double w = _svPanel.ActualWidth, hgt = _svPanel.ActualHeight;
                if (w > 0 && hgt > 0)
                {
                    Canvas.SetLeft(_svCursor, _s * w - _svCursor.Width / 2);
                    Canvas.SetTop(_svCursor, (1 - _v) * hgt - _svCursor.Height / 2);
                }
            }

            // 色相光标
            if (_hueBar != null && _hueCursor != null)
            {
                double hgt = _hueBar.ActualHeight;
                if (hgt > 0)
                    Canvas.SetTop(_hueCursor, _h / 360.0 * hgt - _hueCursor.Height / 2);
            }

            var col = HsvToRgb(_h, _s, _v);
            if (_preview != null)
                _preview.Background = new SolidColorBrush(col);

      

            if (_hexBox != null && !_hexBox.IsKeyboardFocusWithin)
                _hexBox.Text = "#" + col.R.ToString("X2") + col.G.ToString("X2") + col.B.ToString("X2");
        }

    


        #region 颜色转换 / 解析

        public static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        public static void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            if (delta < 1e-6) h = 0;
            else if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
            if (h < 0) h += 360;

            s = max < 1e-6 ? 0 : delta / max;
            v = max;
        }

        public static bool TryParseHex(string? text, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().TrimStart('#');
            if (text.Length == 3)
                text = string.Concat(text[0], text[0], text[1], text[1], text[2], text[2]);
            if (text.Length != 6) return false;
            if (int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
            {
                color = Color.FromRgb(
                    (byte)((v >> 16) & 0xFF),
                    (byte)((v >> 8) & 0xFF),
                    (byte)(v & 0xFF));
                return true;
            }
            return false;
        }

        #endregion
    }
}
