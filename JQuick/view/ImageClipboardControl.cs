using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JQuick
{
    /// <summary>
    /// 图片剪贴板控件(JQuick 二次开发, 基于 JuiGrid)。
    /// 内置: 缓存目录管理、拖入(本地复制/网络下载)、顺序持久化、启动恢复。
    /// 使用者只需在 XAML 放置并给 ItemTemplate(数据项类型 Photo)。
    /// </summary>
    public class ImageClipboardControl : JuiGrid
    {
        static ImageClipboardControl()
        {
            // 子类复用 JuiGrid 的默认样式
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

            _store = new ImageClipboardStore(CacheDir);

            FilePathSelector = item => (item as Photo)?.Path;
            ContentChanged = () => _store.SaveOrder(_items.Select(p => p.Path));
            ExternalDropHandler = HandleExternalDrop;
            LeftClick = item => ImageClicked?.Invoke((Photo)item);

            ItemsSource = _items;

            foreach (var path in _store.LoadOrdered())
                _items.Add(new Photo { Path = path });
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
