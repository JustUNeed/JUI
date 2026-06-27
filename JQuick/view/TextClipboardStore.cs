using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JQuick
{
    /// <summary>
    /// 文本剪贴板存储层: 把所有文本项(内容 + 顺序)整体持久化到 texts.json。
    /// 顺序即数组顺序, 内容即文本本身, 一个文件搞定。
    /// </summary>
    public class TextClipboardStore
    {
        public string ConfigDir { get; }
        private string DataFile => Path.Combine(ConfigDir, "texts.json");

        public TextClipboardStore(string? dir = null)
        {
            ConfigDir = dir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JQuick", "textclipboard");
            Directory.CreateDirectory(ConfigDir);
        }

        /// <summary>读取所有文本项(按持久化顺序)。</summary>
        public List<string> Load()
        {
            try
            {
                if (File.Exists(DataFile))
                    return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(DataFile)) ?? new();
            }
            catch { }
            return new();
        }

        /// <summary>整体写盘(内容 + 顺序)。</summary>
        public void Save(IEnumerable<string> texts)
        {
            try
            {
                var list = texts.Where(t => t != null).ToList();
                File.WriteAllText(DataFile,
                    JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* 写失败不影响运行 */ }
        }
    }
}
