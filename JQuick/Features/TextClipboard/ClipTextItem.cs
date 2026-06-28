using System;

namespace JQuick
{
    /// <summary>文本剪贴板的一个文本项。内容即数据, 直接持久化。</summary>
    public class ClipTextItem
    {
        public string Text { get; set; } = "";

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
    }
}
