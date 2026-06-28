using JUI.Controls;
using System.Windows;

namespace JQuick
{
    public partial class SettingsWindow : JuiWindow
    {
        private bool _initializing;

        public SettingsWindow()
        {
            InitializeComponent();

            // 初始化勾选状态(以注册表为准), 避免触发事件
            _initializing = true;
            StartupCheck.IsChecked = StartupHelper.IsEnabled();
            _initializing = false;
        }

        private void StartupCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            StartupHelper.SetEnabled(StartupCheck.IsChecked == true);
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            StartupHelper.OpenConfigFolder();
        }
    }
}
