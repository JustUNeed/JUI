using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace JQuick
{
    /// <summary>
    /// 文件夹收纳控件(基于 JuiGrid)。
    /// 拖入文件夹 → 添加一项; 拖文件到某项上 → 移动文件进该文件夹; 点击项 → 打开文件夹。
    /// 只显示文件夹名称(无图标、无文件数)。收纳的文件夹路径列表持久化到 folders.json。
    /// 优化点: 启动读取配置放后台线程, 文件夹有效性校验也在后台, 数据一次性批量灌入,
    ///         避免启动白屏卡顿。文件移动也放后台, 避免大文件/多文件时卡 UI。
    /// </summary>
    public class FolderBoxControl : JuiGrid
    {
        static FolderBoxControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FolderBoxControl),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));

            // 捕获模板里所有名为 "PART_DeleteButton" 的按钮点击
            EventManager.RegisterClassHandler(
                typeof(FolderBoxControl),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnAnyButtonClick));
        }

        private FolderBoxStore? _store;
        private readonly ObservableCollection<FolderItem> _items = new();

        /// <summary>列表配置(folders.json)所在目录; 不设则用 %AppData%\JQuick\folderbox。</summary>
        public string? ConfigDir { get; set; }

        public FolderBoxControl()
        {
            AllowDropOnItem = true;
            Loaded += OnFirstLoaded;
        }

        private static void OnAnyButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe &&
                fe.Name == "PART_DeleteButton" &&
                sender is FolderBoxControl control)
            {
                if (control.GetItemFromElement(fe) is FolderItem item)
                {
                    control.Remove(item);
                    e.Handled = true;
                }
            }
        }

        private bool _inited;
        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            if (_inited) return;
            _inited = true;

            // 行为契约同步接好, 不耗时
            ExternalDropHandler = HandleExternalDrop;   // 拖到空白 → 添加文件夹项
            ItemDropped = HandleItemDropped;            // 拖到某项上 → 移动文件进文件夹
            ContentChanged = () => _store?.Save(_items.Select(f => f.Path));
            LeftClick = item => OpenFolder(((FolderItem)item).Path);

            ItemsSource = _items;

            // 关键: 读取配置 + 校验文件夹是否存在都放后台, 不阻塞首帧
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // 1) 后台: 构造 store(创建目录)、读 folders.json、过滤掉已不存在的文件夹
            List<string> valid = await Task.Run(() =>
            {
                _store = new FolderBoxStore(ConfigDir);
                return _store.Load().Where(Directory.Exists).ToList();
            });

            if (valid.Count == 0) return;

            // 2) 回 UI 线程, 低优先级批量灌入(首帧先画出来), 批量期间抑制逐项高度重算
            await Dispatcher.InvokeAsync(() =>
            {
                using (BulkUpdate())
                {
                    foreach (var path in valid)
                        _items.Add(new FolderItem(path));
                }
            }, DispatcherPriority.Background);
        }

        // 拖到空白/间隙: 只接受文件夹, 每个文件夹添加成一项
        private IEnumerable<object>? HandleExternalDrop(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;

            var result = new List<object>();
            foreach (var path in (string[])data.GetData(DataFormats.FileDrop))
            {
                if (!Directory.Exists(path)) continue;             // 只收文件夹
                if (_items.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                    continue;                                       // 去重
                result.Add(new FolderItem(path));
            }
            return result.Count > 0 ? result : null;
        }

        // 拖到某一项主体上: 把拖来的文件移动进该文件夹
        private void HandleItemDropped(object? draggedData, IDataObject? rawData, object target)
        {
            if (target is not FolderItem folder) return;
            if (!Directory.Exists(folder.Path)) return;
            if (rawData == null || !rawData.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])rawData.GetData(DataFormats.FileDrop);

            // 文件移动放后台, 避免大文件/多文件时卡 UI
            _ = Task.Run(() =>
            {
                foreach (var src in paths)
                    MoveInto(src, folder.Path);
            });
        }

        private static bool MoveInto(string src, string destDir)
        {
            try
            {
                if (File.Exists(src))
                {
                    string dest = MakeUnique(Path.Combine(destDir, Path.GetFileName(src)));
                    File.Move(src, dest);
                    return true;
                }
                if (Directory.Exists(src))
                {
                    string dest = MakeUniqueDir(Path.Combine(
                        destDir, Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar))));
                    Directory.Move(src, dest);
                    return true;
                }
            }
            catch { /* 跨盘/占用/权限等忽略 */ }
            return false;
        }

        private static string MakeUnique(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path)!;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;
            string p;
            do { p = Path.Combine(dir, $"{name} ({i++}){ext}"); } while (File.Exists(p));
            return p;
        }

        private static string MakeUniqueDir(string path)
        {
            if (!Directory.Exists(path)) return path;
            int i = 1;
            string p;
            do { p = $"{path} ({i++})"; } while (Directory.Exists(p));
            return p;
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        /// <summary>从列表移除一项(不删除磁盘上的真实文件夹, 只是不再收纳)。</summary>
        public void Remove(FolderItem item)
        {
            if (item != null) RemoveItem(item);   // 触发 ContentChanged → 保存
        }
    }
}
