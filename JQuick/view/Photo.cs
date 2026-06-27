using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;

public class Photo : INotifyPropertyChanged
{
    public string Path { get; set; } = "";

    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    private bool _loadStarted;

    /// 触发一次后台加载,重复调用无副作用
    public void EnsureThumbnail()
    {
        if (_loadStarted) return;
        _loadStarted = true;

        Task.Run(() =>
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(Path);
                bmp.DecodePixelWidth = 240;          // 降低解码成本
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                bmp.Freeze();                        // 关键:冻结后才能跨线程

                // 回主线程赋值,触发 UI 更新
                Application.Current.Dispatcher.Invoke(() => Thumbnail = bmp);
            }
            catch
            {
                // 加载失败保持 null / 占位符
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
