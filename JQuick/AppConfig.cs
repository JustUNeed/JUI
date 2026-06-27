using System;
using System.IO;
using System.Text.Json;

namespace JQuick
{
    /// <summary>应用持久化配置。后期加配置项直接在这里加属性即可。</summary>
    public class AppConfig
    {
        // 悬浮球位置
        public double BallLeft { get; set; } = double.NaN;
        public double BallTop { get; set; } = double.NaN;

        // 面板尺寸
        public double PanelWidth { get; set; } = 400;
        public double PanelHeight { get; set; } = 500;

        // 常驻模式
        public bool Pinned { get; set; } = false;
        // 常驻模式下面板的位置
        public double PinnedLeft { get; set; } = double.NaN;
        public double PinnedTop { get; set; } = double.NaN;
    }

    /// <summary>配置读写: %AppData%\JQuick\config.json</summary>
    public static class ConfigStore
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JQuick");
        private static readonly string FilePath = Path.Combine(Dir, "config.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true
        };

        private static AppConfig? _current;

        /// <summary>全局当前配置(首次访问自动加载)。</summary>
        public static AppConfig Current => _current ??= Load();

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { /* 读失败用默认 */ }
            return new AppConfig();
        }

        public static void Save()
        {
            if (_current == null) return;
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(_current, Options);
                File.WriteAllText(FilePath, json);
            }
            catch { /* 写失败忽略 */ }
        }
    }
}
