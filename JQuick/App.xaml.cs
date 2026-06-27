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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.Initialize();

            var panel = new PanelWindow();
            panel.Show();              // 先 Show 注册拖放目标(随后 Loaded 里会自己藏起来)

            var ball = new FloatingBallWindow(panel);
            // 若配置为常驻, 球初始就藏起来(PanelWindow.OnLoadedInit 会进入常驻)
            if (ConfigStore.Current.Pinned)
                ball.Hide();
            else
                ball.Show();

            MainWindow = ball;         // 主窗口设为悬浮球, 关它即退出

         
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ConfigStore.Save();   // 退出兜底保存
            base.OnExit(e);
        }
    }



 }
