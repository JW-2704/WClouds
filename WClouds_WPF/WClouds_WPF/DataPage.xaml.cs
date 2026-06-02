using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    /// <summary>
    /// Interaction logic for DataPage.xaml
    /// </summary>
    public partial class DataPage : Page
    {
        private StorageService storageService = new StorageService();

        public DataPage()
        {
            InitializeComponent();
            LoadFiles();
        }

        private async void LoadFiles()
        {
            try
            {
                SavedDirectory? root = await storageService.GetDirectory(1);
                if (root == null) return;

                FileTree.Items.Clear();
                FileTree.Items.Add(BuildTreeItem(root));
            }
            catch
            {
                MessageBox.Show("Fehler beim Laden");
            }
        }
        private TreeViewItem BuildTreeItem(SavedDirectory directory)
        {
            var folderItem = new TreeViewItem
            {
                Header = directory.Name ?? "Root"
            };

           
            foreach (SavedDirectory subDir in directory.SubDirectories)
            {
                folderItem.Items.Add(BuildTreeItem(subDir));
            }

            foreach (SavedFile file in directory.Content)
            {
                folderItem.Items.Add(new TreeViewItem
                {
                    Header = $"{file.FileName}{file.Extension}"
                });
            }

            return folderItem;
        }
    }
}

