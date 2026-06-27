using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace JQuick
{
    /// <summary>
    /// 文件夹收纳控件(基于 JuiGrid)。
    /// 拖入文件夹 → 添加一项; 拖文件到某项上 → 移动文件进该文件夹; 点击项 → 打开文件夹。
    /// 收纳的文件夹路径列表持久化到 folders.json。
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
            AllowDropOnItem = true;   // 关键: 允许"拖到某一项上" → 移动文件进去
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

            _store = new FolderBoxStore(ConfigDir);

            ExternalDropHandler = HandleExternalDrop;   // 拖到空白 → 添加文件夹项
            ItemDropped = HandleItemDropped;    // 拖到某项上 → 移动文件进文件夹
            ContentChanged = () => _store.Save(_items.Select(f => f.Path));
            LeftClick = item => OpenFolder(((FolderItem)item).Path);

            ItemsSource = _items;

            // 启动恢复: 只保留仍存在的文件夹
            foreach (var path in _store.Load())
                if (Directory.Exists(path))
                    _items.Add(new FolderItem(path));
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

            // 本场景是外部文件拖入, rawData 有值
            if (rawData == null || !rawData.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])rawData.GetData(DataFormats.FileDrop);
            int moved = 0;
            foreach (var src in paths)
            {
                if (MoveInto(src, folder.Path)) moved++;
            }

            if (moved > 0)
                folder.RefreshCount();   // 更新该文件夹项的显示(如文件数)
        }

        private static bool MoveInto(string src, string destDir)
        {
            try
            {
                if (File.Exists(src))
                {
                    string dest = Path.Combine(destDir, Path.GetFileName(src));
                    dest = MakeUnique(dest);
                    File.Move(src, dest);
                    return true;
                }
                if (Directory.Exists(src))
                {
                    // 拖进来的是子文件夹: 整体移动
                    string dest = Path.Combine(destDir, Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar)));
                    dest = MakeUniqueDir(dest);
                    Directory.Move(src, dest);
                    return true;
                }
            }
            catch { /* 跨盘/占用/权限等忽略, 也可在此提示 */ }
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
