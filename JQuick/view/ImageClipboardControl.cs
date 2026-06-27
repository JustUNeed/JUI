using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JQuick
{
    /// <summary>
    /// 图片剪贴板控件(JQuick 二次开发, 基于 JuiGrid)。
    /// 内置: 缓存目录管理、拖入(本地复制/网络下载)、顺序持久化、启动恢复。
    /// 优化点: 启动扫盘放后台线程, 数据一次性批量灌入, 灌入期间抑制逐项高度重算,
    ///         避免启动白屏卡顿。
    /// </summary>
    public class ImageClipboardControl : JuiGrid
    {
        static ImageClipboardControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageClipboardControl),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));
        }

        private ImageClipboardStore? _store;
        private readonly ObservableCollection<Photo> _items = new();

        /// <summary>缓存目录; 不设则用 %AppData%\JQuick\clipboard。需在加载前设置。</summary>
        public string? CacheDir { get; set; }

        /// <summary>左键点击一张图片时回调(参数: 图片项)。</summary>
        public Action<Photo>? ImageClicked { get; set; }

        public ImageClipboardControl()
        {
            Loaded += OnFirstLoaded;

            // 容器进入视口才解码缩略图(JuiGrid 虚拟化时回调), 这步本身是懒加载, 保持不变
            ItemPreparing = item =>
            {
                if (item is Photo p) p.EnsureThumbnail();
            };
        }

        private bool _inited;
        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            if (_inited) return;
            _inited = true;

            // 行为契约可以同步接好, 不耗时
            FilePathSelector = item => (item as Photo)?.Path;
            ContentChanged = () => _store?.SaveOrder(_items.Select(p => p.Path));
            ExternalDropHandler = HandleExternalDrop;
            LeftClick = item => ImageClicked?.Invoke((Photo)item);

            ItemsSource = _items;

            // 关键: 扫盘 + 初始化放后台, 不阻塞首帧渲染
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // 1) 后台线程构造 store(会创建目录)并扫描已保存顺序
            List<string> paths = await Task.Run(() =>
            {
                _store = new ImageClipboardStore(CacheDir);
                return _store.LoadOrdered();
            });

            if (paths.Count == 0) return;

            // 2) 回 UI 线程, 在低优先级上批量灌入(让首帧先画出来)
            await Dispatcher.InvokeAsync(() =>
            {
                BulkAdd(paths.Select(p => new Photo { Path = p }));
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// 批量添加: 灌入期间抑制 CollectionChanged 风暴带来的逐项高度重算,
        /// 全部加完只重算一次。
        /// </summary>
        private void BulkAdd(IEnumerable<Photo> photos)
        {
            using (BulkUpdate())
            {
                foreach (var p in photos)
                    _items.Add(p);
            }
            // 注: ObservableCollection 每次 Add 仍会发通知, 但虚拟化下只有视口内
            //     的容器会真正实例化并触发 ItemPreparing(缩略图懒加载), 不会一次性解码所有图。
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
                    DownloadAsync(url);
                    return null;
                }
            }
            return null;
        }

        private async void DownloadAsync(string url)
        {
            if (_store == null) return;
            var dest = await _store.ImportUrlAsync(url);
            if (dest == null) return;
            _items.Add(new Photo { Path = dest });
            _store.SaveOrder(_items.Select(p => p.Path));
        }

        /// <summary>删除一张图(从列表移除 + 物理删除缓存文件)。</summary>
        public void Remove(Photo item)
        {
            if (item == null || _store == null) return;
            RemoveItem(item);
            _store.DeleteFile(item.Path);
        }
    }
}
