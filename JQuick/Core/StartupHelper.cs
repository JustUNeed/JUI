using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace JQuick
{
    /// <summary>开机自启动(当前用户注册表 Run 项)与配置文件夹相关工具。</summary>
    public static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "JQuick";   // 注册表项名, 唯一即可

        /// <summary>是否已设置开机自启动。</summary>
        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(AppName) is string;
            }
            catch { return false; }
        }

        /// <summary>开启 / 关闭开机自启动。</summary>
        public static void SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                                ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (key == null) return;

                if (enabled)
                {
                    string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(exe))
                        key.SetValue(AppName, $"\"{exe}\"");
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                        key.DeleteValue(AppName, false);
                }
            }
            catch { /* 失败忽略, 或可弹提示 */ }
        }

        /// <summary>打开配置所在文件夹(并选中配置文件)。</summary>
        public static void OpenConfigFolder()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JQuick");
                Directory.CreateDirectory(dir);

                string file = Path.Combine(dir, "config.json");
                if (File.Exists(file))
                    Process.Start("explorer.exe", $"/select,\"{file}\"");
                else
                    Process.Start("explorer.exe", $"\"{dir}\"");
            }
            catch { }
        }
    }
}
