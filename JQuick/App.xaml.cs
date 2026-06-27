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
            try
            {
                ThemeManager.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show("主题加载失败:\n" + ex.ToString());
            }
        }
    }



 }
