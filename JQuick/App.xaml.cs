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
            // 若配置为常驻, 球初始就藏起来(PanelWindow.OnLoadedInit 会进入常驻)
            if (ConfigStore.Current.Pinned)
                ball.Hide();
            else
                ball.Show();

            MainWindow = ball;         // 主窗口设为悬浮球

            // ★ 先创建协调中枢, 再注入面板, 最后给托盘
            _app = new AppController(ball, panel);
            panel.AttachController(_app);
            _tray = new TrayIconManager(_app);   // 托盘把自己注册为通知实现
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ConfigStore.Save();   // 退出兜底保存
            base.OnExit(e);
        }
    }



 }
