using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace JUI.Controls
{
    /// <summary>
    /// JUI 通用网格控件: 多列布局 + 虚拟化 + 拖动排序 + 拖入/拖出 + 移入项(类资源管理器)。
    /// 落点判定: 间隙/空白 = 插入或排序; 项主体上 = 移入该项(需开启 AllowDropOnItem)。
    /// 外部拖入的数据格式不由控件判断, 统一通过 ExternalDropHandler 交给用户解析。
    /// 单选; 左右键点击分别通过 LeftClick / RightClick 暴露(项内控件标记 Handled 即可不触发)。
    /// 高度由控件内部按数据量与宽度自动算成固定值, 给虚拟化提供稳定视口; 使用者只需设 ItemsSource。
    /// </summary>
    public class JuiGrid : ListBox
    {
        private const string JuiItemFormat = "JUI.InternalDragItem";

        private InsertionAdorner? _insertionAdorner;
        private int _lastInsertIndex = -2;   // 缓存上次插入位置, -2 表示无效初值


        private ScrollViewer? _scrollViewer;

        static JuiGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiGrid),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));
        }

        public JuiGrid()
        {
            AllowDrop = true;
            SelectionMode = SelectionMode.Single;   // 永远单选

            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;     // 冒泡: 项内控件先处理, Handled 后不触发
            MouseRightButtonUp += OnMouseRightButtonUp;   // 右键点击
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
            Loaded += (_, _) => RecomputeHeight();        // 首次布局完成, ActualWidth 已有值
        }

        // ================= 对外契约 =================

        /// <summary>用户提供: 如何从数据项取出文件路径(用于拖出到外部)。</summary>
        public Func<object, string?>? FilePathSelector { get; set; }

        /// <summary>
        /// 外部数据拖入时调用。控件不判断拖入格式, 把原始拖放数据全权交给用户解析
        /// (文件 / 浏览器 URL / 文本 / 任意私有格式)。返回要插入的数据项集合;
        /// 返回 null 或空表示不接受本次拖入。控件负责把返回的项插入到落点位置。
        /// </summary>
        public Func<IDataObject, IEnumerable<object>?>? ExternalDropHandler { get; set; }

        /// <summary>列表自身增删改(插入/排序/添加)时调用, 供持久化。</summary>
        public Action? ContentChanged { get; set; }

        /// <summary>
        /// 把东西"放进某一项"时调用(不改变当前列表)。
        /// 参数: 被拖的数据(内部拖动时是项数据; 外部拖入时为 null),
        ///       原始拖放数据(外部拖入时有值, 内部拖动时为 null),
        ///       目标项。
        /// </summary>
        public Action<object?, IDataObject?, object>? ItemDropped { get; set; }

        /// <summary>是否允许"放进某一项"。关闭时一律按插入/添加处理。</summary>
        public bool AllowDropOnItem { get; set; } = false;

        /// <summary>项被左键点击时调用(点在项内控件并标记 Handled 的不会触发)。参数: 数据项。</summary>
        public Action<object>? LeftClick { get; set; }

        /// <summary>项被右键点击时调用(点在项内控件并标记 Handled 的不会触发)。参数: 数据项。</summary>
        public Action<object>? RightClick { get; set; }

        // ===== 附加属性: 标记当前高亮的放入目标项, 供样式 Trigger 使用 =====
        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsDropTarget", typeof(bool), typeof(JuiGrid),
                new PropertyMetadata(false));

        public static void SetIsDropTarget(DependencyObject o, bool v) => o.SetValue(IsDropTargetProperty, v);
        public static bool GetIsDropTarget(DependencyObject o) => (bool)o.GetValue(IsDropTargetProperty);

        // ================= 数据源安全访问 =================

        /// <summary>取出可写列表; 绑定的源不可写(只读视图等)时返回 null, 避免崩溃。</summary>
        private IList? WritableList =>
            ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;

        // ================= 对外公开方法 =================

        public void AddItem(object item)
        {
            var list = WritableList;
            if (list != null && item != null)
            {
                list.Add(item);
                ContentChanged?.Invoke();
            }
        }

        public void InsertItem(int index, object item)
        {
            var list = WritableList;
            if (list != null && item != null)
            {
                if (index < 0) index = 0;
                if (index > list.Count) index = list.Count;
                list.Insert(index, item);
                ContentChanged?.Invoke();
            }
        }

        public void RemoveItem(object item)
        {
            var list = WritableList;
            if (list != null && item != null)
            {
                int i = list.IndexOf(item);
                if (i >= 0)
                {
                    list.RemoveAt(i);
                    ContentChanged?.Invoke();
                }
            }
        }

        public void MoveItem(int oldIndex, int newIndex)
        {
            var list = WritableList;
            if (list == null) return;
            if (oldIndex < 0 || oldIndex >= list.Count) return;

            var item = list[oldIndex];
            list.RemoveAt(oldIndex);
            if (newIndex > oldIndex) newIndex--;
            if (newIndex < 0) newIndex = 0;
            if (newIndex > list.Count) newIndex = list.Count;
            list.Insert(newIndex, item);

            ContentChanged?.Invoke();
        }

        public object? GetItemFromElement(DependencyObject? element)
        {
            while (element != null && element is not ListBoxItem)
                element = VisualTreeHelper.GetParent(element);

            return element is ListBoxItem lbi
                ? ItemContainerGenerator.ItemFromContainer(lbi)
                : null;
        }

        // ================= 点击(左/右键分开, 无双击) =================

        private bool _dragHappened;   // 本次按下后是否已发起拖动

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 刚刚拖过 → 不当作点击
            if (_dragHappened) { _dragHappened = false; return; }

            var item = GetItemFromElement(e.OriginalSource as DependencyObject);
            if (item == null) return;   // 点在空白处

            LeftClick?.Invoke(item);
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = GetItemFromElement(e.OriginalSource as DependencyObject);
            if (item == null) return;   // 点在空白处

            RightClick?.Invoke(item);
        }

        // ================= 发起拖动 =================

        private Point _dragStart;
        private object? _dragItem;

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _dragItem = GetItemFromElement(e.OriginalSource as DependencyObject);
            _dragHappened = false;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_dragItem == null) return;

            var pos = e.GetPosition(null);
            var diff = _dragStart - pos;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragHappened = true;

            var data = new DataObject();
            data.SetData(JuiItemFormat, _dragItem);

            var path = FilePathSelector?.Invoke(_dragItem);
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                data.SetData(DataFormats.FileDrop, new[] { path });

            DragDrop.DoDragDrop(this, data, DragDropEffects.Move | DragDropEffects.Copy);

            _dragItem = null;
            ClearDropTargetHighlight();
            RemoveAdorner();
        }

        // ================= 拖动过程 + 高亮 =================

        private ListBoxItem? _highlighted;

        private void OnDragOver(object sender, DragEventArgs e)
        {
            bool isInternal = e.Data.GetDataPresent(JuiItemFormat);
            bool isExternal = !isInternal && ExternalDropHandler != null;

            if (!isInternal && !isExternal)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var targetContainer = AllowDropOnItem ? GetContainerUnderMouse(e) : null;

            if (targetContainer != null)
            {
                // 落在项上 → 移入: 高亮项, 隐藏插入线
                Highlight(targetContainer);
                _insertionAdorner?.Hide();
                _lastInsertIndex = -2;
            }
            else
            {
                // 落在间隙/空白 → 插入: 取消高亮, 显示插入线
                ClearDropTargetHighlight();
                ShowInsertionAt(GetInsertIndex(e));
            }

            e.Effects = isInternal ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            ClearDropTargetHighlight();
            RemoveAdorner();
        }

        private void Highlight(ListBoxItem container)
        {
            if (_highlighted == container) return;
            ClearDropTargetHighlight();
            _highlighted = container;
            SetIsDropTarget(container, true);
        }

        private void ClearDropTargetHighlight()
        {
            if (_highlighted != null)
            {
                SetIsDropTarget(_highlighted, false);
                _highlighted = null;
            }
        }

        // ================= 放下 =================

        private void OnDrop(object sender, DragEventArgs e)
        {
            ClearDropTargetHighlight();
            RemoveAdorner();

            var list = WritableList;
            if (list == null) return;

            bool isInternal = e.Data.GetDataPresent(JuiItemFormat);
            bool isExternal = !isInternal && ExternalDropHandler != null;
            if (!isInternal && !isExternal) return;

            // 是否落在某一项主体上(且开启移入)
            var targetContainer = AllowDropOnItem ? GetContainerUnderMouse(e) : null;
            object? targetItem = targetContainer != null
                ? ItemContainerGenerator.ItemFromContainer(targetContainer)
                : null;

            // ---------- 情况A: 移入某一项(不改变当前列表) ----------
            if (targetItem != null)
            {
                if (isInternal)
                {
                    var dragged = e.Data.GetData(JuiItemFormat);
                    if (dragged != null && !ReferenceEquals(dragged, targetItem))
                        ItemDropped?.Invoke(dragged, null, targetItem);
                }
                else // 外部: 原始数据交给用户
                {
                    ItemDropped?.Invoke(null, e.Data, targetItem);
                }
                e.Handled = true;
                return;
            }

            // ---------- 情况B: 插入到间隙 / 添加到末尾(改变列表) ----------
            int insertIndex = GetInsertIndex(e);

            if (isInternal)
            {
                var dragged = e.Data.GetData(JuiItemFormat);
                if (dragged == null) return;
                int oldIndex = list.IndexOf(dragged);
                if (oldIndex < 0) return;
                MoveItem(oldIndex, insertIndex);
            }
            else // 外部: 控件不解析, 交给用户返回数据项, 控件负责插入
            {
                var items = ExternalDropHandler!(e.Data);
                if (items != null)
                {
                    bool added = false;
                    int idx = insertIndex;
                    foreach (var item in items)
                    {
                        if (item == null) continue;
                        if (idx < 0 || idx > list.Count) idx = list.Count;
                        list.Insert(idx, item);
                        idx++;
                        added = true;
                    }
                    if (added) ContentChanged?.Invoke();
                }
            }

            e.Handled = true;
        }

        // ================= 落点判定 =================

        /// <summary>鼠标当前是否悬停在某一项主体上, 是则返回容器, 否则 null(落在间隙/空白)。</summary>
        private ListBoxItem? GetContainerUnderMouse(DragEventArgs e)
        {
            var pos = e.GetPosition(this);
            var hit = InputHitTest(pos) as DependencyObject;
            while (hit != null && hit is not ListBoxItem)
                hit = VisualTreeHelper.GetParent(hit);
            return hit as ListBoxItem;
        }

        /// <summary>
        /// 算出插入索引(0..Count)。优先精确命中项的左右半边;
        /// 没命中则找几何上最近的项, 按方位决定插在其前或后。
        /// </summary>
        private int GetInsertIndex(DragEventArgs e)
        {
            if (ItemsSource is not IList list || list.Count == 0) return 0;

            var pos = e.GetPosition(this);

            // 1) 精确命中某一项
            var container = GetContainerUnderMouse(e);
            if (container != null)
            {
                var item = ItemContainerGenerator.ItemFromContainer(container);
                int index = list.IndexOf(item);
                if (index >= 0)
                {
                    var p = e.GetPosition(container);
                    bool after = p.X > container.ActualWidth / 2;
                    return after ? index + 1 : index;
                }
            }

            // 2) 没命中: 找最近项, 按几何方位决定前/后
            int bestIndex = -1;
            double bestDist = double.MaxValue;
            bool insertAfterBest = false;

            for (int i = 0; i < list.Count; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem c) continue;
                if (!c.IsVisible) continue;

                var topLeft = c.TranslatePoint(new Point(0, 0), this);
                var rect = new Rect(topLeft, new Size(c.ActualWidth, c.ActualHeight));
                var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

                double dx = pos.X - center.X;
                double dy = pos.Y - center.Y;
                double dist = dx * dx + dy * dy;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;

                    if (pos.Y > rect.Bottom) insertAfterBest = true;        // 下方 → 之后
                    else if (pos.Y < rect.Top) insertAfterBest = false;     // 上方 → 之前
                    else insertAfterBest = pos.X > center.X;                 // 同行 → 看左右半边
                }
            }

            if (bestIndex < 0) return list.Count;
            return insertAfterBest ? bestIndex + 1 : bestIndex;
        }

        // ================= 插入指示线 =================

        private void EnsureAdorner()
        {
            if (_insertionAdorner == null)
            {
                var layer = AdornerLayer.GetAdornerLayer(this);
                if (layer != null)
                {
                    _insertionAdorner = new InsertionAdorner(this);
                    layer.Add(_insertionAdorner);
                }
            }
        }

        private void RemoveAdorner()
        {
            if (_insertionAdorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(this);
                layer?.Remove(_insertionAdorner);
                _insertionAdorner = null;
            }
            _lastInsertIndex = -2;
        }

        /// <summary>根据插入索引, 把指示线画到两项之间的间隙正中。</summary>
        private void ShowInsertionAt(int insertIndex)
        {
            if (insertIndex == _lastInsertIndex) return;
            _lastInsertIndex = insertIndex;

            EnsureAdorner();
            if (_insertionAdorner == null) return;

            if (ItemsSource is not IList list || list.Count == 0)
            {
                _insertionAdorner.Hide();
                return;
            }

            double x, top, bottom;

            ListBoxItem? cur = insertIndex < list.Count
                ? ItemContainerGenerator.ContainerFromIndex(insertIndex) as ListBoxItem
                : null;

            ListBoxItem? prev = insertIndex - 1 >= 0 && insertIndex - 1 < list.Count
                ? ItemContainerGenerator.ContainerFromIndex(insertIndex - 1) as ListBoxItem
                : null;

            if (cur != null)
            {
                var curTL = cur.TranslatePoint(new Point(0, 0), this);
                double curLeft = curTL.X;
                top = curTL.Y;
                bottom = curTL.Y + cur.ActualHeight;

                if (prev != null)
                {
                    var prevTL = prev.TranslatePoint(new Point(0, 0), this);
                    double prevRight = prevTL.X + prev.ActualWidth;
                    bool sameRow = Math.Abs(prevTL.Y - curTL.Y) < 1;
                    x = sameRow ? (prevRight + curLeft) / 2 : curLeft - 2;
                }
                else
                {
                    x = curLeft - 2;   // 插到最前面
                }

                _insertionAdorner.Update(x, top, bottom);
                return;
            }

            // 末尾: 画在最后一项右边缘稍外侧
            if (ItemContainerGenerator.ContainerFromIndex(list.Count - 1) is ListBoxItem last)
            {
                var lastTL = last.TranslatePoint(new Point(0, 0), this);
                x = lastTL.X + last.ActualWidth + 2;
                top = lastTL.Y;
                bottom = lastTL.Y + last.ActualHeight;
                _insertionAdorner.Update(x, top, bottom);
            }
        }

        // ================= 依赖属性 =================

        /// <summary>列表为空时显示的内容(由用户提供, 任意 UI)。</summary>
        public object? EmptyContent
        {
            get => GetValue(EmptyContentProperty);
            set => SetValue(EmptyContentProperty, value);
        }
        public static readonly DependencyProperty EmptyContentProperty =
            DependencyProperty.Register(nameof(EmptyContent), typeof(object),
                typeof(JuiGrid), new PropertyMetadata(null));

        /// <summary>项的间隔(单边)。实际项与项之间的间隙为此值的 2 倍。默认 0。</summary>
        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }
        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(0.0, (d, e) => ((JuiGrid)d).RecomputeHeight()));

        /// <summary>最多显示几行, 超过则滚动。默认 3。</summary>
        public int MaxRows
        {
            get => (int)GetValue(MaxRowsProperty);
            set => SetValue(MaxRowsProperty, value);
        }
        public static readonly DependencyProperty MaxRowsProperty =
            DependencyProperty.Register(nameof(MaxRows), typeof(int),
                typeof(JuiGrid), new PropertyMetadata(3, (d, e) => ((JuiGrid)d).RecomputeHeight()));

        /// <summary>每项容器的完整高度(不含间隔)。间隔由 ItemSpacing 控制。</summary>
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(148.0, (d, e) => ((JuiGrid)d).RecomputeHeight()));

        /// <summary>每项容器的完整宽度(不含间隔)。间隔由 ItemSpacing 控制。</summary>
        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(nameof(ItemWidth), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(128.0, (d, e) => ((JuiGrid)d).RecomputeHeight()));




        /// <summary>每个项的圆角半径。内容(包括图片)会被裁剪进此圆角内。</summary>
        public CornerRadius ItemCornerRadius
        {
            get => (CornerRadius)GetValue(ItemCornerRadiusProperty);
            set => SetValue(ItemCornerRadiusProperty, value);
        }
        public static readonly DependencyProperty ItemCornerRadiusProperty =
            DependencyProperty.Register(nameof(ItemCornerRadius), typeof(CornerRadius),
                typeof(JuiGrid), new PropertyMetadata(new CornerRadius(6)));



        // 通知外部项目进入视口了 
        public Action<object>? ItemPreparing;


        // ================= 高度自治(控件内部, 使用者无感) =================

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            RecomputeHeight();          // 数量变化
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged)
            {
                RecomputeHeight();
                ClampScrollOffset();   // 宽度变了 → 内容高度可能变矮 → 修正超界的滚动偏移
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is FrameworkElement fe)
            {
                fe.Width = ItemWidth;
                fe.Height = ItemHeight;
            }

            ItemPreparing?.Invoke(item);
        }




        /// <summary>
        /// 根据当前宽度和数据量, 把固定高度写入控件。
        /// 宽度尚未就绪(ActualWidth=0)时跳过, 等 Loaded / 尺寸变化时自然补算。
        /// 用确定高度给虚拟化提供固定视口, 一次测量即正确, 无需任何手动刷新。
        /// </summary>
        private void RecomputeHeight()
        {
            if (ItemHeight <= 0) return;
            if (ActualWidth <= 0) return;                  // 关键: 宽度没就绪就不算, 不写错值
            if (ItemsSource is not IList list) return;

            double cellWidth = ItemWidth + ItemSpacing * 2;
            double cellHeight = ItemHeight + ItemSpacing * 2;

            double available = ActualWidth - Padding.Left - Padding.Right
                               - BorderThickness.Left - BorderThickness.Right;

            int cols = available > 0 && cellWidth > 0
                ? Math.Max(1, (int)(available / cellWidth))
                : 1;

            int count = list.Count;
            int rows = count <= 0 ? 1 : (int)Math.Ceiling(count / (double)cols);
            int shownRows = Math.Min(rows, Math.Max(1, MaxRows));
            Height = shownRows * cellHeight + Padding.Top + Padding.Bottom
                     + BorderThickness.Top + BorderThickness.Bottom;

            System.Diagnostics.Debug.WriteLine(
                $"[RecomputeHeight] count={list.Count}, cols={cols}, rows={rows}, shownRows={shownRows}, Height={Height}");
        }








        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_Scroll") as ScrollViewer;
        }

        /// <summary>
        /// 内容高度变化后, 若当前垂直滚动偏移超出了新的可滚动范围, 把它拉回最大值。
        /// 解决"拉到底再变宽, 内容变矮但滚动条没回收, 底部空一行"的问题。
        /// </summary>
        private void ClampScrollOffset()
        {
            if (_scrollViewer == null) return;

            // 等本轮布局完成、ScrollableHeight 更新后再夹, 否则拿到的还是旧值
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                if (_scrollViewer == null) return;
                double max = _scrollViewer.ScrollableHeight;
                if (_scrollViewer.VerticalOffset > max)
                    _scrollViewer.ScrollToVerticalOffset(max);
            }));
        }
    }
}
