using JUI.Controls;
using System;
using System.Windows;
using System.Windows.Input;

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
            var area = SystemParameters.WorkArea;
            Left = double.IsNaN(cfg.BallLeft) ? area.Right - Width - 8 : cfg.BallLeft;
            Top = double.IsNaN(cfg.BallTop) ? area.Top + 200 : cfg.BallTop;

            LocationChanged += (_, _) =>
            {
                ConfigStore.Current.BallLeft = Left;
                ConfigStore.Current.BallTop = Top;
            };
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
