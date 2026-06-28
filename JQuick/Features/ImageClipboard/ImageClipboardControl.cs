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
    /// 图片剪贴板控件(JQuick 二次开发, 基于 JuiGrid)。
    /// 内置: 缓存目录管理、拖入(本地复制/网络下载)、顺序持久化、启动恢复。
    /// 体验: 左键打开预览(系统默认看图程序), 叉叉删除, 网络图片先占位下载完才出现。
    /// 优化点: 启动扫盘放后台线程, 数据一次性批量灌入, 灌入期间抑制逐项高度重算。
    /// </summary>
    public class ImageClipboardControl : JuiGrid
    {
        static ImageClipboardControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageClipboardControl),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));

            // 捕获模板里所有名为 "PART_DeleteButton" 的按钮点击
            EventManager.RegisterClassHandler(
                typeof(ImageClipboardControl),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnAnyButtonClick));
        }

        private ImageClipboardStore? _store;
        private readonly ObservableCollection<Photo> _items = new();

        /// <summary>缓存目录; 不设则用 %AppData%\JQuick\clipboard。需在加载前设置。</summary>
        public string? CacheDir { get; set; }

        /// <summary>左键点击一张图片时回调; 不设则使用默认行为(系统默认程序打开原图)。</summary>
        public Action<Photo>? ImageClicked { get; set; }



        public ImageClipboardControl()
        {
            Loaded += OnFirstLoaded;

            // 容器进入视口才解码缩略图(虚拟化时回调), 懒加载
            ItemPreparing = item =>
            {
                if (item is Photo p) p.EnsureThumbnail();
            };
        }





        private static void OnAnyButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe &&
                fe.Name == "PART_DeleteButton" &&
                sender is ImageClipboardControl control)
            {
                if (control.GetItemFromElement(fe) is Photo item)
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

            FilePathSelector = item => (item as Photo)?.Path;
            ContentChanged = () => _store?.SaveOrder(_items.Where(p => !p.IsLoading).Select(p => p.Path));
            ExternalDropHandler = HandleExternalDrop;
            LeftClick = item =>
            {
                var p = (Photo)item;
                if (p.IsLoading) return;                 // 下载中不响应
                if (ImageClicked != null) ImageClicked(p);
                else OpenPreview(p.Path);                 // 默认行为: 系统看图程序打开原图
            };

            ItemsSource = _items;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            List<string> paths = await Task.Run(() =>
            {
                _store = new ImageClipboardStore(CacheDir);
                return _store.LoadOrdered();
            });

            if (paths.Count == 0) return;

            await Dispatcher.InvokeAsync(() =>
            {
                BulkAdd(paths.Select(p => new Photo { Path = p }));
            }, DispatcherPriority.Background);
        }

        private void BulkAdd(IEnumerable<Photo> photos)
        {
            using (BulkUpdate())
            {
                foreach (var p in photos)
                    _items.Add(p);
            }
        }
        private IEnumerable<object>? HandleExternalDrop(IDataObject data)
        {
            if (_store == null) return null;

            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var result = new List<object>();
                foreach (var src in (string[])data.GetData(DataFormats.FileDrop))
                {
                    var dest = _store.ImportLocalFile(src);
                    if (dest != null) result.Add(new Photo { Path = dest });
                }
                return result.Count > 0 ? result : null;
            }

            if (data.GetDataPresent(DataFormats.UnicodeText))
            {
                var url = ((string)data.GetData(DataFormats.UnicodeText)).Trim();
                if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // 返回占位项, 由控件按落点插入(位置正确); 后台启动下载, 完成后填充
                    var placeholder = new Photo { IsLoading = true };
                    DownloadAsync(url, placeholder);
                    return new object[] { placeholder };
                }
            }
            return null;
        }

        private async void DownloadAsync(string url, Photo placeholder)
        {
            if (_store == null) return;

            var dest = await _store.ImportUrlAsync(url);

            if (dest == null)
            {
                // 下载失败/非图片: 移除占位项(用 RemoveItem 以触发持久化)
                RemoveItem(placeholder);
                return;
            }

            // 下载成功: 在占位项原位填充真实图片, 解码缩略图
            placeholder.OnDownloaded(dest);
            _store.SaveOrder(_items.Where(p => !p.IsLoading).Select(p => p.Path));
        }

        /// <summary>用系统默认程序打开原图。</summary>
        private static void OpenPreview(string path)
        {
            try
            {
                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        /// <summary>删除一张图(从列表移除 + 物理删除缓存文件)。</summary>
        public void Remove(Photo item)
        {
            if (item == null || _store == null) return;
            RemoveItem(item);
            if (!string.IsNullOrEmpty(item.Path))
                _store.DeleteFile(item.Path);
        }
    }
}
