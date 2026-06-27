using System;
using System.IO;
using System.Text.Json;

namespace JUI.Configuration
{
    /// <summary>
    /// 负责 JuiSettings 的 JSON 持久化。库内部统一通过这里读写设置,
    /// 用户无需关心文件路径和序列化细节。
    /// </summary>
    public static class SettingsStore
    {
        // 配置文件: %AppData%\JUI\settings.json
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JUI", "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,                 // 输出带缩进, 方便调试查看
            PropertyNameCaseInsensitive = true    // 读取时大小写不敏感, 更宽容
        };

        private static JuiSettings _current;

        /// <summary>
        /// 当前设置(内存中)。首次访问时自动从磁盘加载。
        /// </summary>
        public static JuiSettings Current
        {
            get
            {
                if (_current == null)
                    _current = Load();
                return _current;
            }
        }

        /// <summary>
        /// 从磁盘加载设置; 文件不存在或损坏时返回默认设置。
        /// </summary>
        public static JuiSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var settings = JsonSerializer.Deserialize<JuiSettings>(json, JsonOptions);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // 文件损坏 / 格式错误 / 无权限 等, 一律回退到默认设置, 不让库崩溃
            }
            return new JuiSettings();
        }

        /// <summary>
        /// 将当前内存中的设置写入磁盘。
        /// </summary>
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                var json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // 写失败(磁盘满/无权限等)不影响程序继续运行
            }
        }
    }
}
