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
    /// 拖出格式通过 JuiList.DragDataProvider 填充, JUI 框架不感知"文本"这一业务格式。
    /// </summary>
    public class TextClipboardControl : JuiList
    {
        static TextClipboardControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TextClipboardControl),
                new FrameworkPropertyMetadata(typeof(JuiList)));

            // 捕获模板里所有名为 "PART_DeleteButton" 的按钮点击
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
            if (sender is not TextClipboardControl control ||
                e.OriginalSource is not FrameworkElement fe)
                return;

            if (control.GetItemFromElement(fe) is not ClipTextItem item) return;

            if (fe.Name == "PART_DeleteButton")
            {
                control.Remove(item);
                e.Handled = true;
            }
            else if (fe.Name == "PART_EditButton")
            {
                control.OpenEditor(item);
                e.Handled = true;
            }
        }

        private bool _inited;
        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            if (_inited) return;
            _inited = true;

            // 拖入文本 → 加项
            ExternalDropHandler = HandleExternalDrop;

            // 拖出 → 由本控件决定往 DataObject 放什么(文本格式)
            DragDataProvider = (item, data) =>
            {
                var text = (item as ClipTextItem)?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    data.SetData(DataFormats.Text, text);
                    data.SetData(DataFormats.UnicodeText, text);
                }
            };

            // 排序/增删 → 整体保存(内容 + 顺序)
            ContentChanged = () => _store?.Save(_items.Select(i => i.Text));

            // 左键 → 复制到系统剪贴板
            LeftClick = item => CopyToClipboard((ClipTextItem)item);

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


        /// <summary>打开多行编辑窗口编辑指定项; 关闭即保存(内容有变更才落盘)。</summary>
        private void OpenEditor(ClipTextItem item)
        {
            if (item == null) return;

            var win = new TextEditWindow(item)
            {
                Owner = Window.GetWindow(this),
                // 编辑保存后整体落盘(沿用现有持久化: 按当前顺序保存全部文本)
                Saved = () => _store?.Save(_items.Select(i => i.Text))
            };
            win.ShowDialog();
        }
    }
}
