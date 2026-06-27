using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace JUI.Controls
{
    /// <summary>
    /// JUI 通用网格控件: 多列布局 + 虚拟化 + 拖动排序 + 拖入/拖出文件 + 移入项(类资源管理器)。
    /// 落点判定: 间隙/空白=插入或排序; 项主体上=移入该项(需开启 AllowDropOnItem)。
    /// </summary>
    public class JuiGrid : ListBox
    {
        private const string JuiItemFormat = "JUI.InternalDragItem";

        private InsertionAdorner? _insertionAdorner;
        private int _lastInsertIndex = -2;   // 缓存上次插入位置, -2 表示无效初值


        static JuiGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiGrid),
                new FrameworkPropertyMetadata(typeof(JuiGrid)));
        }

        public JuiGrid()
        {
            AllowDrop = true;

            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
        }

        // ================= 对外契约 =================

        /// <summary>用户提供: 如何从数据项取出文件路径(用于拖出到外部)。</summary>
        public Func<object, string?>? FilePathSelector { get; set; }

        /// <summary>用户提供: 如何把拖入的文件路径转成数据项。</summary>
        public Func<string, object?>? FileToItemConverter { get; set; }

        /// <summary>列表自身增删改(插入/排序/添加)时调用, 供持久化。</summary>
        public Action? OnContentChanged { get; set; }

        /// <summary>
        /// 把东西"放进某一项"时调用(不改变当前列表)。
        /// 参数: 被拖的数据对象(内部拖动时是项数据; 外部拖入时是转换后的项, 可能为 null),
        ///       文件路径(外部拖入时有值, 内部拖动时为 null),
        ///       目标项。
        /// </summary>
        public Action<object?, string?, object>? OnItemDroppedOnItem { get; set; }

        /// <summary>是否允许"放进某一项"。关闭时一律按插入/添加处理。</summary>
        public bool AllowDropOnItem { get; set; } = false;

        // ===== 附加属性: 标记当前高亮的放入目标项, 供样式 Trigger 使用 =====
        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsDropTarget", typeof(bool), typeof(JuiGrid),
                new PropertyMetadata(false));

        public static void SetIsDropTarget(DependencyObject o, bool v) => o.SetValue(IsDropTargetProperty, v);
        public static bool GetIsDropTarget(DependencyObject o) => (bool)o.GetValue(IsDropTargetProperty);

        // ================= 对外公开方法 =================

        public void AddItem(object item)
        {
            if (ItemsSource is IList list && item != null)
            {
                list.Add(item);
                OnContentChanged?.Invoke();
            }
        }

        public void InsertItem(int index, object item)
        {
            if (ItemsSource is IList list && item != null)
            {
                if (index < 0) index = 0;
                if (index > list.Count) index = list.Count;
                list.Insert(index, item);
                OnContentChanged?.Invoke();
            }
        }

        public void RemoveItem(object item)
        {
            if (ItemsSource is IList list && item != null && list.Contains(item))
            {
                list.Remove(item);
                OnContentChanged?.Invoke();
            }
        }

        public void MoveItem(int oldIndex, int newIndex)
        {
            if (ItemsSource is not IList list) return;
            if (oldIndex < 0 || oldIndex >= list.Count) return;

            var item = list[oldIndex];
            list.RemoveAt(oldIndex);
            if (newIndex > oldIndex) newIndex--;
            if (newIndex < 0) newIndex = 0;
            if (newIndex > list.Count) newIndex = list.Count;
            list.Insert(newIndex, item);

            OnContentChanged?.Invoke();
        }

        public object? GetItemFromElement(DependencyObject? element)
        {
            while (element != null && element is not ListBoxItem)
                element = VisualTreeHelper.GetParent(element);

            return element is ListBoxItem lbi
                ? ItemContainerGenerator.ItemFromContainer(lbi)
                : null;
        }

        // ================= 发起拖动 =================

        private Point _dragStart;
        private object? _dragItem;

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _dragItem = GetItemFromElement(e.OriginalSource as DependencyObject);
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
            bool isExternal = e.Data.GetDataPresent(DataFormats.FileDrop) && FileToItemConverter != null;

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
                e.Effects = isInternal ? DragDropEffects.Move : DragDropEffects.Copy;
            }
            else
            {
                // 落在间隙/空白 → 插入: 取消高亮, 显示插入线
                ClearDropTargetHighlight();
                ShowInsertionAt(GetInsertIndex(e));
                e.Effects = isInternal ? DragDropEffects.Move : DragDropEffects.Copy;
            }

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
            if (ItemsSource is not IList list) return;

            bool isInternal = e.Data.GetDataPresent(JuiItemFormat);
            bool isExternal = e.Data.GetDataPresent(DataFormats.FileDrop) && FileToItemConverter != null;
            if (!isInternal && !isExternal) return;

            // 先判断: 是否落在某一项主体上(且开启移入)
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
                        OnItemDroppedOnItem?.Invoke(dragged, null, targetItem);
                }
                else // 外部文件
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    foreach (var f in files)
                    {
                        var converted = FileToItemConverter!(f);
                        OnItemDroppedOnItem?.Invoke(converted, f, targetItem);
                    }
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
            else // 外部文件: 插入到对应位置
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool added = false;
                int idx = insertIndex;
                foreach (var f in files)
                {
                    var item = FileToItemConverter!(f);
                    if (item != null)
                    {
                        if (idx < 0) idx = list.Count;
                        if (idx > list.Count) idx = list.Count;
                        list.Insert(idx, item);
                        idx++;
                        added = true;
                    }
                }
                if (added) OnContentChanged?.Invoke();
            }

            e.Handled = true;
        }

        // ================= 落点判定 =================

        /// <summary>鼠标当前是否悬停在某一项的主体上, 是则返回该容器, 否则 null(落在间隙/空白)。</summary>
        private ListBoxItem? GetContainerUnderMouse(DragEventArgs e)
        {
            var pos = e.GetPosition(this);
            var hit = InputHitTest(pos) as DependencyObject;
            while (hit != null && hit is not ListBoxItem)
                hit = VisualTreeHelper.GetParent(hit);
            return hit as ListBoxItem;
        }

        /// <summary>
        /// 算出插入索引(0..Count)。
        /// 优先精确命中项的左右半边; 没命中则找最近的项, 按几何方位决定插在其前或后;
        /// 只有鼠标在所有项之后时才真正落到末尾。
        /// </summary>
        private int GetInsertIndex(DragEventArgs e)
        {
            if (ItemsSource is not IList list || list.Count == 0) return 0;

            var pos = e.GetPosition(this);

            // 1) 先尝试精确命中某一项
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

            // 2) 没命中: 遍历所有已生成的容器, 找几何上"最近"的项, 决定插在它前或后
            int bestIndex = -1;
            double bestDist = double.MaxValue;
            bool insertAfterBest = false;

            for (int i = 0; i < list.Count; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem c) continue;
                if (!c.IsVisible) continue;

                // 该项相对于本控件的矩形
                var topLeft = c.TranslatePoint(new Point(0, 0), this);
                var rect = new Rect(topLeft, new Size(c.ActualWidth, c.ActualHeight));
                var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

                // 用鼠标到项中心的距离找最近项
                double dx = pos.X - center.X;
                double dy = pos.Y - center.Y;
                double dist = dx * dx + dy * dy;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;

                    // 优先按"同一行内的左右"判断; 若鼠标明显在该项下方一行, 则算其后
                    if (pos.Y > rect.Bottom)
                        insertAfterBest = true;                    // 在该项下方 → 之后
                    else if (pos.Y < rect.Top)
                        insertAfterBest = false;                   // 在该项上方 → 之前
                    else
                        insertAfterBest = pos.X > center.X;        // 同行 → 看左右半边
                }
            }

            if (bestIndex < 0) return list.Count;   // 一个容器都没生成(极端情况)

            return insertAfterBest ? bestIndex + 1 : bestIndex;
        }








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

        /// <summary>根据插入索引, 把指示线画到对应的项边缘。</summary>
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

            // 当前项(insertIndex 指向的项, 线画在它左侧)
            ListBoxItem? cur = insertIndex < list.Count
                ? ItemContainerGenerator.ContainerFromIndex(insertIndex) as ListBoxItem
                : null;

            // 前一项(用来取间隙左界)
            ListBoxItem? prev = insertIndex - 1 >= 0 && insertIndex - 1 < list.Count
                ? ItemContainerGenerator.ContainerFromIndex(insertIndex - 1) as ListBoxItem
                : null;

            if (cur != null)
            {
                var curTL = cur.TranslatePoint(new Point(0, 0), this);
                double curLeft = curTL.X;
                top = curTL.Y;
                bottom = curTL.Y + cur.ActualHeight;

                // 如果前一项和当前项在同一行, 线取两者之间间隙的中点
                if (prev != null)
                {
                    var prevTL = prev.TranslatePoint(new Point(0, 0), this);
                    double prevRight = prevTL.X + prev.ActualWidth;
                    bool sameRow = System.Math.Abs(prevTL.Y - curTL.Y) < 1;

                    x = sameRow ? (prevRight + curLeft) / 2 : curLeft - 2;
                }
                else
                {
                    // 没有前一项(插到最前面): 画在当前项左边缘稍外侧
                    x = curLeft - 2;
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


        /// <summary>列表为空时显示的内容(由用户提供, 任意 UI)。</summary>
        public object? EmptyContent
        {
            get => GetValue(EmptyContentProperty);
            set => SetValue(EmptyContentProperty, value);
        }
        public static readonly DependencyProperty EmptyContentProperty =
            DependencyProperty.Register(nameof(EmptyContent), typeof(object),
                typeof(JuiGrid), new PropertyMetadata(null));





        /// <summary>最多显示几行, 超过则滚动。默认 3。</summary>
        public int MaxRows
        {
            get => (int)GetValue(MaxRowsProperty);
            set => SetValue(MaxRowsProperty, value);
        }
        public static readonly DependencyProperty MaxRowsProperty =
            DependencyProperty.Register(nameof(MaxRows), typeof(int),
                typeof(JuiGrid), new PropertyMetadata(3, (d, e) => ((JuiGrid)d).UpdateAutoHeight()));

        /// <summary>每项的高度(含间距), 用于计算行高。需与 ItemTemplate 实际高度一致。</summary>
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(148.0, (d, e) => ((JuiGrid)d).UpdateAutoHeight()));

        /// <summary>每项的宽度(含间距), 用于计算列数。</summary>
        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(nameof(ItemWidth), typeof(double),
                typeof(JuiGrid), new PropertyMetadata(128.0, (d, e) => ((JuiGrid)d).UpdateAutoHeight()));




        protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            UpdateAutoHeight();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged) UpdateAutoHeight();
        }

        private void UpdateAutoHeight()
        {
            if (ItemWidth <= 0 || ItemHeight <= 0) return;
            if (ItemsSource is not IList list) { return; }

            int count = list.Count;
            if (count == 0)
            {
                // 空列表给一个最小高度, 让 EmptyContent 有地方显示
                MaxHeight = ItemHeight;
                return;
            }

            // 根据当前可用宽度算每行能放几列
            double available = ActualWidth > 0 ? ActualWidth : Width;
            int cols = available > 0 ? System.Math.Max(1, (int)(available / ItemWidth)) : 1;

            // 当前实际需要几行
            int rows = (int)System.Math.Ceiling(count / (double)cols);

            // 取 "实际行数" 和 "最大行数" 的较小值
            int shownRows = System.Math.Min(rows, System.Math.Max(1, MaxRows));

            // 设置高度上限 = 行数 × 行高 (留一点边距)
            MaxHeight = shownRows * ItemHeight + Padding.Top + Padding.Bottom + 4;
        }



    }
}
