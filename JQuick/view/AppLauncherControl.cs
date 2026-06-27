using JUI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace JQuick
{
    /// <summary>
    /// 快捷方式启动器(JQuick 二次开发, 基于 JuiGrid)。
    /// 只接受本地文件拖入, 在 ShortcutDir 创建 .lnk, 列表显示目录里所有快捷方式的图标,
    /// 点击即启动。类似开始菜单。
    /// </summary>
    public class AppLauncherControl : JuiGrid
    {
        static AppLauncherControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AppLauncherControl),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));
        }

        private ShortcutStore? _store;
        private readonly ObservableCollection<ShortcutItem> _items = new();

        /// <summary>快捷方式目录; 不设则用 %AppData%\JQuick\launcher。</summary>
        public string? ShortcutDir { get; set; }

        public AppLauncherControl()
        {
            Loaded += OnFirstLoaded;
        }

        private bool _inited;
        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            if (_inited) return;
            _inited = true;

            _store = new ShortcutStore(ShortcutDir);

            ExternalDropHandler = HandleExternalDrop;
            LeftClick = item => _store!.Launch(((ShortcutItem)item).LnkPath);

            ItemsSource = _items;
            Refresh();
        }

        /// <summary>重新扫描目录刷新列表。</summary>
        public void Refresh()
        {
            if (_store == null) return;
            _items.Clear();
            foreach (var lnk in _store.LoadShortcuts())
                _items.Add(new ShortcutItem(lnk));
        }

        private IEnumerable<object>? HandleExternalDrop(IDataObject data)
        {
            if (_store == null) return null;
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;   // 只允许本地文件

            var result = new List<object>();
            foreach (var src in (string[])data.GetData(DataFormats.FileDrop))
            {
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
