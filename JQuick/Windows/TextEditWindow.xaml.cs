using JUI.Controls;
using System;
using System.Windows;

namespace JQuick
{
    /// <summary>
    /// 文本项的多行编辑窗口。打开时载入指定项的文本, 关闭时把编辑结果写回该项并持久化。
    /// 直接持有目标 ClipTextItem 引用, 故无需关心它在列表里的索引——改的就是原对象。
    /// 关闭即保存: 通过 Saved 回调把"内容已变更"通知给列表控件去落盘。
    /// </summary>
    public partial class TextEditWindow : JuiWindow
    {
        private readonly ClipTextItem _item;

        /// <summary>文本被修改并保存后回调(用于触发持久化)。仅在内容确有变化时调用。</summary>
        public Action? Saved { get; set; }

        private bool _saved;          // 防止"保存按钮"与"关闭兜底"重复保存
        private readonly string _original;

        public TextEditWindow(ClipTextItem item)
        {
            InitializeComponent();
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _original = _item.Text ?? "";
            Editor.Text = _original;
            Editor.Focus();
            Editor.CaretIndex = Editor.Text.Length;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Commit();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 关闭兜底: 用户直接点 X 关闭也保存(关闭即保存)
            Commit();
            base.OnClosed(e);
        }

        /// <summary>把编辑框内容写回数据项; 仅在确有变化时触发持久化, 避免无谓写盘。</summary>
        private void Commit()
        {
            if (_saved) return;
            _saved = true;

            string newText = Editor.Text ?? "";
            if (newText == _original) return;   // 无改动, 不写回也不落盘

            _item.Text = newText;               // 直接覆盖原对象, 索引天然正确
            Saved?.Invoke();                    // 通知列表持久化
        }
    }
}
