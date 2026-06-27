using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JQuick
{
    /// <summary>文件夹收纳列表的持久化(folders.json, 存文件夹绝对路径数组)。</summary>
    public class FolderBoxStore
    {
        public string ConfigDir { get; }
        private string ConfigFile => Path.Combine(ConfigDir, "folders.json");

        public FolderBoxStore(string? dir = null)
        {
            ConfigDir = dir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JQuick", "folderbox");
            Directory.CreateDirectory(ConfigDir);
        }

        public List<string> Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                    return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ConfigFile)) ?? new();
            }
            catch { }
            return new();
        }

        public void Save(IEnumerable<string> paths)
        {
            try
            {
                File.WriteAllText(ConfigFile,
                    JsonSerializer.Serialize(paths.ToList(), new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
