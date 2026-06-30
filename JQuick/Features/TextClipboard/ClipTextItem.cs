using System;
using System.ComponentModel;

namespace JQuick
{
    /// <summary>文本剪贴板的一个文本项。内容即数据, 直接持久化。</summary>
    public class ClipTextItem : INotifyPropertyChanged
    {
        private string _text = "";
        public string Text
        {
            get => _text;
            set
            {
                if (_text == value) return;
                _text = value ?? "";
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(Preview));   // 文本变了, 预览跟着刷新
            }
        }

        /// <summary>单行预览(去掉换行、首尾空白, 过长截断), 用于列表显示。</summary>
        public string Preview
        {
            get
            {
                var t = (Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                return t.Length > 200 ? t.Substring(0, 200) + "…" : t;
            }
        }

        public ClipTextItem() { }
        public ClipTextItem(string text) { Text = text ?? ""; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
