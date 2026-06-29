using JUI.Theming;
using System.Configuration;
using System.Data;
using System.Windows;

namespace JQuick
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private AppController? _app;
        private TrayIconManager? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.Initialize();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;   // 退出由 AppController.Exit 控制

            var panel = new PanelWindow();
            panel.Show();              // 先 Show 注册拖放目标(随后 Loaded 里会自己藏起来)

            var ball = new FloatingBallWindow(panel);

            // ★ 先创建协调中枢, 再注入面板, 最后给托盘
            _app = new AppController(ball, panel);
            panel.AttachController(_app);
            _tray = new TrayIconManager(_app);   // 托盘把自己注册为通知实现

            MainWindow = ball;         // 主窗口设为悬浮球

            // ★ 每次启动强制非常驻:永远只显示悬浮球, 不读取/恢复任何常驻状态。
            //   常驻模式只在本次运行期间有效, 不持久化, 从根本上避免启动恢复常驻态时的崩溃。
            ball.ShowBall();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ConfigStore.Save();   // 退出兜底保存(球位置/尺寸/颜色等, 不含常驻态)
            base.OnExit(e);
        }
    }
}
