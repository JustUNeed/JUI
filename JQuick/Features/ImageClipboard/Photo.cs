using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JQuick
{
    public class Photo : INotifyPropertyChanged
    {
        public string Path { get; set; } = "";

        /// <summary>是否处于下载中(占位)状态。UI 可据此显示加载提示。</summary>
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnChanged(nameof(IsLoading)); }
        }

        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            private set { _thumbnail = value; OnChanged(nameof(Thumbnail)); }
        }

        private bool _loadStarted;

        /// <summary>触发一次后台解码缩略图, 重复调用无副作用。下载中(无文件)时不解码。</summary>
        public void EnsureThumbnail()
        {
            if (_loadStarted) return;
            if (IsLoading || string.IsNullOrEmpty(Path)) return;   // 占位阶段先不解码
            _loadStarted = true;
            DecodeAsync();
        }

        /// <summary>下载完成后调用: 设置真实路径并解码缩略图、解除占位状态。</summary>
        public void OnDownloaded(string path)
        {
            Path = path;
            IsLoading = false;
            _loadStarted = true;
            DecodeAsync();
        }

        private void DecodeAsync()
        {
            string path = Path;
            Task.Run(() =>
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path);
                    bmp.DecodePixelWidth = 80;          // 降低解码成本
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bmp.EndInit();
                    bmp.Freeze();                        // 关键: 冻结后才能跨线程

                    Application.Current?.Dispatcher.Invoke(() => Thumbnail = bmp);
                }
                catch { /* 加载失败保持 null / 占位符 */ }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
