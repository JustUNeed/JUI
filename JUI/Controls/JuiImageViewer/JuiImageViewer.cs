using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;

namespace JUI.Controls
{
    /// <summary>
    /// JUI 大图预览控件(lookless)。给定图片路径即用 SkiaSharp 解码全分辨率原图显示,
    /// 内置平移、平滑缩放、翻页。可独立放任意容器, 也可配合图片网格点击弹窗使用。
    /// </summary>
    [TemplatePart(Name = PartContainer, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartImage, Type = typeof(Image))]
    public class JuiImageViewer : Control
    {
        private const string PartContainer = "PART_Container";
        private const string PartImage = "PART_Image";

        private static readonly string[] SupportedExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".wbmp" };

        private FrameworkElement? _container;
        private Image? _img;
        private MatrixTransform _mt = new(Matrix.Identity);

        private bool _dragging;
        private Point _lastPos;

        private readonly DispatcherTimer _qualityTimer;

        private double _targetScale = 1.0;
        private double _currentScale = 1.0;
        private Point _zoomCenter;
        private bool _animating;
        private const double ZoomSmooth = 0.18;
        private const double MinScale = 0.05;
        private const double MaxScale = 50.0;

        private List<string> _playlist = new();
        private int _index = -1;

        static JuiImageViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiImageViewer), new FrameworkPropertyMetadata(typeof(JuiImageViewer)));
        }

        public JuiImageViewer()
        {
            _qualityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _qualityTimer.Tick += (s, e) =>
            {
                _qualityTimer.Stop();
                SetHighQuality();
            };

            Focusable = true;
            Unloaded += (s, e) => StopZoomAnimation();
        }

        // ===== 解码扩展钩子 =====
        /// <summary>自定义解码委托。返回非 null 优先使用, 返回 null 回退内置 SkiaSharp 解码。</summary>
        public Func<string, BitmapSource?>? DecodeOverride { get; set; }

        /// <summary>扩展名是否在内置支持范围内。</summary>
        public static bool IsSupportedExtension(string path) =>
            SupportedExts.Contains(Path.GetExtension(path).ToLowerInvariant());

        // ===== 依赖属性 =====
        /// <summary>图片路径。设置后自动解码显示(并以同目录构建翻页列表)。</summary>
        public static readonly DependencyProperty ImagePathProperty =
            DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(JuiImageViewer),
                new FrameworkPropertyMetadata(null, OnImagePathChanged));

        public string? ImagePath
        {
            get => (string?)GetValue(ImagePathProperty);
            set => SetValue(ImagePathProperty, value);
        }

        /// <summary>是否用同目录其它图片自动构建翻页列表, 默认 true。</summary>
        public static readonly DependencyProperty AutoPlaylistProperty =
            DependencyProperty.Register(nameof(AutoPlaylist), typeof(bool), typeof(JuiImageViewer),
                new FrameworkPropertyMetadata(true));

        public bool AutoPlaylist
        {
            get => (bool)GetValue(AutoPlaylistProperty);
            set => SetValue(AutoPlaylistProperty, value);
        }

        private static void OnImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (JuiImageViewer)d;
            if (e.NewValue is string p && !string.IsNullOrEmpty(p))
                v.OpenImage(p);
        }

        // ===== 对外事件 =====
        /// <summary>当前显示图片变化, 参数为路径。</summary>
        public event Action<string>? CurrentChanged;
        /// <summary>解码失败, 参数为路径。</summary>
        public event Action<string>? LoadFailed;

        // ===== 模板 =====
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            DetachContainer();

            _container = GetTemplateChild(PartContainer) as FrameworkElement;
            _img = GetTemplateChild(PartImage) as Image;

            if (_img != null)
            {
                _mt = new MatrixTransform(Matrix.Identity);
                _img.RenderTransform = _mt;
            }

            if (_container != null)
            {
                _container.MouseWheel += OnWheel;
                _container.MouseLeftButtonDown += OnMouseDown;
                _container.MouseLeftButtonUp += OnMouseUp;
                _container.MouseMove += OnMouseMove;
            }

            if (_index >= 0) LoadCurrent();
        }

        private void DetachContainer()
        {
            if (_container == null) return;
            _container.MouseWheel -= OnWheel;
            _container.MouseLeftButtonDown -= OnMouseDown;
            _container.MouseLeftButtonUp -= OnMouseUp;
            _container.MouseMove -= OnMouseMove;
        }

        // ===== 公开接口 =====
        public void Show(string path) => OpenImage(path);

        public void Show(string path, IEnumerable<string> playlist)
        {
            _playlist = playlist?.ToList() ?? new List<string> { path };
            _index = _playlist.FindIndex(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
            if (_index < 0) { _playlist = new List<string> { path }; _index = 0; }
            LoadCurrent();
        }

        /// <summary>适应窗口(重置变换)。</summary>
        public void FitToWindow()
        {
            StopZoomAnimation();
            _mt.Matrix = Matrix.Identity;
            SetHighQuality();
        }

        /// <summary>1:1 原始像素显示。</summary>
        public void ActualSize()
        {
            if (_img?.Source is not BitmapSource bs || _container == null) return;
            StopZoomAnimation();

            double fit = GetUniformScale(bs);
            if (fit <= 0) return;
            double k = 1.0 / fit;
            var m = Matrix.Identity;
            m.ScaleAt(k, k, _container.ActualWidth / 2, _container.ActualHeight / 2);
            _mt.Matrix = m;
            SetHighQuality();
        }

        public void ShowPrev()
        {
            if (_playlist.Count == 0) return;
            _index = (_index - 1 + _playlist.Count) % _playlist.Count;
            LoadCurrent();
        }

        public void ShowNext()
        {
            if (_playlist.Count == 0) return;
            _index = (_index + 1) % _playlist.Count;
            LoadCurrent();
        }

        // ===== 打开 / 播放列表 =====
        private void OpenImage(string path)
        {
            if (AutoPlaylist) BuildPlaylist(path);
            else { _playlist = new List<string> { path }; _index = 0; }
            LoadCurrent();
        }

        private void BuildPlaylist(string path)
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                { _playlist = new List<string> { path }; _index = 0; return; }

                _playlist = Directory.EnumerateFiles(dir)
                    .Where(IsSupportedExtension)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _index = _playlist.FindIndex(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
                if (_index < 0) { _playlist = new List<string> { path }; _index = 0; }
            }
            catch { _playlist = new List<string> { path }; _index = 0; }
        }

        private void LoadCurrent()
        {
            if (_img == null) return;
            if (_index < 0 || _index >= _playlist.Count) return;
            string path = _playlist[_index];

            try
            {
                var bmp = DecodeOverride?.Invoke(path) ?? DecodeFull(path);
                if (bmp == null)
                {
                    LoadFailed?.Invoke(path);
                    return;
                }

                _img.Source = bmp;
                FitToWindow();
                CurrentChanged?.Invoke(path);
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(path);
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // ===== SkiaSharp 全分辨率解码 =====
        private static BitmapSource? DecodeFull(string path)
        {
            try
            {
                using var codec = SKCodec.Create(path);
                if (codec == null) return null;

                var info = new SKImageInfo(
                    codec.Info.Width, codec.Info.Height,
                    SKColorType.Bgra8888, SKAlphaType.Premul);
                if (info.Width <= 0 || info.Height <= 0) return null;

                using var skBitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, skBitmap.GetPixels());
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                    return null;

                var wb = new WriteableBitmap(info.Width, info.Height, 96, 96,
                    PixelFormats.Pbgra32, null);
                wb.Lock();
                try
                {
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (void*)skBitmap.GetPixels(),
                            (void*)wb.BackBuffer,
                            (long)wb.BackBufferStride * wb.PixelHeight,
                            (long)info.RowBytes * info.Height);
                    }
                    wb.AddDirtyRect(new Int32Rect(0, 0, info.Width, info.Height));
                }
                finally { wb.Unlock(); }

                wb.Freeze();
                return wb;
            }
            catch { return null; }
        }

        // ===== 键盘 =====
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.Left: case Key.PageUp: ShowPrev(); e.Handled = true; break;
                case Key.Right: case Key.PageDown: case Key.Space: ShowNext(); e.Handled = true; break;
            }
        }

        // ===== 缩放 / 平移 / 质量 =====
        private double GetUniformScale(BitmapSource bs)
        {
            if (_container == null) return 0;
            double cw = _container.ActualWidth, ch = _container.ActualHeight;
            if (cw <= 0 || ch <= 0) return 0;
            return Math.Min(cw / bs.PixelWidth, ch / bs.PixelHeight);
        }

        private Rect GetImageBounds()
        {
            if (_img?.Source == null || _container == null) return Rect.Empty;
            double w = _img.RenderSize.Width, h = _img.RenderSize.Height;
            if (w <= 0 || h <= 0) return Rect.Empty;
            var t = _img.TransformToVisual(_container);
            return new Rect(t.Transform(new Point(0, 0)), t.Transform(new Point(w, h)));
        }

        private bool IsInsideImage(Point p)
        {
            Rect r = GetImageBounds();
            return r != Rect.Empty && r.Contains(p);
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if (_img?.Source == null || _container == null) return;
            UseLowQualityWhileInteracting();

            double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            double predicted = _mt.Matrix.M11 * _targetScale / _currentScale * factor;
            if (predicted < MinScale || predicted > MaxScale) return;

            Point posInContainer = e.GetPosition(_container);
            _zoomCenter = IsInsideImage(posInContainer)
                ? e.GetPosition(_img)
                : new Point(_img.RenderSize.Width / 2, _img.RenderSize.Height / 2);

            _targetScale *= factor;
            StartZoomAnimation();
        }

        private void StartZoomAnimation()
        {
            if (_animating) return;
            _animating = true;
            CompositionTarget.Rendering += OnZoomTick;
        }

        private void OnZoomTick(object? sender, EventArgs e)
        {
            double newScale = _currentScale + (_targetScale - _currentScale) * ZoomSmooth;
            if (Math.Abs(_targetScale - newScale) < 0.001) newScale = _targetScale;

            double step = newScale / _currentScale;
            _currentScale = newScale;

            var m = _mt.Matrix;
            m.ScaleAt(step, step, _zoomCenter.X, _zoomCenter.Y);
            _mt.Matrix = m;

            if (_currentScale == _targetScale) StopZoomAnimation();
        }

        private void StopZoomAnimation()
        {
            if (_animating)
            {
                CompositionTarget.Rendering -= OnZoomTick;
                _animating = false;
            }
            _currentScale = 1.0;
            _targetScale = 1.0;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_img?.Source == null || _container == null) return;
            Focus();

            if (e.ClickCount == 2) { FitToWindow(); return; }
            if (!IsInsideImage(e.GetPosition(_container))) return;

            StopZoomAnimation();
            _dragging = true;
            _lastPos = e.GetPosition(_container);
            _container.CaptureMouse();
            _container.Cursor = Cursors.SizeAll;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            _container?.ReleaseMouseCapture();
            if (_container != null) _container.Cursor = Cursors.Arrow;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _container == null) return;
            UseLowQualityWhileInteracting();

            Point p = e.GetPosition(_container);
            var m = _mt.Matrix;
            m.Translate(p.X - _lastPos.X, p.Y - _lastPos.Y);
            _mt.Matrix = m;
            _lastPos = p;
        }

        private void UseLowQualityWhileInteracting()
        {
            if (_img == null) return;
            RenderOptions.SetBitmapScalingMode(_img, BitmapScalingMode.LowQuality);
            _qualityTimer.Stop();
            _qualityTimer.Start();
        }

        private void SetHighQuality()
        {
            if (_img != null)
                RenderOptions.SetBitmapScalingMode(_img, BitmapScalingMode.HighQuality);
        }



        /// <summary>异步打开: 后台线程解码, 完成后回 UI 线程显示, 不阻塞调用方。</summary>
        public async Task ShowAsync(string path, ImageSource? placeholder)
        {
            if (AutoPlaylist) BuildPlaylist(path);
            else { _playlist = new List<string> { path }; _index = 0; }

            if (_img != null && placeholder != null)
            {
                _img.Source = placeholder;   // 先显示缩略图, 用户立刻看到内容
                FitToWindow();
            }
            await LoadCurrentAsync(placeholder);         // 后台解原图, 完成后替换
        }

        private async Task LoadCurrentAsync(ImageSource? placeholder)
        {
            string path = _playlist[_index];

            if (_img != null && placeholder != null)
            {
                _img.Source = placeholder;
                FitToWindow();              // 只在占位时摆一次位
            }

            var bmp = await Task.Run(() => DecodeOverride?.Invoke(path) ?? DecodeFull(path));
            if (_img == null || bmp == null) { LoadFailed?.Invoke(path); return; }

            _img.Source = bmp;             // 只换 Source, 不动变换矩阵
            SetHighQuality();              // 切回高质量, 但不 FitToWindow
            CurrentChanged?.Invoke(path);
        }

    }
}
