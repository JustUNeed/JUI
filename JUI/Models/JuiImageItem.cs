using System.Windows.Media.Imaging;

namespace JUI
{
    /// <summary>
    /// 网格中的一个图片项。
    /// </summary>
    public class JuiImageItem
    {
        /// <summary>磁盘上的真实文件路径(拖出时用)。</summary>
        public string FilePath { get; set; } = "";

        /// <summary>显示用缩略图。</summary>
        public BitmapImage? Thumbnail { get; set; }

        /// <summary>显示名称(可选)。</summary>
        public string DisplayName { get; set; } = "";
    }
}
