using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace JQuick
{
    /// <summary>
    /// 图片剪贴板的存储层: 管理本地缓存目录、导入(复制/下载)、顺序持久化。
    /// 缓存目录: %AppData%\JQuick\clipboard\
    /// 顺序文件: %AppData%\JQuick\clipboard\order.json (存文件名数组)
    /// </summary>
    public class ImageClipboardStore
    {
        private static readonly string[] ImageExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public string CacheDir { get; }
        private string OrderFile => Path.Combine(CacheDir, "order.json");



        public ImageClipboardStore(string? cacheDir = null)
        {
            CacheDir = cacheDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JQuick", "clipboard");
            Directory.CreateDirectory(CacheDir);
        }

        // ============ 启动加载 ============

        /// <summary>
        /// 扫描缓存目录并按持久化顺序返回文件路径列表。
        /// 顺序文件里有的排前面; 目录里多出来的(外部放入的)追加末尾;
        /// 顺序文件里指向的已删除文件被剔除。
        /// </summary>
        public List<string> LoadOrdered()
        {
            // 目录里真实存在的图片文件名集合
            var actual = Directory.EnumerateFiles(CacheDir)
                .Where(IsImageFile)
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

            // 2) 目录里有但顺序文件没记录的(外部新增), 追加到末尾
            foreach (var name in actual)
            {
                if (seen.Add(name))
                    ordered.Add(name);
            }

            return ordered.Select(n => Path.Combine(CacheDir, n)).ToList();
        }

        // ============ 导入 ============

        /// <summary>
        /// 把一个本地文件复制进缓存目录, 返回新路径; 失败返回 null。
        /// </summary>
        public string? ImportLocalFile(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath) || !IsImageFile(sourcePath))
                    return null;

                string ext = Path.GetExtension(sourcePath);
                string dest = NewUniquePath(ext);
                File.Copy(sourcePath, dest, overwrite: false);
                return dest;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 下载一张网络图片进缓存目录, 返回新路径; 失败返回 null。
        /// </summary>
        public async Task<string?> ImportUrlAsync(string url)
        {
            try
            {
                using var resp = await Http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;

                string ext = GuessExtension(url, resp.Content.Headers.ContentType?.MediaType);
                string dest = NewUniquePath(ext);

                await using var fs = File.Create(dest);
                await resp.Content.CopyToAsync(fs);
                return dest;
            }
            catch
            {
                return null;
            }
        }

        // ============ 删除 ============

        /// <summary>从缓存目录删除文件(物理删除)。</summary>
        public void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { /* 占用/权限等忽略 */ }
        }

        // ============ 顺序持久化 ============

        /// <summary>把当前顺序(文件名数组)写盘。</summary>
        public void SaveOrder(IEnumerable<string> orderedPaths)
        {
            try
            {
                var names = orderedPaths
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
                {
                    var json = File.ReadAllText(OrderFile);
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new();
                }
            }
            catch { }
            return new();
        }

        // ============ 工具 ============

        private string NewUniquePath(string ext)
        {
            if (string.IsNullOrEmpty(ext)) ext = ".img";
            string name = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
            return Path.Combine(CacheDir, name);
        }

        private static bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ImageExtensions.Contains(ext);
        }

        private static string GuessExtension(string url, string? mediaType)
        {
            // 先看 URL 自带扩展名
            string ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            if (ImageExtensions.Contains(ext)) return ext;

            // 再看 Content-Type
            return mediaType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }
    }
}
