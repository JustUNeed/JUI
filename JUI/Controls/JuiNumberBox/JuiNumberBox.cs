using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JUI.Controls
{
    /// <summary>
    /// JUI 数值框: 显示态为 "Label .... 数值+Unit", 点击进入编辑(复用 JuiTextBox);
    /// 回车 / 失焦提交并按 [Minimum, Maximum] 夹取, Esc 取消; 显示态左右拖动可调值。
    /// </summary>
    [TemplatePart(Name = PartTextBox, Type = typeof(JuiTextBox))]
    public class JuiNumberBox : Control
    {
        public const string PartTextBox = "PART_TextBox";

        private JuiTextBox? _textBox;
        private bool _dragging;
        private bool _dragMoved;
        private Point _dragStart;
        private double _dragStartValue;

        static JuiNumberBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiNumberBox), new FrameworkPropertyMetadata(typeof(JuiNumberBox)));
        }

        // ================= 依赖属性 =================

        /// <summary>当前值(始终落在 [Minimum, Maximum] 内)。</summary>
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(JuiNumberBox),
                new FrameworkPropertyMetadata(0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged, CoerceValue));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(JuiNumberBox),
                new PropertyMetadata(double.MinValue, OnRangeChanged));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(JuiNumberBox),
                new PropertyMetadata(double.MaxValue, OnRangeChanged));

        /// <summary>每次拖动 / 步进的增量。</summary>
        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(double), typeof(JuiNumberBox),
                new PropertyMetadata(1.0));

        /// <summary>小数位数(用于显示与提交时的取整)。</summary>
        public int Decimals
        {
            get => (int)GetValue(DecimalsProperty);
            set => SetValue(DecimalsProperty, value);
        }
        public static readonly DependencyProperty DecimalsProperty =
            DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(JuiNumberBox),
                new PropertyMetadata(0, OnDisplayAffectingChanged));

        /// <summary>左侧标签文字。</summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(JuiNumberBox),
                new PropertyMetadata(string.Empty));

        /// <summary>数值后的单位(如 "px"、"%")。</summary>
        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(JuiNumberBox),
                new PropertyMetadata(string.Empty, OnDisplayAffectingChanged));

        /// <summary>圆角半径。</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(JuiNumberBox), new PropertyMetadata(new CornerRadius(6)));

        // 只读: 显示文本("数值+单位")
        private static readonly DependencyPropertyKey ValueDisplayTextKey =
            DependencyProperty.RegisterReadOnly(nameof(ValueDisplayText), typeof(string),
                typeof(JuiNumberBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ValueDisplayTextProperty =
            ValueDisplayTextKey.DependencyProperty;
        /// <summary>显示态文本: 数值按 Decimals 格式化 + Unit。</summary>
        public string ValueDisplayText
        {
            get => (string)GetValue(ValueDisplayTextProperty);
            private set => SetValue(ValueDisplayTextKey, value);
        }

        // 只读: 是否处于编辑态(供模板触发器切换显示 / 编辑层)
        private static readonly DependencyPropertyKey IsEditingKey =
            DependencyProperty.RegisterReadOnly(nameof(IsEditing), typeof(bool),
                typeof(JuiNumberBox), new PropertyMetadata(false));
        public static readonly DependencyProperty IsEditingProperty =
            IsEditingKey.DependencyProperty;
        /// <summary>当前是否处于编辑态。</summary>
        public bool IsEditing
        {
            get => (bool)GetValue(IsEditingProperty);
            private set => SetValue(IsEditingKey, value);
        }

        /// <summary>值变化事件。</summary>
        public event EventHandler<double>? ValueChanged;

        // ================= 回调 =================

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var box = (JuiNumberBox)d;
            double v = (double)baseValue;
            if (v < box.Minimum) v = box.Minimum;
            if (v > box.Maximum) v = box.Maximum;
            if (box.Decimals >= 0) v = Math.Round(v, box.Decimals, MidpointRounding.AwayFromZero);
            return v;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (JuiNumberBox)d;
            box.UpdateDisplayText();
            box.ValueChanged?.Invoke(box, (double)e.NewValue);
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => d.CoerceValue(ValueProperty);

        private static void OnDisplayAffectingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (JuiNumberBox)d;
            box.CoerceValue(ValueProperty);   // Decimals 变化可能改变取整结果
            box.UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            string num = Value.ToString("F" + Math.Max(0, Decimals), CultureInfo.CurrentCulture);
            ValueDisplayText = string.IsNullOrEmpty(Unit) ? num : num + " " + Unit;
        }

        // ================= 模板 =================

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_textBox != null)
            {
                _textBox.LostKeyboardFocus -= OnTextBoxLostFocus;
                _textBox.PreviewKeyDown -= OnTextBoxKeyDown;
            }

            _textBox = GetTemplateChild(PartTextBox) as JuiTextBox;

            if (_textBox != null)
            {
                _textBox.LostKeyboardFocus += OnTextBoxLostFocus;
                _textBox.PreviewKeyDown += OnTextBoxKeyDown;
            }

            UpdateDisplayText();
        }

        // ================= 进入 / 退出编辑 =================

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_dragging)
            {
                _dragging = false;
                if (IsMouseCaptured) ReleaseMouseCapture();
            }

            // 没有发生拖动位移，视为一次点击 → 进入编辑
            if (!IsEditing && !_dragMoved)
                BeginEdit();

            _dragMoved = false;
        }

        private void BeginEdit()
        {
            if (_textBox == null) return;
            IsEditing = true;
            _textBox.Text = Value.ToString(CultureInfo.CurrentCulture);
            _textBox.Focus();
            _textBox.SelectAll();
        }

        private void CommitEdit()
        {
            if (!IsEditing) return;
            if (_textBox != null &&
                double.TryParse(_textBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double v))
            {
                Value = v;   // 由 CoerceValue 负责夹取与取整
            }
            IsEditing = false;
        }

        private void CancelEdit() => IsEditing = false;

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e) => CommitEdit();

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitEdit();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    CancelEdit();
                    e.Handled = true;
                    break;
            }
        }

        // ================= 拖动调值 =================

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (IsEditing) return;
            _dragging = true;
            _dragMoved = false;
            _dragStart = e.GetPosition(this);
            _dragStartValue = Value;
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            double dx = e.GetPosition(this).X - _dragStart.X;
            if (!_dragMoved && Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance)
                return;

            _dragMoved = true;
            int stepsMoved = (int)(dx / 4);   // 每 4px 一个 Step
            Value = _dragStartValue + stepsMoved * Step;
        }



        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (IsEditing) return;
            Value += (e.Delta > 0 ? Step : -Step);
            e.Handled = true;
        }
    }
}
