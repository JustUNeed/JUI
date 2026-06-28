using JUI.Models;
using JUI.Theming;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace JUI.Controls
{
    /// <summary>
    /// 鼠标位置堆叠式 toast 通知, 可在任意线程/任意位置静态调用。
    /// 跑在独立 UI 线程上, 主线程卡顿不影响其动画与淡出; 配色与模板跟随 JUI 主题。
    /// </summary>
    public static class JuiToast
    {
        private const int Gap = 6;
        private const int CursorOffsetY = 24;
        private const int MaxCount = 6;

        private static Dispatcher? _dispatcher;
        private static readonly object _initLock = new();

        private static readonly List<JuiToastWindow> _stack = new();
        private static Point _anchor;

        // toast 线程上的本地资源域: 色板 + JuiToast.xaml 模板
        private static ResourceDictionary? _resources;

        /// <summary>弹出一条通知, 可在任意线程调用。返回该条的句柄, 可用于手动关闭/更新。</summary>
        public static JuiToastHandle Show(string message, int ms = 2500)
        {
            var dispatcher = EnsureThread();
            var cursor = NativeMethods.GetCursorScreenPoint();
            var handle = new JuiToastHandle();

            dispatcher.BeginInvoke(() =>
            {
                _anchor = cursor;
                while (_stack.Count >= MaxCount)
                    _stack[0].CloseNow();

                var toast = new JuiToastWindow(message, ms, EnsureResources(), OnToastClosed);
                handle.Bind(toast, dispatcher);
                _stack.Add(toast);
                toast.Show();
                Relayout(toast);
            });

            return handle;
        }

        /// <summary>弹一条不自动消失的"加载中"通知, 用返回的句柄手动关闭或更新。</summary>
        public static JuiToastHandle ShowPersistent(string message)
            => Show(message, Timeout.Infinite);

        // ---- 独立 UI 线程 ----
        private static Dispatcher EnsureThread()
        {
            if (_dispatcher != null) return _dispatcher;
            lock (_initLock)
            {
                if (_dispatcher != null) return _dispatcher;

                var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                { IsBackground = true, Name = "JuiToastThread" };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait();

                // 主题切换 → 跨线程重建资源并刷新已存在的 toast
                ThemeManager.ThemeChanged += _ =>
                    _dispatcher!.BeginInvoke(() =>
                    {
                        _resources = null;
                        var res = EnsureResources();
                        foreach (var t in _stack)
                            t.ApplyResources(res);
                    });

                return _dispatcher!;
            }
        }

        // ---- 资源域: 当前主题色板 + toast 模板 ----
        private static ResourceDictionary EnsureResources()
        {
            if (_resources != null) return _resources;

            var dict = new ResourceDictionary();

            // 1) 当前主题色板(Light/Dark)
            string themeFile = ThemeManager.Current == JuiTheme.Dark ? "Dark" : "Light";
            dict.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/JUI;component/Themes/{themeFile}.xaml", UriKind.Absolute)
            });
            // 2) toast 的 XAML 模板
            dict.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/JUI;component/Themes/Styles/JuiToast.xaml", UriKind.Absolute)
            });

            _resources = dict;
            return _resources;
        }

        private static void OnToastClosed(JuiToastWindow t)
        {
            _stack.Remove(t);
            Relayout();
        }

        private static void Relayout(JuiToastWindow? instant = null)
        {
            double baseY = _anchor.Y - CursorOffsetY;
            double left = _anchor.X;
            double y = baseY;

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var t = _stack[i];
                double h = t.ActualHeight > 0 ? t.ActualHeight : t.DesiredHeight;
                y -= h;
                t.MoveTo(left, y, animate: t != instant);
                y -= Gap;
            }
        }
    }

    /// <summary>一条 toast 的句柄: 可手动更新文本或关闭(线程安全)。</summary>
    public sealed class JuiToastHandle
    {
        private JuiToastWindow? _window;
        private Dispatcher? _dispatcher;

        internal void Bind(JuiToastWindow w, Dispatcher d) { _window = w; _dispatcher = d; }

        /// <summary>更新这条通知的文本(如"加载中"→"加载完成")。</summary>
        public void Update(string message)
            => _dispatcher?.BeginInvoke(() => _window?.SetMessage(message));

        /// <summary>关闭这条通知(带淡出)。</summary>
        public void Close()
            => _dispatcher?.BeginInvoke(() => _window?.FadeOutAndClose());

        /// <summary>更新文本, 并在指定毫秒后自动关闭(适合"加载完成"提示)。</summary>
        public void Complete(string message, int autoCloseMs = 1500)
            => _dispatcher?.BeginInvoke(() => _window?.CompleteWith(message, autoCloseMs));
    }

    internal sealed class JuiToastWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Action<JuiToastWindow> _onClosed;
        private readonly JUI.Controls.JuiToastControl _content;
        private bool _closed;

        public double DesiredHeight => Content is FrameworkElement fe ? fe.ActualHeight : 36;

        public JuiToastWindow(string message, int ms, ResourceDictionary resources,
                              Action<JuiToastWindow> onClosed)
        {
            _onClosed = onClosed;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            IsHitTestVisible = false;   // 点击穿透
            ShowActivated = false;      // 不抢焦点

            // 把色板 + 模板挂到窗口资源域, 模板里的 DynamicResource 才能解析
            Resources.MergedDictionaries.Add(resources);

            _content = new JUI.Controls.JuiToastControl { Message = message };
            Content = _content;

            Loaded += (_, _) => FadeIn();

            _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            if (ms != Timeout.Infinite)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(ms);
                _timer.Tick += (_, _) => { _timer.Stop(); FadeOutAndClose(); };
                _timer.Start();
            }
        }

        public void SetMessage(string message) => _content.Message = message;

        /// <summary>主题切换时替换资源域。</summary>
        public void ApplyResources(ResourceDictionary resources)
        {
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(resources);
        }

        public void CompleteWith(string message, int autoCloseMs)
        {
            _timer.Stop();
            _content.Message = message;
            _timer.Interval = TimeSpan.FromMilliseconds(autoCloseMs);
            _timer.Tick += (_, _) => { _timer.Stop(); FadeOutAndClose(); };
            _timer.Start();
        }

        public void MoveTo(double left, double top, bool animate)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = left;

            if (animate)
                BeginAnimation(TopProperty,
                    new DoubleAnimation(Top, top, TimeSpan.FromMilliseconds(120)));
            else
                Top = top;
        }

        private void FadeIn()
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        public void FadeOutAndClose()
        {
            if (_closed) return;
            var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
            anim.Completed += (_, _) => CloseNow();
            BeginAnimation(OpacityProperty, anim);
        }

        public void CloseNow()
        {
            if (_closed) return;
            _closed = true;
            _timer.Stop();
            try { Close(); } catch { }
            _onClosed(this);
        }
    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorScreenPoint()
            => GetCursorPos(out var p) ? new Point(p.X, p.Y) : new Point(0, 0);
    }
}
