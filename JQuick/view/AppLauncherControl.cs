using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace JQuick
{
    /// <summary>
    /// 快捷方式启动器(JQuick 二次开发, 基于 JuiGrid)。
    /// 只接受本地文件拖入(文件夹交给 FolderBox), 在 ShortcutDir 创建 .lnk, 点击即启动。
    /// 体验: 叉叉删除。图标进视口后台懒加载, 先显示占位, 不阻塞启动。
    /// </summary>
    public class AppLauncherControl : JuiGrid
    {
        static AppLauncherControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AppLauncherControl),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));

            // 捕获模板里所有名为 "PART_DeleteButton" 的按钮点击
            EventManager.RegisterClassHandler(
                typeof(AppLauncherControl),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(OnAnyButtonClick));
        }

        private ShortcutStore? _store;
        private readonly ObservableCollection<ShortcutItem> _items = new();

        /// <summary>快捷方式目录; 不设则用 %AppData%\JQuick\launcher。需在加载前设置。</summary>
        public string? ShortcutDir { get; set; }

        public AppLauncherControl()
        {
            Loaded += OnFirstLoaded;

            // 容器进入视口才加载图标(虚拟化时回调), 懒加载, 先显示占位符
            ItemPreparing = item =>
            {
                if (item is ShortcutItem s) s.EnsureIconLoaded();
            };
        }

        private static void OnAnyButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe &&
                fe.Name == "PART_DeleteButton" &&
                sender is AppLauncherControl control)
            {
                if (control.GetItemFromElement(fe) is ShortcutItem item)
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

            ExternalDropHandler = HandleExternalDrop;
            ContentChanged = () => _store?.SaveOrder(_items.Select(s => s.LnkPath));  // ← 新增: 排序/增删后保存
            LeftClick = item => _store?.Launch(((ShortcutItem)item).LnkPath);

            ItemsSource = _items;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            List<string> lnks = await Task.Run(() =>
            {
                _store = new ShortcutStore(ShortcutDir);
                return _store.LoadShortcuts();
            });

            if (lnks.Count == 0) return;

            await Dispatcher.InvokeAsync(() =>
            {
                BulkAdd(lnks.Select(l => new ShortcutItem(l)));
            }, DispatcherPriority.Background);
        }

        private void BulkAdd(IEnumerable<ShortcutItem> items)
        {
            using (BulkUpdate())
            {
                foreach (var s in items)
                    _items.Add(s);
            }
        }

        /// <summary>重新扫描目录刷新列表(异步, 不阻塞 UI)。</summary>
        public async Task RefreshAsync()
        {
            if (_store == null) return;

            List<string> lnks = await Task.Run(() => _store.LoadShortcuts());

            _items.Clear();
            if (lnks.Count == 0) return;

            BulkAdd(lnks.Select(l => new ShortcutItem(l)));
        }

        private IEnumerable<object>? HandleExternalDrop(IDataObject data)
        {
            if (_store == null) return null;
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;   // 只允许本地文件

            var result = new List<object>();
            foreach (var src in (string[])data.GetData(DataFormats.FileDrop))
            {
                if (!File.Exists(src)) continue;   // 只收文件, 文件夹交给 FolderBox
                var lnk = _store.CreateShortcut(src);
                if (lnk != null) result.Add(new ShortcutItem(lnk));
            }
            return result.Count > 0 ? result : null;
        }

        public void Remove(ShortcutItem item)
        {
            if (item == null || _store == null) return;
            RemoveItem(item);
            _store.Delete(item.LnkPath);
        }
    }
}
