using JUI.Configuration;
using JUI.Models;
using System;
using System.Windows;

namespace JUI.Theming
{
    /// <summary>
    /// JUI 总入口 + 主题管理。
    /// 用户在 App 的 OnStartup 里调用一次 Install() 即可:
    /// 注入全部样式 + 应用主题。之后可用 Apply / Toggle 切换主题。
    /// </summary>
    public static class ThemeManager
    {
        private static ResourceDictionary _styles;   // 全部控件样式(注入一次)
        private static ResourceDictionary _colors;   // 当前颜色主题(可替换)
        private static bool _installed;

        /// <summary>当前主题。</summary>
        public static JuiTheme Current { get; private set; } = JuiTheme.Light;

        /// <summary>主题切换事件。</summary>
        public static event Action<JuiTheme> ThemeChanged;

        // ============ 接入 ============

        /// <summary>
        /// 一行接入 JUI: 注入全部样式并应用主题。在 App 的 OnStartup 里调用一次。
        /// </summary>
        /// <param name="theme">可选: 指定初始主题; 不传则用上次保存的设置。</param>
        public static void Install(JuiTheme? theme = null)
        {
            var app = Application.Current
                ?? throw new InvalidOperationException("请在 App 启动后(OnStartup)调用 Install。");

            if (!_installed)
            {
                _installed = true;

                // 注入样式聚合入口(以后加样式只改 Jui.Styles.xaml, 这里不用动)
                _styles = new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/JUI;component/Themes/Styles.xaml",
                        UriKind.Absolute)
                };
                app.Resources.MergedDictionaries.Add(_styles);
            }

            // 指定了主题 → 应用并持久化; 否则用已保存的设置(不重复落盘)
            if (theme.HasValue)
                Apply(theme.Value);
            else
                Apply(SettingsStore.Current.Theme, save: false);
        }

        /// <summary>兼容旧调用: 等价于 Install()(用已保存的主题)。</summary>
        public static void Initialize() => Install();

        // ============ 主题切换 ============

        /// <summary>切换并应用颜色主题。默认会持久化。</summary>
        public static void Apply(JuiTheme theme, bool save = true)
        {
            var app = Application.Current;
            if (app == null) return;

            var dict = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/JUI;component/Themes/{theme}.xaml",
                    UriKind.Absolute)
            };

            // 移除旧颜色, 新颜色插到最前面, 保证样式里的 DynamicResource 能解析到
            if (_colors != null)
                app.Resources.MergedDictionaries.Remove(_colors);
            app.Resources.MergedDictionaries.Insert(0, dict);
            _colors = dict;

            Current = theme;

            if (save)
            {
                SettingsStore.Current.Theme = theme;
                SettingsStore.Save();
            }

            ThemeChanged?.Invoke(theme);
        }

        /// <summary>在浅色/深色之间一键切换(自动保存)。</summary>
        public static void Toggle()
            => Apply(Current == JuiTheme.Light ? JuiTheme.Dark : JuiTheme.Light);
    }
}
