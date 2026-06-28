using System.IO;

namespace JQuick
{
    /// <summary>收纳的一个文件夹项, 只保存路径与显示名(无图标、无文件数)。</summary>
    public class FolderItem
    {
        public string Path { get; }
        public string DisplayName { get; }

        public FolderItem(string path)
        {
            Path = path;
            // DirectoryInfo(path).Name 只是字符串解析, 不碰磁盘
            DisplayName = new DirectoryInfo(path).Name;
        }
    }
}
