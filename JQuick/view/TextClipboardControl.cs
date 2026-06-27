using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace JQuick
{
    /// <summary>
    /// 文本剪贴板列表(基于 JuiList)。
    /// 外部拖入文本 → 加项; 项可拖出到外部文本框(微信/浏览器); 左键点击 → 复制到系统剪贴板;
    /// 叉叉删除。内容与顺序整体持久化到 texts.json。
    /// </summary>
    public class TextClipboardControl : JuiList
    {
        static TextClipboardControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TextClipboardControl),
                new FrameworkPropertyMetadata(typeof(JuiList)));

            EventManager.RegisterClassHandler(
                typeof(TextClipboardControl),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnAnyButtonClick));
        }

        private TextClipboardStore? _store;
        private readonly ObservableCollection<ClipTextItem> _items = new();

        /// <summary>配置目录; 不设则用 %AppData%\JQuick\textclipboard。需在加载前设置。</summary>
        public string? ConfigDir { get; set; }

        /// <summary>左键点击复制成功后的回调(可用于提示"已复制")。参数: 文本项。</summary>
        public Action<ClipTextItem>? Copied { get; set; }

        public TextClipboardControl()
        {
            Loaded += OnFirstLoaded;
        }

        private static void OnAnyButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe &&
                fe.Name == "PART_DeleteButton" &&
                sender is TextClipboardControl control)
            {
                if (control.GetItemFromElement(fe) is ClipTextItem item)
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

            ExternalDropHandler = HandleExternalDrop;                 // 拖入文本 → 加项
            DragTextSelector = item => (item as ClipTextItem)?.Text;  // 拖出 → 给文本框
            ContentChanged = () => _store?.Save(_items.Select(i => i.Text));  // 排序/增删 → 保存
            LeftClick = item => CopyToClipboard((ClipTextItem)item);  // 左键 → 复制

            ItemsSource = _items;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            List<string> texts = await Task.Run(() =>
            {
                _store = new TextClipboardStore(ConfigDir);
                return _store.Load();
            });

            if (texts.Count == 0) return;

            await Dispatcher.InvokeAsync(() =>
            {
                using (BulkUpdate())
                {
                    foreach (var t in texts)
                        _items.Add(new ClipTextItem(t));
                }
            }, DispatcherPriority.Background);
        }

        private IEnumerable<object>? HandleExternalDrop(IDataObject data)
        {
            string? text = null;

            if (data.GetDataPresent(DataFormats.UnicodeText))
                text = data.GetData(DataFormats.UnicodeText) as string;
            else if (data.GetDataPresent(DataFormats.Text))
                text = data.GetData(DataFormats.Text) as string;

            if (string.IsNullOrWhiteSpace(text)) return null;   // 只收文本, 非文本忽略

            return new object[] { new ClipTextItem(text) };
        }

        private void CopyToClipboard(ClipTextItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Text)) return;
            try
            {
                Clipboard.SetText(item.Text);
                Copied?.Invoke(item);
            }
            catch { /* 剪贴板被占用等忽略 */ }
        }

        /// <summary>删除一项(从列表移除并持久化)。</summary>
        public void Remove(ClipTextItem item)
        {
            if (item == null) return;
            RemoveItem(item);   // 触发 ContentChanged → 保存
        }
    }
}
