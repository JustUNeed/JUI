using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace JQuick
{
    /// <summary>收纳的一个文件夹项。</summary>
    public class FolderItem : INotifyPropertyChanged
    {
        public string Path { get; }
        public string DisplayName { get; }

        public FolderItem(string path)
        {
            Path = path;
            DisplayName = new DirectoryInfo(path).Name;
            RefreshCount();
        }

        private int _fileCount;
        /// <summary>文件夹内文件数量(用于显示)。</summary>
        public int FileCount
        {
            get => _fileCount;
            private set { _fileCount = value; OnPropertyChanged(nameof(FileCount)); }
        }

        public void RefreshCount()
        {
            try { FileCount = Directory.Exists(Path) ? Directory.GetFiles(Path).Length : 0; }
            catch { FileCount = 0; }
        }

        private BitmapSource? _icon;
        /// <summary>文件夹图标。</summary>
        public BitmapSource? Icon => _icon ??= LoadFolderIcon(Path);

        private static BitmapSource? LoadFolderIcon(string path)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            catch { return null; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
