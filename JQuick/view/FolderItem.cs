using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JQuick
{
    /// <summary>收纳的一个文件夹项。文件数与图标均后台异步加载, 不阻塞 UI。</summary>
    public class FolderItem : INotifyPropertyChanged
    {
        public string Path { get; }
        public string DisplayName { get; }

        public FolderItem(string path)
        {
            Path = path;
            // DirectoryInfo(path).Name 只是字符串解析, 不碰磁盘, 同步无妨
            DisplayName = new DirectoryInfo(path).Name;
            RefreshCountAsync();   // 文件数后台统计
        }

        // ---------------- 文件数 ----------------

        private int _fileCount;
        /// <summary>文件夹内文件数量(用于显示)。后台统计, 完成后通知 UI。</summary>
        public int FileCount
        {
            get => _fileCount;
            private set { _fileCount = value; OnPropertyChanged(nameof(FileCount)); }
        }

        /// <summary>后台重新统计文件数(可从任意线程调用)。</summary>
        public void RefreshCountAsync()
        {
            string path = Path;
            Task.Run(() =>
            {
                int count;
                try
                {
                    count = Directory.Exists(path)
                        ? Directory.EnumerateFiles(path).Count()   // 不一次性建数组
                        : 0;
                }
                catch { count = 0; }

                // 回 UI 线程赋值(属性变更要在 UI 线程触发绑定更新)
                Application.Current?.Dispatcher.Invoke(() => FileCount = count);
            });
        }

        /// <summary>供后台线程调用的安全刷新别名(语义更清晰)。</summary>
        public void Dispatcher_RefreshCount() => RefreshCountAsync();

        // ---------------- 图标 ----------------

        private ImageSource? _icon;
        /// <summary>文件夹图标; 第一次被绑定读取时触发后台加载。</summary>
        public ImageSource? Icon
        {
            get { EnsureIcon(); return _icon; }
            private set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        // FolderItem.cs 里加
        /// <summary>主动触发图标 + 文件数加载(供控件在容器进视口时调用)。</summary>
        public void EnsureLoaded()
        {
            EnsureIcon();        // 触发图标后台加载
            RefreshCountAsync(); // 触发文件数后台统计
        }


        private bool _iconLoadStarted;
        private void EnsureIcon()
        {
            if (_iconLoadStarted) return;
            _iconLoadStarted = true;

            string path = Path;
            Task.Run(() =>
            {
                try
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon == null) return;

                    var src = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();   // 冻结后才能跨线程交给 UI

                    Application.Current?.Dispatcher.Invoke(() => Icon = src);
                }
                catch { /* 失败保持 null / 占位 */ }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
