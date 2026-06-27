using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JUI.Controls
{
    /// <summary>
    /// JUI 通用网格控件: 多列布局 + 虚拟化 + 拖动排序 + 拖入/拖出 + 移入项(类资源管理器)。
    /// 落点判定: 间隙/空白 = 插入或排序; 项主体上 = 移入该项(需开启 AllowDropOnItem)。
    /// 外部拖入的数据格式不由控件判断, 统一通过 ExternalDropHandler 交给用户解析。
    /// 单选; 左右键点击分别通过 LeftClick / RightClick 暴露(项内控件标记 Handled 即可不触发)。
    /// 高度由控件按数据量与宽度自动算成固定值, 给虚拟化提供稳定视口。
    /// </summary>
    public class JuiGrid : ListBox
    {
        private const string JuiItemFormat = "JUI.InternalDragItem";

        private InsertionAdorner? _insertionAdorner;
        private int _lastInsertIndex = -2;     // 缓存上次插入位置, -2 为无效初值
        private ScrollViewer? _scrollViewer;

        // 批量更新: 计数 >0 时挂起高度重算, 归零时补算一次
        private int _bulkDepth;
        private bool _heightDirty;

        static JuiGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiGrid), new FrameworkPropertyMetadata(typeof(JuiGrid)));
        }

        public JuiGrid()
        {
            AllowDrop = true;
            SelectionMode = SelectionMode.Single;

            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonUp += OnMouseRightButtonUp;
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => RecomputeHeight();

        // ================= 对外契约 =================

        /// <summary>如何从数据项取出文件路径(用于拖出到外部)。</summary>
        public Func<object, string?>? FilePathSelector { get; set; }

        /// <summary>
        /// 外部数据拖入时调用。控件不判断格式, 把原始数据交给用户解析, 返回要插入的数据项;
        /// 返回 null 或空表示不接受。控件负责按落点插入。
        /// </summary>
        public Func<IDataObject, IEnumerable<object>?>? ExternalDropHandler { get; set; }

        /// <summary>列表自身增删改(插入/排序/添加)时调用, 供持久化。</summary>
        public Action? ContentChanged { get; set; }

        /// <summary>
        /// 把东西"放进某一项"时调用(不改变当前列表)。参数依次为:
        /// 被拖数据(内部=项数据, 外部=null)、原始拖放数据(外部有值, 内部=null)、目标项。
        /// </summary>
        public Action<object?, IDataObject?, object>? ItemDropped { get; set; }

        /// <summary>是否允许"放进某一项"。关闭时一律按插入/添加处理。</summary>
        public bool AllowDropOnItem { get; set; }

        /// <summary>项被左键点击时调用(项内控件标记 Handled 的不触发)。参数: 数据项。</summary>
        public Action<object>? LeftClick { get; set; }

        /// <summary>项被右键点击时调用(项内控件标记 Handled 的不触发)。参数: 数据项。</summary>
        public Action<object>? RightClick { get; set; }

        /// <summary>容器进入视口(被实现)时调用, 供懒加载缩略图等。参数: 数据项。</summary>
        public Action<object>? ItemPreparing { get; set; }

        // ===== 附加属性: 标记当前高亮的放入目标项 =====
        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsDropTarget", typeof(bool), typeof(JuiGrid), new PropertyMetadata(false));

        public static void SetIsDropTarget(DependencyObject o, bool v) => o.SetValue(IsDropTargetProperty, v);
        public static bool GetIsDropTarget(DependencyObject o) => (bool)o.GetValue(IsDropTargetProperty);

        // ================= 批量更新 =================

        /// <summary>
        /// 开始批量更新: 挂起内部高度重算, 避免逐项灌入时的布局风暴。
        /// 必须与 EndBulkUpdate 成对, 支持嵌套。推荐用 using (grid.BulkUpdate()) {...}。
        /// </summary>
        public void BeginBulkUpdate() => _bulkDepth++;

        /// <summary>结束批量更新: 计数归零时补算一次高度。</summary>
        public void EndBulkUpdate()
        {
            if (_bulkDepth == 0) return;
            if (--_bulkDepth == 0 && _heightDirty)
            {
                _heightDirty = false;
                RecomputeHeight();
            }
        }

        /// <summary>using 友好的批量更新作用域。</summary>
        public IDisposable BulkUpdate()
        {
            BeginBulkUpdate();
            return new BulkScope(this);
        }

        private sealed class BulkScope : IDisposable
        {
            private JuiGrid? _owner;
            public BulkScope(JuiGrid owner) => _owner = owner;
            public void Dispose() { _owner?.EndBulkUpdate(); _owner = null; }
        }

        // ================= 数据源安全访问 =================

        private IList? WritableList =>
            ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;

        // ================= 公开方法 =================

        public void AddItem(object item)
        {
            var list = WritableList;
            if (list == null || item == null) return;
            list.Add(item);
            ContentChanged?.Invoke();
        }

        public void InsertItem(int index, object item)
        {
            var list = WritableList;
            if (list == null || item == null) return;
            index = Math.Clamp(index, 0, list.Count);
            list.Insert(index, item);
            ContentChanged?.Invoke();
        }

        public void RemoveItem(object item)
        {
            var list = WritableList;
            if (list == null || item == null) return;
            int i = list.IndexOf(item);
            if (i < 0) return;
            list.RemoveAt(i);
            ContentChanged?.Invoke();
        }

        public void MoveItem(int oldIndex, int newIndex)
        {
            var list = WritableList;
            if (list == null || oldIndex < 0 || oldIndex >= list.Count) return;

            var item = list[oldIndex];
            list.RemoveAt(oldIndex);
            if (newIndex > oldIndex) newIndex--;
            newIndex = Math.Clamp(newIndex, 0, list.Count);
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

        private bool _dragHappened;

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragHappened) { _dragHappened = false; return; }
            var item = GetItemFromElement(e.OriginalSource as DependencyObject);
            if (item != null) LeftClick?.Invoke(item);
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = GetItemFromElement(e.OriginalSource as DependencyObject);
            if (item != null) RightClick?.Invoke(item);
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
            if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null) return;

            var diff = _dragStart - e.GetPosition(null);
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

            var target = AllowDropOnItem ? GetContainerUnderMouse(e) : null;
            if (target != null)
            {
                Highlight(target);
                _insertionAdorner?.Hide();
                _lastInsertIndex = -2;
            }
            else
            {
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
            if (_highlighted == null) return;
            SetIsDropTarget(_highlighted, false);
            _highlighted = null;
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

            // 落在某一项主体上(且开启移入)
            var targetContainer = AllowDropOnItem ? GetContainerUnderMouse(e) : null;
            object? targetItem = targetContainer != null
                ? ItemContainerGenerator.ItemFromContainer(targetContainer)
                : null;

            // ---- 情况A: 移入某一项(不改变列表) ----
            if (targetItem != null)
            {
                if (isInternal)
                {
                    var dragged = e.Data.GetData(JuiItemFormat);
                    if (dragged != null && !ReferenceEquals(dragged, targetItem))
                        ItemDropped?.Invoke(dragged, null, targetItem);
                }
                else
                {
                    ItemDropped?.Invoke(null, e.Data, targetItem);
                }
                e.Handled = true;
                return;
            }

            // ---- 情况B: 插入间隙 / 末尾(改变列表) ----
            int insertIndex = GetInsertIndex(e);

            if (isInternal)
            {
                var dragged = e.Data.GetData(JuiItemFormat);
                if (dragged == null) return;
                int oldIndex = list.IndexOf(dragged);
                if (oldIndex >= 0) MoveItem(oldIndex, insertIndex);
            }
            else
            {
                var items = ExternalDropHandler!(e.Data);
                if (items != null)
                {
                    bool added = false;
                    int idx = insertIndex;
                    using (BulkUpdate())   // 多项一次性插入, 抑制逐项重算
                    {
                        foreach (var item in items)
                        {
                            if (item == null) continue;
                            if (idx < 0 || idx > list.Count) idx = list.Count;
                            list.Insert(idx++, item);
                            added = true;
                        }
                    }
                    if (added) ContentChanged?.Invoke();
                }
            }

            e.Handled = true;
        }

        // ================= 落点判定 =================

        private ListBoxItem? GetContainerUnderMouse(DragEventArgs e)
        {
            var hit = InputHitTest(e.GetPosition(this)) as DependencyObject;
            while (hit != null && hit is not ListBoxItem)
                hit = VisualTreeHelper.GetParent(hit);
            return hit as ListBoxItem;
        }

        /// <summary>算出插入索引(0..Count): 先精确命中项的左右半边, 否则取几何最近项的前/后。</summary>
        private int GetInsertIndex(DragEventArgs e)
        {
            if (ItemsSource is not IList list || list.Count == 0) return 0;

            var pos = e.GetPosition(this);

            // 1) 精确命中某一项
            var container = GetContainerUnderMouse(e);
            if (container != null)
            {
                int index = list.IndexOf(ItemContainerGenerator.ItemFromContainer(container));
                if (index >= 0)
                {
                    bool after = e.GetPosition(container).X > container.ActualWidth / 2;
                    return after ? index + 1 : index;
                }
            }

            // 2) 没命中: 找几何最近的已实现项, 按方位决定前/后
            int bestIndex = -1;
            double bestDist = double.MaxValue;
            bool insertAfterBest = false;

            for (int i = 0; i < list.Count; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem c || !c.IsVisible)
                    continue;

                var topLeft = c.TranslatePoint(new Point(0, 0), this);
                var rect = new Rect(topLeft, new Size(c.ActualWidth, c.ActualHeight));
                var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

                double dx = pos.X - center.X, dy = pos.Y - center.Y;
                double dist = dx * dx + dy * dy;
                if (dist >= bestDist) continue;

                bestDist = dist;
                bestIndex = i;
                if (pos.Y > rect.Bottom) insertAfterBest = true;
                else if (pos.Y < rect.Top) insertAfterBest = false;
                else insertAfterBest = pos.X > center.X;
            }

            if (bestIndex < 0) return list.Count;
            return insertAfterBest ? bestIndex + 1 : bestIndex;
        }

        // ================= 插入指示线 =================

        private void EnsureAdorner()
        {
            if (_insertionAdorner != null) return;
            var layer = AdornerLayer.GetAdornerLayer(this);
            if (layer == null) return;
            _insertionAdorner = new InsertionAdorner(this);
            layer.Add(_insertionAdorner);
        }

        private void RemoveAdorner()
        {
            if (_insertionAdorner != null)
            {
                AdornerLayer.GetAdornerLayer(this)?.Remove(_insertionAdorner);
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

            ListBoxItem? cur = insertIndex < list.Count
                ? ItemContainerGenerator.ContainerFromIndex(insertIndex) as ListBoxItem
                : null;
            ListBoxItem? prev = insertIndex - 1 >= 0 && insertIndex - 1 < list.Count
                ? ItemContainerGenerator.ContainerFromIndex(insertIndex - 1) as ListBoxItem
                : null;

            double x, top, bottom;

            if (cur != null)
            {
                var curTL = cur.TranslatePoint(new Point(0, 0), this);
                top = curTL.Y;
                bottom = curTL.Y + cur.ActualHeight;

                if (prev != null)
                {
                    var prevTL = prev.TranslatePoint(new Point(0, 0), this);
                    double prevRight = prevTL.X + prev.ActualWidth;
                    bool sameRow = Math.Abs(prevTL.Y - curTL.Y) < 1;
                    x = sameRow ? (prevRight + curTL.X) / 2 : curTL.X - 2;
                }
                else x = curTL.X - 2;

                _insertionAdorner.Update(x, top, bottom);
                return;
            }

            // 末尾: 画在最后一项右边缘稍外侧
            if (ItemContainerGenerator.ContainerFromIndex(list.Count - 1) is ListBoxItem last)
            {
                var lastTL = last.TranslatePoint(new Point(0, 0), this);
                _insertionAdorner.Update(
                    lastTL.X + last.ActualWidth + 2, lastTL.Y, lastTL.Y + last.ActualHeight);
            }
        }

        // ================= 依赖属性 =================

        public object? EmptyContent
        {
            get => GetValue(EmptyContentProperty);
            set => SetValue(EmptyContentProperty, value);
        }
        public static readonly DependencyProperty EmptyContentProperty =
            DependencyProperty.Register(nameof(EmptyContent), typeof(object),
                typeof(JuiGrid), new PropertyMetadata(null));

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }
        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(0.0, OnLayoutAffectingChanged));

        public int MaxRows
        {
            get => (int)GetValue(MaxRowsProperty);
            set => SetValue(MaxRowsProperty, value);
        }
        public static readonly DependencyProperty MaxRowsProperty =
            DependencyProperty.Register(nameof(MaxRows), typeof(int),
                typeof(JuiGrid), new PropertyMetadata(3, OnLayoutAffectingChanged));

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(148.0, OnLayoutAffectingChanged));

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(nameof(ItemWidth), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(128.0, OnLayoutAffectingChanged));

        public CornerRadius ItemCornerRadius
        {
            get => (CornerRadius)GetValue(ItemCornerRadiusProperty);
            set => SetValue(ItemCornerRadiusProperty, value);
        }
        public static readonly DependencyProperty ItemCornerRadiusProperty =
            DependencyProperty.Register(nameof(ItemCornerRadius), typeof(CornerRadius),
                typeof(JuiGrid), new PropertyMetadata(new CornerRadius(6)));

        private static void OnLayoutAffectingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((JuiGrid)d).RecomputeHeight();

        // ================= 高度自治 =================

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            RecomputeHeight();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged)
            {
                RecomputeHeight();
                ClampScrollOffset();
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

        /// <summary>按当前宽度和数据量算出固定高度。批量更新期间挂起, 结束补算。</summary>
        private void RecomputeHeight()
        {
            if (_bulkDepth > 0) { _heightDirty = true; return; }

            if (ItemHeight <= 0 || ActualWidth <= 0) return;
            if (ItemsSource is not IList list) return;

            double cellWidth = ItemWidth + ItemSpacing * 2;
            double cellHeight = ItemHeight + ItemSpacing * 2;

            double available = ActualWidth - Padding.Left - Padding.Right
                               - BorderThickness.Left - BorderThickness.Right;

            int cols = available > 0 && cellWidth > 0
                ? Math.Max(1, (int)(available / cellWidth)) : 1;

            int rows = list.Count <= 0 ? 1 : (int)Math.Ceiling(list.Count / (double)cols);
            int shownRows = Math.Min(rows, Math.Max(1, MaxRows));

            Height = shownRows * cellHeight + Padding.Top + Padding.Bottom
                     + BorderThickness.Top + BorderThickness.Bottom;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_Scroll") as ScrollViewer;
        }

        /// <summary>内容变矮后, 若滚动偏移超界则拉回最大值。</summary>
        private void ClampScrollOffset()
        {
            if (_scrollViewer == null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_scrollViewer == null) return;
                double max = _scrollViewer.ScrollableHeight;
                if (_scrollViewer.VerticalOffset > max)
                    _scrollViewer.ScrollToVerticalOffset(max);
            }));
        }
    }
}
