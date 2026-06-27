using JQuick;
using System.Windows;
namespace JQuick
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardGrid.GetItemFromElement(sender as DependencyObject) is Photo p)
                ClipboardGrid.Remove(p);
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderBox.GetItemFromElement(sender as DependencyObject) is FolderItem f)
                FolderBox.Remove(f);
        }
    }
}
