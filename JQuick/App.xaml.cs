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
            ThemeManager.Initialize();   // 就这一行, 自动读取上次保存的主题
        }
    }



 }
