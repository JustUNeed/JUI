using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace JUI.Controls
{
    /// <summary>
    /// 插入位置指示器: 在被装饰元素(JuiGrid)的上层画一条竖线。
    /// 只负责绘制, 线的位置由外部设置。
    /// </summary>
    internal class InsertionAdorner : Adorner
    {
        private double _x;          // 竖线的 X 坐标
        private double _top;        // 竖线顶端 Y
        private double _bottom;     // 竖线底端 Y
        private bool _visible;

        private readonly Pen _pen;

        public InsertionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;   // 不挡鼠标
            var brush = TryFindAccentBrush();
            _pen = new Pen(brush, 2);
            _pen.Freeze();
        }

        /// <summary>设置竖线位置并显示。</summary>
        public void Update(double x, double top, double bottom)
        {
            _x = x; _top = top; _bottom = bottom;
            _visible = true;
            InvalidateVisual();
        }

        /// <summary>隐藏指示线。</summary>
        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (!_visible) return;

            var p1 = new Point(_x, _top);
            var p2 = new Point(_x, _bottom);
            dc.DrawLine(_pen, p1, p2);

            // 两端小三角, 更醒目(可选)
            double s = 4;
            dc.DrawGeometry(_pen.Brush, null, MakeTriangle(_x, _top, s, true));
            dc.DrawGeometry(_pen.Brush, null, MakeTriangle(_x, _bottom, s, false));
        }

        private static Geometry MakeTriangle(double x, double y, double s, bool down)
        {
            var fig = new PathFigure { StartPoint = new Point(x - s, y + (down ? -s : s)) };
            fig.Segments.Add(new LineSegment(new Point(x + s, y + (down ? -s : s)), true));
            fig.Segments.Add(new LineSegment(new Point(x, y), true));
            fig.IsClosed = true;
            var g = new PathGeometry();
            g.Figures.Add(fig);
            g.Freeze();
            return g;
        }

        private static Brush TryFindAccentBrush()
        {
            // 尝试用主题强调色, 取不到就用默认蓝
            if (Application.Current?.TryFindResource("Jui.Accent") is Brush b)
                return b;
            var fallback = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            fallback.Freeze();
            return fallback;
        }
    }
}
