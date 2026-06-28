using System;
using System.Windows;

namespace JQuick
{
    /// <summary>
    /// 应用协调中枢: 管理窗口生命周期、应用级操作、用户通知。
    /// 各窗口与托盘只依赖本类, 不互相引用。后期加窗口/通知都在这里扩展。
    /// </summary>
    public class AppController
    {
        // ---- 核心常驻窗口 ----
        public FloatingBallWindow Ball { get; }
        public PanelWindow Panel { get; }

        // ---- 按需创建的窗口(单例式, 关了置空) ----
        private SettingsWindow? _settings;

        // ---- 设置窗口开关事件: 面板订阅, 用于暂停/恢复自动收起 ----
        public event Action? SettingsOpened;
        public event Action? SettingsClosed;

        // ---- 通知(托盘气泡 / 后期可换成自定义 Toast 窗口) ----
        public Action<string, string>? Notifier { get; set; }   // (title, message)

        public AppController(FloatingBallWindow ball, PanelWindow panel)
        {
            Ball = ball;
            Panel = panel;
        }

        // ===================== 悬浮球 =====================

        public bool BallVisible => Ball.IsVisible;
        public void ShowBall() => Ball.ShowBall();
        public void HideBall() => Ball.HideBall();
        public void ToggleBall()
        {
            if (BallVisible) HideBall();
            else ShowBall();
        }

        // ===================== 设置窗口 =====================

        public void OpenSettings()
        {
            if (_settings != null)
            {
                _settings.Activate();
                return;
            }

            _settings = new SettingsWindow();   // 应用级窗口, 不挂 Owner
            _settings.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _settings.Closed += (_, _) =>
            {
                _settings = null;
                SettingsClosed?.Invoke();
            };

            SettingsOpened?.Invoke();
            _settings.Show();
            _settings.Activate();
        }

        // ===================== 通知 =====================

        /// <summary>给用户一条简短通知。当前走托盘气泡, 后期可替换实现。</summary>
        public void Notify(string message, string title = "JQuick")
        {
            // 切回 UI 线程, 允许后台线程也能安全调用
            Application.Current.Dispatcher.Invoke(() => Notifier?.Invoke(title, message));
        }

        // ===================== 退出 =====================

        public void Exit()
        {
            ConfigStore.Save();
            Application.Current.Shutdown();
        }

        // ===================== 后期扩展示例 =====================
        // private SomeWindow? _someWindow;
        // public void OpenSomeWindow() { ... 同 OpenSettings 的单例模式 ... }
    }
}
