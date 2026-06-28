using JUI.Controls;
using System.Windows;
using System.Windows.Media;

namespace JQuick
{
    public partial class SettingsWindow : JuiWindow
    {
        private bool _initializing;


        /// <summary>由打开方注入, 用于实时调整悬浮球大小。</summary>
        public FloatingBallWindow? Ball { get; set; }


        public SettingsWindow()
        {
            InitializeComponent();

            // 初始化勾选状态(以注册表为准), 避免触发事件
            _initializing = true;
            StartupCheck.IsChecked = StartupHelper.IsEnabled();

            // 初始化色块为当前配置色
            BallColorBtn.SelectedColor = ParseColor(ConfigStore.Current.BallColor, "#0A84FF");
            BallTextColorBtn.SelectedColor = ParseColor(ConfigStore.Current.BallTextColor, "#FFFFFF");

            _initializing = false;


            DataContext = ConfigStore.Current;
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


        private void BallSizeBox_ValueChanged(object sender, double newValue)
        {
            if (_initializing) return;

            // 绑定已把 newValue 写回 ConfigStore.Current.BallSize, 这里只负责"副作用"
            Ball?.SetBallSize(newValue);   // 实时改悬浮球
            ConfigStore.Save();            // 落盘
        }

        private void BallRadiusBox_ValueChanged(object sender, double newValue)
        {
            if (_initializing) return;
            Ball?.SetBallCornerRadius(newValue);
            ConfigStore.Save();
        }

        private void BallFontSizeBox_ValueChanged(object sender, double newValue)
        {
            if (_initializing) return;
            Ball?.SetBallFontSize(newValue);
            ConfigStore.Save();
        }

        private void BallColorBtn_ColorChanged(object sender, Color c)
        {
            if (_initializing) return;
            string hex = BallColorBtn.HexColor;     // "#RRGGBB"
            Ball?.SetBallColor(hex);                 // 实时更新悬浮球
            ConfigStore.Current.BallColor = hex;     // 写入配置
            ConfigStore.Save();                      // 持久化
        }

        private void BallTextColorBtn_ColorChanged(object sender, Color c)
        {
            if (_initializing) return;
            string hex = BallTextColorBtn.HexColor;
            Ball?.SetBallTextColor(hex);
            ConfigStore.Current.BallTextColor = hex;
            ConfigStore.Save();
        }

        private static Color ParseColor(string? hex, string fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                    return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch { }
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }
}
