using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace JUI.Controls
{
    /// <summary>
    /// JUI 透明承载窗口。窗口自身始终透明, 背景色 / 边框 / 圆角全部由模板内的 RootBorder 绘制。
    /// 支持四种模式: 常规 / 无标题栏 / 侵入式 / 完全无边框, 可用方法直接切换。
    /// 标题栏提供最小化 / 最大化(还原) / 关闭, 并保留系统原生的双击与拖到顶部最大化。
    /// </summary>
    [TemplatePart(Name = PartMinButton, Type = typeof(Button))]
    [TemplatePart(Name = PartMaxButton, Type = typeof(Button))]
    [TemplatePart(Name = PartCloseButton, Type = typeof(Button))]
    public class JuiWindow : Window
    {
        public const string PartMinButton = "PART_MinButton";
        public const string PartMaxButton = "PART_MaxButton";
        public const string PartCloseButton = "PART_CloseButton";

        // Segoe MDL2 Assets: 最大化 / 还原
        private const string GlyphMaximize = "\uE922";
        private const string GlyphRestore = "\uE923";

        private const double DefaultCaptionHeight = 32;
        private const double DefaultResizeBorder = 6;

        private Button? _minButton;
        private Button? _maxButton;
        private Button? _closeButton;

        static JuiWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiWindow),
                new FrameworkPropertyMetadata(typeof(JuiWindow)));
        }

        public JuiWindow()
        {
            // 窗口自身永远透明, 颜色 / 圆角全交给模板 RootBorder 画
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = DefaultCaptionHeight,
                ResizeBorderThickness = new Thickness(DefaultResizeBorder),
                CornerRadius = new CornerRadius(0),      // 圆角由 RootBorder 负责
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            StateChanged += OnStateChanged;
            ApplyChromeForMode(TitleBarMode);
        }

        // ================= 模式依赖属性 =================

        public static readonly DependencyProperty TitleBarModeProperty =
            DependencyProperty.Register(
                nameof(TitleBarMode),
                typeof(JuiTitleBarMode),
                typeof(JuiWindow),
                new FrameworkPropertyMetadata(
                    JuiTitleBarMode.Normal,
                    OnTitleBarModeChanged));

        /// <summary>窗口外观模式: 常规 / 无标题栏 / 侵入式 / 完全无边框。</summary>
        public JuiTitleBarMode TitleBarMode
        {
            get => (JuiTitleBarMode)GetValue(TitleBarModeProperty);
            set => SetValue(TitleBarModeProperty, value);
        }

        private static void OnTitleBarModeChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JuiWindow w)
            {
                w.ApplyChromeForMode((JuiTitleBarMode)e.NewValue);
                w.UpdateStateForMode();
            }
        }

        // ================= 圆角 =================

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(JuiWindow),
                new FrameworkPropertyMetadata(new CornerRadius(0), OnCornerRadiusChanged));

        /// <summary>窗口圆角(由 RootBorder 绘制)。最大化时自动归零。</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        private static void OnCornerRadiusChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JuiWindow w) w.UpdateEffectiveCornerRadius();
        }

        // 模板用的"当前实际圆角": 最大化归零, 其余等于 CornerRadius
        private static readonly DependencyPropertyKey EffectiveCornerRadiusKey =
            DependencyProperty.RegisterReadOnly(
                nameof(EffectiveCornerRadius),
                typeof(CornerRadius),
                typeof(JuiWindow),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public static readonly DependencyProperty EffectiveCornerRadiusProperty =
            EffectiveCornerRadiusKey.DependencyProperty;

        /// <summary>模板绑定用: 最大化时为 0, 其余等于 CornerRadius。</summary>
        public CornerRadius EffectiveCornerRadius
        {
            get => (CornerRadius)GetValue(EffectiveCornerRadiusProperty);
            private set => SetValue(EffectiveCornerRadiusKey, value);
        }

        private void UpdateEffectiveCornerRadius()
        {
            EffectiveCornerRadius = WindowState == WindowState.Maximized
                ? new CornerRadius(0)
                : CornerRadius;
        }

        // ================= 直接切换模式的方法 =================

        /// <summary>切换到指定外观模式。</summary>
        public void SetTitleBarMode(JuiTitleBarMode mode) => TitleBarMode = mode;

        /// <summary>常规模式: 标题栏 + 标题文字 + 按钮 + 边框, 可拖边改尺寸。</summary>
        public void UseNormalMode() => TitleBarMode = JuiTitleBarMode.Normal;

        /// <summary>无标题栏: 隐藏标题栏与按钮, 边框仍可拖动改尺寸。</summary>
        public void UseNoTitleBarMode() => TitleBarMode = JuiTitleBarMode.NoTitleBar;

        /// <summary>侵入式: 标题栏透明、内容铺满、保留按钮, 可拖边改尺寸。</summary>
        public void UseImmersiveMode() => TitleBarMode = JuiTitleBarMode.Immersive;

        /// <summary>完全无边框: 无标题、无按钮、无边框、无背景, 不可拖边改尺寸。</summary>
        public void UseBorderlessMode() => TitleBarMode = JuiTitleBarMode.Borderless;

        // ================= Chrome 适配 =================

        /// <summary>
        /// 按模式调整 WindowChrome:
        /// Borderless 模式禁止边缘拖动改尺寸, 也没有标题栏拖拽区;
        /// NoTitleBar 没有标题拖拽区, 但保留边框拖动改尺寸;
        /// Immersive / Normal 保留标题拖拽区 + 边框拖动改尺寸。
        /// </summary>
        private void ApplyChromeForMode(JuiTitleBarMode mode)
        {
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome is null) return;

            switch (mode)
            {
                case JuiTitleBarMode.Borderless:
                    chrome.CaptionHeight = 0;
                    chrome.ResizeBorderThickness = new Thickness(0);
                    break;

                case JuiTitleBarMode.NoTitleBar:
                    chrome.CaptionHeight = 0;
                    chrome.ResizeBorderThickness = new Thickness(DefaultResizeBorder);
                    break;

                case JuiTitleBarMode.Immersive:
                case JuiTitleBarMode.Normal:
                default:
                    chrome.CaptionHeight = DefaultCaptionHeight;
                    chrome.ResizeBorderThickness = new Thickness(DefaultResizeBorder);
                    break;
            }
        }

        /// <summary>模式切换后刷新边框补偿、圆角、按钮图标等状态。</summary>
        private void UpdateStateForMode()
        {
            OnStateChanged(this, EventArgs.Empty);
        }

        // ================= 状态变化 =================

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (TitleBarMode == JuiTitleBarMode.Borderless ||
                TitleBarMode == JuiTitleBarMode.NoTitleBar)
            {
                BorderThickness = new Thickness(0);
            }
            else
            {
                // 最大化时补偿系统溢出, 避免内容超出工作区
                BorderThickness = WindowState == WindowState.Maximized
                    ? new Thickness(7)
                    : new Thickness(0);
            }

            UpdateEffectiveCornerRadius();
            UpdateMaxButtonGlyph();
        }

        // ================= 模板与按钮 =================

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            DetachButtonHandlers();

            _minButton = GetTemplateChild(PartMinButton) as Button;
            _maxButton = GetTemplateChild(PartMaxButton) as Button;
            _closeButton = GetTemplateChild(PartCloseButton) as Button;

            if (_minButton is not null) _minButton.Click += OnMinButtonClick;
            if (_maxButton is not null) _maxButton.Click += OnMaxButtonClick;
            if (_closeButton is not null) _closeButton.Click += OnCloseButtonClick;

            UpdateEffectiveCornerRadius();
            UpdateMaxButtonGlyph();
        }

        private void DetachButtonHandlers()
        {
            if (_minButton is not null) _minButton.Click -= OnMinButtonClick;
            if (_maxButton is not null) _maxButton.Click -= OnMaxButtonClick;
            if (_closeButton is not null) _closeButton.Click -= OnCloseButtonClick;
        }

        private void OnMinButtonClick(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void OnMaxButtonClick(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
            => Close();

        private void UpdateMaxButtonGlyph()
        {
            if (_maxButton is null) return;

            bool maximized = WindowState == WindowState.Maximized;
            _maxButton.Content = maximized ? GlyphRestore : GlyphMaximize;
            _maxButton.ToolTip = maximized ? "还原" : "最大化";
        }
    }
}
