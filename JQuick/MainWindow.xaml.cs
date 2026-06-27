using JUI.Theming;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JQuick
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Photo> Items { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            // 设置数据源(用 ObservableCollection)
            DataContext = this;   // 或你的 ViewModel


            // ↓ 这行之前漏了, 拖出文件必须有它
            Grid.FilePathSelector = item => (item as Photo)?.Path;





            // 外部拖入: 用户自己分流
            Grid.ExternalDropHandler = data =>
            {
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var paths = (string[])data.GetData(DataFormats.FileDrop);
                    return paths.Select(path => new Photo
                    {
                        Path = path,
                        Caption = System.IO.Path.GetFileName(path)
                    });
                }

                // 浏览器拖来的通常是 URL 文本
                if (data.GetDataPresent(DataFormats.UnicodeText))
                {
                    var url = ((string)data.GetData(DataFormats.UnicodeText)).Trim();
                    if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return new[] { new Photo { Path = url, Caption = "网络图片" } };
                }

                return null;
            };


            // 项点击
            Grid.ItemClick += (s, e) =>
            {
                if (e.ClickCount == 2) MessageBox.Show("shuangji");   // 双击打开
                else
                {
                    Photo item= (Photo)e.Item;
                    MessageBox.Show(item.Path);
                }
                                   // 单击选中
            };


            Grid.OnItemDroppedOnItem = (data, path, target) => MessageBox.Show("移入");
            // ↓↓↓ 初始化数据写在这里 ↓↓↓
            LoadInitialData();

        }


        private void LoadInitialData()
        {
            var dir = @"C:\Users\JUNPC\Desktop\葵";

            // 临时调试: 看看到底找到几个文件
            if (!System.IO.Directory.Exists(dir))
            {
                MessageBox.Show("文件夹不存在: " + dir);
                return;
            }
            var files = System.IO.Directory.GetFiles(dir, "*.jpeg");
            MessageBox.Show("找到文件数: " + files.Length);

            foreach (var file in files)
                Items.Add(new Photo { Path = file, Caption = System.IO.Path.GetFileName(file) });

            Grid.ItemsSource = Items;   // ← 加这一行
            MessageBox.Show("Items 数量: " + Items.Count + ", Grid.Items 数量: " + Grid.Items.Count);
        }



        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();   // 一键切换 + 自动保存
        }


        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var item = Grid.GetItemFromElement(sender as DependencyObject);
            if (item != null)
                Grid.RemoveItem(item);   // 内部会触发 OnContentChanged
        }

    }
}



public class Photo
{
    public string Path { get; set; } = "";
    public string Caption { get; set; } = "";

    private BitmapImage? _thumbnail;
    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumbnail == null && !string.IsNullOrEmpty(Path) && System.IO.File.Exists(Path))
                _thumbnail = LoadThumbnail(Path);
            return _thumbnail;
        }
    }

    private static BitmapImage LoadThumbnail(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.DecodePixelWidth = 200;                  // 解码成缩略图大小, 大幅省内存
        bmp.CacheOption = BitmapCacheOption.OnLoad;  // 立刻读进内存, 不锁定文件
        bmp.EndInit();
        bmp.Freeze();                                // 冻结, 可跨线程、更高效
        return bmp;
    }
}