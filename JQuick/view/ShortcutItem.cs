using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JQuick
{
    /// <summary>启动器里的一个快捷方式项, 缩略图为目标文件图标。</summary>
    public class ShortcutItem : INotifyPropertyChanged
    {
        public string LnkPath { get; }              // ← 用回原来的名字

        public ShortcutItem(string lnkPath)
        {
            LnkPath = lnkPath;
        }

        public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(LnkPath);

        private ImageSource? _icon;
        public ImageSource? Icon
        {
            get { EnsureIcon(); return _icon; }   // 第一次被绑定读取时触发后台加载
            private set
            {
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }

        private bool _loadStarted;

        private void EnsureIcon()
        {
            if (_loadStarted) return;
            _loadStarted = true;

            Task.Run(() =>
            {
                try
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(LnkPath);
                    if (icon == null) return;

                    var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                    src.Freeze();   // 关键:冻结后才能跨线程交给 UI

                    Application.Current.Dispatcher.Invoke(() => Icon = src);
                }
                catch { /* 失败保持 null / 占位 */ }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

}
