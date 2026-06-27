using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace JUI.Controls
{
    /// <summary>插入位置指示器(横线版): 在 JuiList 上层画一条水平线。位置由外部设置。</summary>
    internal class InsertionAdornerH : Adorner
    {
        private double _y;          // 横线 Y
        private double _left;       // 左端 X
        private double _right;      // 右端 X
        private bool _visible;

        private readonly Pen _pen;

        public InsertionAdornerH(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
            var brush = TryFindAccentBrush();
            _pen = new Pen(brush, 2);
            _pen.Freeze();
        }

        public void Update(double y, double left, double right)
        {
            _y = y; _left = left; _right = right;
            _visible = true;
            InvalidateVisual();
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (!_visible) return;

            dc.DrawLine(_pen, new Point(_left, _y), new Point(_right, _y));

            // 两端小三角
            double s = 4;
            dc.DrawGeometry(_pen.Brush, null, MakeTriangle(_left, _y, s, true));
            dc.DrawGeometry(_pen.Brush, null, MakeTriangle(_right, _y, s, false));
        }

        private static Geometry MakeTriangle(double x, double y, double s, bool rightward)
        {
            // 指向横线的小三角(左端朝右, 右端朝左)
            double dir = rightward ? s : -s;
            var fig = new PathFigure { StartPoint = new Point(x - dir, y - s) };
            fig.Segments.Add(new LineSegment(new Point(x - dir, y + s), true));
            fig.Segments.Add(new LineSegment(new Point(x, y), true));
            fig.IsClosed = true;
            var g = new PathGeometry();
            g.Figures.Add(fig);
            g.Freeze();
            return g;
        }

        private static Brush TryFindAccentBrush()
        {
            if (Application.Current?.TryFindResource("Jui.Accent") is Brush b)
                return b;
            var fallback = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            fallback.Freeze();
            return fallback;
        }
    }
}
