using JUI.Controls;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace JQuick
{
    public partial class FloatingBallWindow : JuiWindow
    {
        private readonly PanelWindow _panel;

        private const double SnapThreshold = 24;
        private const double SnapGap = 0;

        private bool _dragging;
        private Point _dragStartCursor;
        private double _dragStartLeft;
        private double _dragStartTop;




        public FloatingBallWindow(PanelWindow panel)
        {
            InitializeComponent();
            _panel = panel;
            _panel.Ball = this;

            WindowStartupLocation = WindowStartupLocation.Manual;

            var cfg = ConfigStore.Current;



            // 应用已保存的悬浮球大小
            SetBallSize(cfg.BallSize);
            SetBallCornerRadius(cfg.BallCornerRadius);
            SetBallFontSize(cfg.BallFontSize);
            SetBallColor(cfg.BallColor);
            SetBallTextColor(cfg.BallTextColor);


            var area = SystemParameters.WorkArea;
            Left = double.IsNaN(cfg.BallLeft) ? area.Right - Width - 8 : cfg.BallLeft;
            Top = double.IsNaN(cfg.BallTop) ? area.Top + 200 : cfg.BallTop;

    

            LocationChanged += (_, _) =>
            {
                ConfigStore.Current.BallLeft = Left;
                ConfigStore.Current.BallTop = Top;
            };


     

        }


        /// <summary>实时设置悬浮球圆角(0~32)。</summary>
        public void SetBallCornerRadius(double radius)
        {
            radius = Math.Clamp(radius, 0, 32);
            Ball.CornerRadius = new CornerRadius(radius);
        }

        /// <summary>实时设置悬浮球中间文字大小(8~32)。</summary>
        public void SetBallFontSize(double size)
        {
            size = Math.Clamp(size, 8, 32);
            BallText.FontSize = size;
        }



        /// <summary>实时设置悬浮球大小(直径), 并把右/下边缘保持在原位附近, 避免改大后越界。</summary>
        public void SetBallSize(double size)
        {
            size = Math.Clamp(size, 24, 64);
            Width = size;
            Height = size;

            // 改大后若超出工作区, 拉回可见范围
            var area = SystemParameters.WorkArea;
            if (Left + Width > area.Right) Left = area.Right - Width;
            if (Top + Height > area.Bottom) Top = area.Bottom - Height;
            if (Left < area.Left) Left = area.Left;
            if (Top < area.Top) Top = area.Top;

            // 让正在显示的面板按新的球矩形重新贴位
          
            _panel.RepositionBesideIfShown(new Rect(Left, Top, Width, Height));
        }




        // ===== 手动拖动(绕开系统 Aero Snap 分屏) =====
        private void Ball_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;

            _dragging = true;
            _dragStartCursor = GetCursorScreenPosition();
            _dragStartLeft = Left;
            _dragStartTop = Top;

            Ball.CaptureMouse();
            e.Handled = true;
        }

        private void Ball_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;

            var cur = GetCursorScreenPosition();
            Left = _dragStartLeft + (cur.X - _dragStartCursor.X);
            Top = _dragStartTop + (cur.Y - _dragStartCursor.Y);
        }

        private void Ball_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;

            _dragging = false;
            Ball.ReleaseMouseCapture();

            SnapToEdge();
            _panel.RepositionBesideIfShown(BallRect);
            ConfigStore.Save();
        }


        public void SetBallColor(string hex)
        {
            var brush = MakeBrush(hex, "#0A84FF");
            Ball.Background = brush;
            ConfigStore.Current.BallColor = ColorToHex(((SolidColorBrush)brush).Color);
        }

        public void SetBallTextColor(string hex)
        {
            var brush = MakeBrush(hex, "#FFFFFF");
            BallText.Foreground = brush;
            ConfigStore.Current.BallTextColor = ColorToHex(((SolidColorBrush)brush).Color);
        }

        // 解析十六进制，失败回退到默认值，避免脏数据导致异常
        private static SolidColorBrush MakeBrush(string? hex, string fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var c = (Color)ColorConverter.ConvertFromString(hex);
                    var b = new SolidColorBrush(c);
                    b.Freeze();
                    return b;
                }
            }
            catch { /* 脏数据，走回退 */ }

            var fb = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
            fb.Freeze();
            return fb;
        }

        private static string ColorToHex(Color c)
            => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");







        // ===== 悬停 / 拖入展开 =====
        private void Ball_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_dragging) return;
            _panel.ShowBeside(BallRect, false);
        }

        private void Ball_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            _panel.ShowBeside(BallRect, true);
        }

        private Rect BallRect => new Rect(Left, Top, ActualWidth, ActualHeight);

        public void ShowBall() { Show(); Topmost = true; }
        public void HideBall() { Hide(); }

        // ===== 边缘吸附 =====
        private void SnapToEdge()
        {
            var area = SystemParameters.WorkArea;
            double left = Left, top = Top, w = ActualWidth, h = ActualHeight;

            if (Math.Abs(left - area.Left) <= SnapThreshold)
                left = area.Left + SnapGap;
            else if (Math.Abs(area.Right - (left + w)) <= SnapThreshold)
                left = area.Right - w - SnapGap;

            if (Math.Abs(top - area.Top) <= SnapThreshold)
                top = area.Top + SnapGap;
            else if (Math.Abs(area.Bottom - (top + h)) <= SnapThreshold)
                top = area.Bottom - h - SnapGap;

            Left = left;
            Top = top;
        }

        // ===== 取屏幕光标位置(处理 DPI), 自己写的辅助方法 =====
        private Point GetCursorScreenPosition()
        {
            GetCursorPos(out POINT pt);
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformFromDevice;
                return m.Transform(new Point(pt.X, pt.Y));
            }
            return new Point(pt.X, pt.Y);
        }

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}
