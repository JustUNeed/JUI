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
    /// 入场: 从鼠标处淡入并上滑到"鼠标上方一点"的目标位; 出场: 下滑淡出。
    /// </summary>
    public static class JuiToast
    {
        // ---- 布局参数 ----
        private const int Gap = 6;              // 堆叠两条之间的垂直间距
        private const int CursorOffsetY = 24;   // 锚点相对鼠标上移量(避免被光标遮挡)
        private const int MaxCount = 6;         // 同屏最多条数, 超出挤掉最旧的

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
                Relayout(toast);   // instant=本条: 本条不做 Top 动画(入场动画在窗口内部做)
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

        /// <summary>
        /// 重排堆叠: 从锚点上方开始, 自下而上依次摆放每条 toast。
        /// instant 指定的那条不做 Top 动画(它走窗口内部的入场动画), 其余条平滑过渡到新位置。
        /// </summary>
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
        // ---- 动画参数(集中可调) ----
        private const double EnterSlide = 24;     // 入场起始下偏移(从鼠标处往上滑入)
        private const double ExitSlide = 12;      // 出场下滑量
        private const int FadeInMs = 160;         // 入场淡入时长
        private const int SlideInMs = 220;        // 入场上滑时长(略长于淡入, 收尾更自然)
        private const int FadeOutMs = 200;        // 出场淡出时长
        private const int MoveMs = 180;           // 堆叠重排位移时长

        private readonly DispatcherTimer _timer;
        private readonly Action<JuiToastWindow> _onClosed;
        private readonly JuiToastControl _content;
        private readonly TranslateTransform _slide = new(0, 0);
        private EventHandler? _tickHandler;       // 复用的定时器回调, 避免重复挂载
        private bool _closed;
        private bool _closing;                    // 已进入出场动画, 拦截重复触发

        /// <summary>内容期望高度: 优先实际高度, 未测量则强制测量一次取期望值。</summary>
        public double DesiredHeight
        {
            get
            {
                if (Content is FrameworkElement fe)
                {
                    if (fe.ActualHeight > 0) return fe.ActualHeight;
                    fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    return fe.DesiredSize.Height;
                }
                return 36;
            }
        }

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

            _content = new JuiToastControl { Message = message };
            _content.RenderTransform = _slide;   // 入场/出场位移作用在内容上, 窗口位置由 Top 稳定控制
            Content = _content;

            Loaded += (_, _) => PlayEnter();

            _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
            if (ms != Timeout.Infinite)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(ms);
                _tickHandler = (_, _) => { _timer.Stop(); FadeOutAndClose(); };
                _timer.Tick += _tickHandler;
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

            // 复用同一个 tick 处理器, 避免多次调用累积多个回调
            if (_tickHandler != null) _timer.Tick -= _tickHandler;
            _tickHandler = (_, _) => { _timer.Stop(); FadeOutAndClose(); };
            _timer.Tick += _tickHandler;

            _timer.Interval = TimeSpan.FromMilliseconds(autoCloseMs);
            _timer.Start();
        }

        /// <summary>堆叠重排: 横向瞬间到位, 纵向带缓动过渡(本条入场时由调用方传 animate=false)。</summary>
        public void MoveTo(double left, double top, bool animate)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = left;

            if (animate)
            {
                var move = new DoubleAnimation(Top, top, TimeSpan.FromMilliseconds(MoveMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(TopProperty, move);
            }
            else
            {
                Top = top;
            }
        }

        /// <summary>入场: 内容从下方 EnterSlide 处缓出上滑到位, 同时淡入。</summary>
        private void PlayEnter()
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeInMs)));

            _slide.Y = EnterSlide;
            var slide = new DoubleAnimation(EnterSlide, 0, TimeSpan.FromMilliseconds(SlideInMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _slide.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        /// <summary>出场: 内容下滑 ExitSlide 同时淡出, 结束后关闭。</summary>
        public void FadeOutAndClose()
        {
            if (_closed || _closing) return;
            _closing = true;
            _timer.Stop();   // 持久化 toast 手动关闭时, 防止定时器仍触发

            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(FadeOutMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => CloseNow();
            BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation(_slide.Y, ExitSlide, TimeSpan.FromMilliseconds(FadeOutMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            _slide.BeginAnimation(TranslateTransform.YProperty, slide);
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
