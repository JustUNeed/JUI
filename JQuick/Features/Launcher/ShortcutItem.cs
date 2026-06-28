using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JQuick
{
    /// <summary>
    /// 启动器里的一个快捷方式项, 缩略图为目标文件的真实缩略图/系统图标。
    /// 后台异步加载: Icon 初始为 null(占位), 加载完成后通过 INotifyPropertyChanged 通知 UI。
    /// 加载由控件在容器进视口时调用 EnsureIconLoaded() 触发, 重复调用无副作用。
    /// 取图用 Windows Shell 的 IShellItemImageFactory: 系统资源管理器怎么显示就取什么
    /// (图片/视频取真实缩略图, 其它取高清类型图标, 支持文件夹), 优于 ExtractAssociatedIcon。
    /// </summary>
    public class ShortcutItem : INotifyPropertyChanged
    {
        /// <summary>取图目标尺寸(像素)。可按格子大小调大, 如 128/256。</summary>
        private const int IconSize = 128;

        public string LnkPath { get; }

        public ShortcutItem(string lnkPath)
        {
            LnkPath = lnkPath;
        }

        public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(LnkPath);

        // ---------------- 图标 ----------------

        private ImageSource? _icon;
        /// <summary>缩略图; 初始为 null(占位), 进视口后后台加载完成再填充。</summary>
        public ImageSource? Icon
        {
            get => _icon;
            private set
            {
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }

        /// <summary>主动触发图标后台加载(供控件在容器进视口时调用), 重复调用无副作用。</summary>
        public void EnsureIconLoaded() => EnsureIcon();

        private bool _loadStarted;
        private void EnsureIcon()
        {
            if (_loadStarted) return;
            _loadStarted = true;

            string lnkPath = LnkPath;
            Task.Run(() =>
            {
                try
                {
                    // 1) 解析 .lnk 得到真实目标; 解析不到则退回 .lnk 本身
                    string? target = ResolveLnkTarget(lnkPath);

                    // 文件夹要用 Directory.Exists 判定, 文件用 File.Exists
                    string iconSource =
                        !string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target))
                            ? target!
                            : lnkPath;

                    // 2) 取系统缩略图/图标
                    var src = ShellThumbnail.GetThumbnail(iconSource, IconSize, IconSize);
                    if (src == null) return;

                    Application.Current?.Dispatcher.Invoke(() => Icon = src);
                }
                catch { /* 失败保持 null / 占位 */ }
            });
        }

        /// <summary>用 WScript.Shell COM 解析 .lnk 的目标路径; 失败返回 null。</summary>
        private static string? ResolveLnkTarget(string lnkPath)
        {
            try
            {
                if (!lnkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    return lnkPath;

                Type? t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;

                dynamic shell = Activator.CreateInstance(t)!;
                dynamic sc = shell.CreateShortcut(lnkPath);
                string target = sc.TargetPath;
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch { return null; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 用 IShellItemImageFactory 取任意路径(文件/文件夹)的系统缩略图或图标。
    /// 返回的是已 Freeze 的 BitmapSource, 可跨线程交给 UI。
    /// </summary>
    internal static class ShellThumbnail
    {
        public static BitmapSource? GetThumbnail(string path, int width, int height)
        {
            IShellItemImageFactory? factory = null;
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                if (hr != 0 || factory == null) return null;

                var size = new SIZE { cx = width, cy = height };

                // RESIZETOFIT: 等比缩放到目标尺寸; THUMBNAILONLY 不加 → 没缩略图时回退到图标
                hr = factory.GetImage(size, SIIGBF.RESIZETOFIT, out hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero) return null;

                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();   // 冻结后才能跨线程交给 UI
                return src;
            }
            catch { return null; }
            finally
            {
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        // ---------------- P/Invoke ----------------

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [Flags]
        private enum SIIGBF
        {
            RESIZETOFIT = 0x00,
            BIGGERSIZEOK = 0x01,
            MEMORYONLY = 0x02,
            ICONONLY = 0x04,
            THUMBNAILONLY = 0x08,
            INCACHEONLY = 0x10,
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }
    }
}
