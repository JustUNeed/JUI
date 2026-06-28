using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows.Controls;

namespace JQuick
{
    /// <summary>系统托盘: 触发 AppController 的应用级操作。</summary>
    public class TrayIconManager : IDisposable
    {
        private readonly TaskbarIcon _tray;
        private readonly AppController _app;
        private MenuItem _toggleBallItem = null!;

        public TrayIconManager(AppController app)
        {
            _app = app;

            _tray = new TaskbarIcon
            {
                ToolTipText = "JQuick",
                IconSource = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/icon/tray.ico", UriKind.Absolute))
            };

            _tray.ContextMenu = BuildMenu();
            _tray.TrayMouseDoubleClick += (_, _) => _app.ShowBall();

            // 把"托盘气泡"注册为应用的通知实现
            _app.Notifier = (title, msg) =>
                _tray.ShowBalloonTip(title, msg, BalloonIcon.Info);
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();

            _toggleBallItem = new MenuItem { Header = "隐藏悬浮球" };
            _toggleBallItem.Click += (_, _) => _app.ToggleBall();

            var settingsItem = new MenuItem { Header = "设置" };
            settingsItem.Click += (_, _) => _app.OpenSettings();

            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (_, _) => _app.Exit();

            menu.Items.Add(_toggleBallItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            menu.Opened += (_, _) =>
                _toggleBallItem.Header = _app.BallVisible ? "隐藏悬浮球" : "显示悬浮球";

            return menu;
        }

        public void Dispose() => _tray.Dispose();
    }
}
