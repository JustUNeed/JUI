using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace JQuick
{
    /// <summary>快捷方式存储层: 在指定目录创建/扫描 .lnk, 启动目标。</summary>
    public class ShortcutStore
    {
        public string ShortcutDir { get; }

        public ShortcutStore(string? dir = null)
        {
            ShortcutDir = dir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JQuick", "launcher");
            Directory.CreateDirectory(ShortcutDir);
        }

        public List<string> LoadShortcuts()
            => Directory.EnumerateFiles(ShortcutDir, "*.lnk").OrderBy(p => p).ToList();

        /// <summary>为本地文件创建 .lnk, 返回路径; 失败返回 null。通过 WScript.Shell COM, 无需第三方库。</summary>
        public string? CreateShortcut(string targetPath)
        {
            try
            {
                if (!File.Exists(targetPath) && !Directory.Exists(targetPath)) return null;

                string name = Path.GetFileNameWithoutExtension(targetPath);
                string lnkPath = UniqueLnkPath(name);

                Type? t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                dynamic shell = Activator.CreateInstance(t)!;
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.TargetPath = targetPath;
                sc.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? "";
                sc.Save();
                return lnkPath;
            }
            catch { return null; }
        }

        /// <summary>启动快捷方式(等同双击 .lnk)。</summary>
        public void Launch(string lnkPath)
        {
            try { Process.Start(new ProcessStartInfo(lnkPath) { UseShellExecute = true }); }
            catch { }
        }

        public void Delete(string lnkPath)
        {
            try { if (File.Exists(lnkPath)) File.Delete(lnkPath); } catch { }
        }

        private string UniqueLnkPath(string baseName)
        {
            string path = Path.Combine(ShortcutDir, baseName + ".lnk");
            int i = 1;
            while (File.Exists(path))
                path = Path.Combine(ShortcutDir, $"{baseName} ({i++}).lnk");
            return path;
        }
    }
}
