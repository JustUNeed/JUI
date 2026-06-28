using JUI.Controls;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace JQuick
{
    public partial class PanelWindow : JuiWindow
    {
        private const double LeaveMargin = 24;

        private DateTime _guardUntil = DateTime.MinValue;
        private const int GuardMs = 0;

        private bool _shown;
        private bool _dragActive;
        private readonly DispatcherTimer _leaveTimer;

        public FloatingBallWindow? Ball { get; set; }

        private Rect _ballRect;

        // 常驻模式
        public bool Pinned { get; private set; }





        private bool _barDragging;
        private Point _barDragStartCursor;
        private double _barDragStartLeft, _barDragStartTop;

        public AppController? Controller { get; set; }





        public PanelWindow()
        {
            InitializeComponent();

            // 从配置恢复尺寸
            var cfg = ConfigStore.Current;
            Width = cfg.PanelWidth;
            Height = cfg.PanelHeight;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -10000;
            Top = -10000;
            Opacity = 0;

            _leaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _leaveTimer.Tick += OnLeaveTick;

            Drop += (_, _) => _dragActive = false;
            PreviewDrop += (_, _) => _dragActive = false;

            // 尺寸变化时记录(用户缩放面板后保存)
            SizeChanged += (_, _) =>
            {
                if (ActualWidth > 1 && ActualHeight > 1)
                {
                    ConfigStore.Current.PanelWidth = ActualWidth;
                    ConfigStore.Current.PanelHeight = ActualHeight;
                }
            };

            // 常驻模式下用户拖动面板时记录位置
            LocationChanged += (_, _) =>
            {
                if (Pinned && _shown)
                {
                    ConfigStore.Current.PinnedLeft = Left;
                    ConfigStore.Current.PinnedTop = Top;
                }
            };

            Loaded += OnLoadedInit;


            // Ctrl+V 粘贴
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnLoadedInit(object sender, RoutedEventArgs e)
        {
            Opacity = 0;
            Hide();

            if (ConfigStore.Current.Pinned)
            {
                var cfg = ConfigStore.Current;

                // 校验常驻位置是否在任一屏幕的可见范围内
                bool posValid =
                    !double.IsNaN(cfg.PinnedLeft) && !double.IsNaN(cfg.PinnedTop) &&
                    IsOnScreen(cfg.PinnedLeft, cfg.PinnedTop, Width, Height);

                if (!posValid)
                {
                    // 位置无效 -> 用屏幕中心兜底
                    var area = SystemParameters.WorkArea;
                    cfg.PinnedLeft = area.Left + (area.Width - Width) / 2;
                    cfg.PinnedTop = area.Top + (area.Height - Height) / 2;
                }

                PinToggle.IsChecked = true;
                EnterPinned(restorePosition: true);
            }
        }


        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 只有常驻模式才允许用这一行拖动面板
            if (!Pinned) return;
            if (e.ButtonState != MouseButtonState.Pressed) return;

            _barDragging = true;
            _barDragStartCursor = GetCursorScreenPosition();
            _barDragStartLeft = Left;
            _barDragStartTop = Top;
            DragBar.CaptureMouse();
            e.Handled = true;
        }

        private void DragBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_barDragging) return;
            var cur = GetCursorScreenPosition();
            Left = _barDragStartLeft + (cur.X - _barDragStartCursor.X);
            Top = _barDragStartTop + (cur.Y - _barDragStartCursor.Y);
        }

        private void DragBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_barDragging) return;
            _barDragging = false;
            DragBar.ReleaseMouseCapture();

            // 常驻位置实时持久化(LocationChanged 也会记, 这里兜底保存落盘)
            ConfigStore.Current.PinnedLeft = Left;
            ConfigStore.Current.PinnedTop = Top;
            ConfigStore.Save();
        }


        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                PasteFromClipboard();
                e.Handled = true;

            }
        }


        /// <summary>
        /// Ctrl+V 粘贴: 把剪贴板内容包成 DataObject, 复用各控件的 ExternalDropHandler 解析,
        /// 再用 JuiGrid/JuiList 公开的 AddItem 插入。不修改任何控件。
        /// 优先级: 文件 -> 文件面板; 图片 -> 图片剪贴板; 文本 -> 文本列表。
        /// </summary>
        private void PasteFromClipboard()
        {
            try
            {
                // 1) 文件 / 文件夹
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();

                    var imagePaths = new System.Collections.Specialized.StringCollection();  // 图片文件
                    var otherFiles = new System.Collections.Specialized.StringCollection();  // 非图片文件
                    var folders = new System.Collections.Specialized.StringCollection();  // 文件夹

                    foreach (string? p in files)
                    {
                        if (string.IsNullOrEmpty(p)) continue;

                        if (System.IO.Directory.Exists(p))
                            folders.Add(p);
                        else if (IsImageFile(p))
                            imagePaths.Add(p);          // 图片优先 -> 图片剪贴板
                        else
                            otherFiles.Add(p);          // 其余文件 -> 文件快捷面板
                    }

                    // 图片文件 -> 图片剪贴板
                    if (imagePaths.Count > 0)
                    {
                        var data = new DataObject();
                        data.SetFileDropList(imagePaths);
                        FeedToControl(ClipboardGrid, data);
                    }

                    // 非图片文件 -> 文件快捷面板
                    if (otherFiles.Count > 0)
                    {
                        var data = new DataObject();
                        data.SetFileDropList(otherFiles);
                        FeedToControl(Launcher, data);
                    }

                    // 文件夹 -> FolderBox
                    if (folders.Count > 0)
                    {
                        var data = new DataObject();
                        data.SetFileDropList(folders);
                        FeedToControl(FolderBox, data);
                    }

                    // 只要剪贴板里有文件/文件夹, 就到此为止, 不再走位图/文本
                    if (imagePaths.Count > 0 || otherFiles.Count > 0 || folders.Count > 0)
                        return;
                }

                // 2) 位图图片(截图等无文件路径的)
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    string? savedPath = SaveBitmapToTemp(img);
                    if (savedPath != null)
                    {
                        var paths = new System.Collections.Specialized.StringCollection { savedPath };
                        var data = new DataObject();
                        data.SetFileDropList(paths);
                        FeedToControl(ClipboardGrid, data);
                    }
                    return;
                }

                // 3) 文本兜底
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var data = new DataObject();
                        data.SetText(text);
                        data.SetData(DataFormats.UnicodeText, text);
                        FeedToControl(TextClip, data);
                    }
                }
            }
            catch { /* 剪贴板访问偶发异常忽略 */ }
        }

        /// <summary>是否为图片文件(按扩展名判断)。</summary>
        private static bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp"
                      or ".gif" or ".webp" or ".tif" or ".tiff" or ".ico";
        }

        /// <summary>把一个 DataObject 交给某 JuiGrid 控件的 ExternalDropHandler 解析并插入。</summary>
        private static void FeedToControl(JuiGrid grid, IDataObject data)
        {
            var handler = grid.ExternalDropHandler;
            if (handler == null) return;

            var items = handler(data);
            if (items == null) return;

            foreach (var item in items)
                if (item != null) grid.AddItem(item);
        }

        /// <summary>JuiList 版(文本控件继承 JuiList, 不是 JuiGrid)。</summary>
        private static void FeedToControl(JuiList list, IDataObject data)
        {
            var handler = list.ExternalDropHandler;
            if (handler == null) return;

            var items = handler(data);
            if (items == null) return;

            foreach (var item in items)
                if (item != null) list.AddItem(item);
        }

        // 判断窗口矩形是否至少部分落在工作区内
        private bool IsOnScreen(double left, double top, double w, double h)
        {
            var area = SystemParameters.WorkArea;
            var rect = new Rect(left, top, w, h);
            return area.IntersectsWith(rect);
        }


        private string? SaveBitmapToTemp(System.Windows.Media.Imaging.BitmapSource? bmp)
        {
            if (bmp == null) return null;
            try
            {
                string dir = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "JQuick_paste");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, $"paste_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                using var fs = new System.IO.FileStream(file, System.IO.FileMode.Create);
                encoder.Save(fs);
                return file;
            }
            catch { return null; }
        }


        // ===================== 常驻开关 =====================

        private void PinToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (PinToggle.IsChecked == true)
                EnterPinned();
            else
                ExitPinned();
        }

        private void EnterPinned(bool restorePosition = false)
        {
            Pinned = true;
            ConfigStore.Current.Pinned = true;

            _leaveTimer.Stop();            // 常驻不自动收起
            _shown = true;

            // 隐藏悬浮球
            Ball?.HideBall();

            if (restorePosition)
            {
                // 启动恢复: 用记录的常驻位置
                var cfg = ConfigStore.Current;
                if (!double.IsNaN(cfg.PinnedLeft) && !double.IsNaN(cfg.PinnedTop))
                {
                    Left = cfg.PinnedLeft;
                    Top = cfg.PinnedTop;
                }
                else
                {
                    var area = SystemParameters.WorkArea;
                    Left = area.Left + (area.Width - Width) / 2;
                    Top = area.Top + (area.Height - Height) / 2;
                }
            }
            else
            {
                // 运行时点开关: 保持当前位置, 仅记录
                ConfigStore.Current.PinnedLeft = Left;
                ConfigStore.Current.PinnedTop = Top;
            }

            Opacity = 1;
            Show();
            Activate();
            Focus();
            ConfigStore.Save();
        }

        private void ExitPinned()
        {
            Pinned = false;
            ConfigStore.Current.Pinned = false;
            ConfigStore.Save();

            // 退出常驻: 面板暂时还留着, 重新启用"鼠标离开才消失"的逻辑
            _shown = true;
            _guardUntil = DateTime.Now.AddMilliseconds(GuardMs);
            _leaveTimer.Start();

            // 悬浮球重新画出来
            Ball?.ShowBall();


        
        }

        // ===================== 弹出在悬浮球旁 =====================

        public void ShowBeside(Rect ballRect, bool dragActive)
        {
            if (Pinned) return;   // 常驻态不走这套

            _ballRect = ballRect;
            _dragActive = dragActive;
            _guardUntil = DateTime.Now.AddMilliseconds(GuardMs);

            var (left, top) = ComputePosition(ballRect);
            Left = left;
            Top = top;

            Opacity = 1;
            Show();
            Activate();
            Focus();
            _shown = true;

            _leaveTimer.Start();
        }

        public void RepositionBesideIfShown(Rect ballRect)
        {
            if (Pinned || !_shown) return;
            _ballRect = ballRect;
            var (left, top) = ComputePosition(ballRect);
            Left = left;
            Top = top;
        }

        private (double left, double top) ComputePosition(Rect ball)
        {
            var area = SystemParameters.WorkArea;
            double w = Width, h = Height;
            const double gap = 0;   // ← 悬浮球与面板的间隔在这里

            double spaceRight = area.Right - ball.Right;
            double spaceLeft = ball.Left - area.Left;
            double spaceBottom = area.Bottom - ball.Bottom;
            double spaceTop = ball.Top - area.Top;

            double left, top;

            if (spaceRight >= w + gap)
                left = ball.Right + gap;
            else if (spaceLeft >= w + gap)
                left = ball.Left - gap - w;
            else
                left = ball.Left + ball.Width / 2 - w / 2;

            if (spaceRight >= w + gap || spaceLeft >= w + gap)
                top = ball.Top;
            else
            {
                if (spaceBottom >= h + gap)
                    top = ball.Bottom + gap;
                else if (spaceTop >= h + gap)
                    top = ball.Top - gap - h;
                else
                    top = ball.Top;
            }

            return (left, top);
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            _dragActive = true;
            _guardUntil = DateTime.Now.AddMilliseconds(GuardMs);
        }

        // ===================== 移出判定 =====================

        private void OnLeaveTick(object? sender, EventArgs e)
        {
            if (Pinned) return;            // 常驻不收
            if (!_shown) return;
            if (DateTime.Now < _guardUntil) return;

            var p = GetCursorScreenPosition();
            double margin = _dragActive ? LeaveMargin * 2 : LeaveMargin;

            var panelRect = new Rect(Left, Top, ActualWidth, ActualHeight);
            panelRect.Inflate(margin, margin);

            var ballRect = _ballRect;
            ballRect.Inflate(margin, margin);

            if (panelRect.Contains(p) || ballRect.Contains(p))
                return;

            HidePanel();
        }

        private void HidePanel()
        {
            _leaveTimer.Stop();
            _shown = false;
            _dragActive = false;

            Opacity = 0;
            Hide();

            Ball?.ShowBall();
        }

        // ===================== Win32 全局光标 =====================

        private Point GetCursorScreenPosition()
        {
            GetCursorPos(out POINT pt);
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformFromDevice;
                return m.Transform(new Point(pt.X, pt.Y));
            }
            return new Point(pt.X, pt.Y);
        }

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // ===================== 原有删除逻辑 =====================

        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardGrid.GetItemFromElement(sender as DependencyObject) is Photo p)
                ClipboardGrid.Remove(p);
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderBox.GetItemFromElement(sender as DependencyObject) is FolderItem f)
                FolderBox.Remove(f);
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 常驻态下点关闭: 不退出程序, 改为退出常驻 + 回到悬浮球
            if (Pinned)
            {
                e.Cancel = true;          // 取消关闭
                PinToggle.IsChecked = false;  // 触发 ExitPinned -> 显示悬浮球
                return;
            }
            base.OnClosing(e);
        }


        private SettingsWindow? _settingsWindow;

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Controller?.OpenSettings();
        }




        // 注入后调用一次, 订阅设置窗口开关事件
        public void AttachController(AppController app)
        {
            Controller = app;
            app.SettingsOpened += () => _leaveTimer.Stop();   // 设置打开, 面板别收起
            app.SettingsClosed += () =>
            {
                if (!Pinned)
                {
                    _guardUntil = DateTime.Now.AddMilliseconds(GuardMs);
                    _leaveTimer.Start();
                }
            };
        }





    }
}
