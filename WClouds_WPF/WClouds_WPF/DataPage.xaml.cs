
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WClouds_WPF.Logic;


// Prompt:
// Bitte verschönere diese gesammte seite zb
// bei ordnern ein ordner symbol bei files das zugehörige icon etc

namespace WClouds_WPF
{
    public partial class DataPage : Page
    {
        private StorageService storageService = new StorageService();

        public DataPage()
        {
            InitializeComponent();
            LoadFiles();
        }

        public async Task LoadFiles()
        {
            try
            {
                SavedDirectory? root = await storageService.GetDirectory(App.CurrentUserId);
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
                Header = BuildHeader(GetFolderIcon(directory), directory.Name ?? "Root"),
                IsExpanded = true
            };

            foreach (SavedDirectory subDir in directory.SubDirectories)
                folderItem.Items.Add(BuildTreeItem(subDir));

            foreach (SavedFile file in directory.Content)
            {
                string fullName = $"{file.FileName}{file.Extension}";
                folderItem.Items.Add(new TreeViewItem
                {
                    Header = BuildHeader(GetFileIcon(file.Extension), fullName)
                });
            }

            return folderItem;
        }

        // Icon + Label nebeneinander
        private StackPanel BuildHeader(string icon, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0F0"))
            });
            return sp;
        }

        private string GetFolderIcon(SavedDirectory dir)
        {
            // Root-Ordner bekommt Cloud-Icon
            if (dir.Name == null || dir.Name == "Root") return "☁";
            return "📁";
        }

        private string GetFileIcon(string? ext) => (ext ?? "").ToLower() switch
        {
            ".pdf" => "📄",
            ".jpg" or ".jpeg" or ".png"
                   or ".gif" or ".bmp" => "🖼️",
            ".mp3" or ".wav" or ".flac" => "🎵",
            ".mp4" or ".mov" or ".avi" => "🎬",
            ".zip" or ".rar" or ".7z" => "🗜️",
            ".exe" or ".msi" => "⚙️",
            ".txt" => "📝",
            ".cs" or ".py" or ".js"
                  or ".ts" or ".cpp" => "💻",
            ".xls" or ".xlsx" => "📊",
            ".doc" or ".docx" => "📃",
            _ => "📎"
        };
    }
}