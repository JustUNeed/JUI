using JUI.Configuration;
using JUI.Models;
using System;
using System.Windows;

namespace JUI.Theming
{
    public static class ThemeManager
    {
        private static ResourceDictionary _currentDict;

        public static JuiTheme Current { get; private set; } = JuiTheme.Light;

        public static event Action<JuiTheme> ThemeChanged;

        /// <summary>
        /// 库初始化: 读取已保存的主题并应用。在 App 启动时调用一次。
        /// </summary>
        public static void Initialize()
        {
            // 从统一的设置存储里读主题
            Apply(SettingsStore.Current.Theme, save: false);
        }

        /// <summary>
        /// 切换并应用主题。默认会持久化。
        /// </summary>
        public static void Apply(JuiTheme theme, bool save = true)
        {
            var app = Application.Current;
            if (app == null) return;

            var uri = new Uri(
                $"pack://application:,,,/JUI;component/Themes/{theme}.xaml",
                UriKind.Absolute);
            var newDict = new ResourceDictionary { Source = uri };

            if (_currentDict != null)
                app.Resources.MergedDictionaries.Remove(_currentDict);
            app.Resources.MergedDictionaries.Add(newDict);
            _currentDict = newDict;

            Current = theme;

            // 写回统一设置存储并落盘
            if (save)
            {
                SettingsStore.Current.Theme = theme;
                SettingsStore.Save();
            }

            ThemeChanged?.Invoke(theme);
        }

        /// <summary>
        /// 在黑白之间一键切换(自动保存)。
        /// </summary>
        public static void Toggle()
        {
            Apply(Current == JuiTheme.Light ? JuiTheme.Dark : JuiTheme.Light);
        }
    }
}
