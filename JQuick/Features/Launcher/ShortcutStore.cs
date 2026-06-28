using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JQuick
{
    /// <summary>
    /// 快捷方式存储层: 在指定目录创建/扫描 .lnk, 启动目标。
    /// 顺序持久化到 order.json(存 .lnk 文件名数组): json 里有的排前面,
    /// 目录里多出来的(外部放入的)追加末尾, json 里指向的已删除文件被剔除。
    /// </summary>
    public class ShortcutStore
    {
        public string ShortcutDir { get; }
        private string OrderFile => Path.Combine(ShortcutDir, "order.json");

        public ShortcutStore(string? dir = null)
        {
            ShortcutDir = dir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JQuick", "launcher");
            Directory.CreateDirectory(ShortcutDir);
        }

        /// <summary>
        /// 按持久化顺序返回 .lnk 完整路径列表。
        /// json 里有且仍存在的排前面; 目录里多出来的追加末尾; json 里已删除的剔除。
        /// </summary>
        public List<string> LoadShortcuts()
        {
            // 目录里真实存在的 .lnk 文件名集合
            var actual = Directory.EnumerateFiles(ShortcutDir, "*.lnk")
                .Select(Path.GetFileName)
                .Where(n => n != null)
                .Select(n => n!)
                .ToList();

            var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);

            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) 先按保存的顺序排, 只保留仍存在的
            foreach (var name in ReadOrder())
            {
                if (actualSet.Contains(name) && seen.Add(name))
                    ordered.Add(name);
            }

            // 2) 目录里有但 json 没记录的(外部新增), 按文件名排序后追加末尾
            foreach (var name in actual.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Add(name))
                    ordered.Add(name);
            }

            return ordered.Select(n => Path.Combine(ShortcutDir, n)).ToList();
        }

        /// <summary>把当前顺序(.lnk 文件名数组)写盘。</summary>
        public void SaveOrder(IEnumerable<string> orderedLnkPaths)
        {
            try
            {
                var names = orderedLnkPaths
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();

                var json = JsonSerializer.Serialize(names,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(OrderFile, json);
            }
            catch { /* 写失败不影响运行 */ }
        }

        private List<string> ReadOrder()
        {
            try
            {
                if (File.Exists(OrderFile))
                    return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(OrderFile)) ?? new();
            }
            catch { }
            return new();
        }

        /// <summary>为本地文件创建 .lnk, 返回路径; 失败返回 null。通过 WScript.Shell COM。</summary>
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
