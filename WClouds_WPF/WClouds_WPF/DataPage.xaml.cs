using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    // Mit KI Design gemacht
    public partial class DataPage : Page
    {
        private readonly StorageService storageService = new StorageService();

        private int? selectedFileId = null;

        public DataPage()
        {
            InitializeComponent();
            LoadFiles();
        }


        public async Task LoadFiles()
        {
            SetStatus("Lade Dateien…");
            try
            {
                SavedDirectory? root = await storageService.GetDirectory(App.CurrentUserId);
                if (root == null)
                {
                    SetStatus("Keine Dateien gefunden.");
                    return;
                }

                FileTree.Items.Clear();
                FileTree.Items.Add(BuildTreeItem(root));
                SetStatus("Bereit.");
            }
            catch
            {
                SetStatus("⚠ Fehler beim Laden.");
                MessageBox.Show("Dateien konnten nicht geladen werden.");
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            selectedFileId = null;
            DownloadBtn.IsEnabled = false;
            await LoadFiles();
        }


        private async void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Datei zum Hochladen auswählen",
                Filter = "Alle Dateien (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            string path      = dialog.FileName;
            string fileName  = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);   // e.g. ".png"

            SetStatus($"Lade hoch: {fileName}{extension}…");
            UploadFileBtn.IsEnabled = false;

            try
            {
                byte[] rawBytes = await File.ReadAllBytesAsync(path);

                var file = new SavedFile
                {
                    FileName  = fileName,
                    Extension = extension,
                    Content   = rawBytes
                };


                await storageService.UploadFile(file, App.CurrentUserId);

                SetStatus($"✔ {fileName}{extension} erfolgreich hochgeladen.");
                await LoadFiles();
            }
            catch (Exception ex)
            {
                SetStatus("⚠ Upload fehlgeschlagen.");
                MessageBox.Show($"Fehler beim Hochladen:\n{ex.Message}");
            }
            finally
            {
                UploadFileBtn.IsEnabled = true;
            }
        }


        private async void DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFileId == null) return;


            var dialog = new SaveFileDialog
            {
                Title  = "Datei speichern unter…",
                Filter = "Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            SetStatus("Lade herunter…");
            DownloadBtn.IsEnabled = false;

            try
            {

                byte[]? decrypted = await storageService.DownloadFile(selectedFileId.Value);

                if (decrypted == null)
                {
                    SetStatus("⚠ Download fehlgeschlagen.");
                    return;
                }

                await File.WriteAllBytesAsync(dialog.FileName, decrypted);
                SetStatus($"✔ Datei gespeichert unter {dialog.FileName}");
            }
            catch (Exception ex)
            {
                SetStatus("⚠ Download fehlgeschlagen.");
                MessageBox.Show($"Fehler beim Herunterladen:\n{ex.Message}");
            }
            finally
            {
                DownloadBtn.IsEnabled = selectedFileId != null;
            }
        }


        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is int fileId)
            {
                selectedFileId = fileId;
                DownloadBtn.IsEnabled = true;
            }
            else
            {
                selectedFileId = null;
                DownloadBtn.IsEnabled = false;
            }
        }


        private TreeViewItem BuildTreeItem(SavedDirectory directory)
        {
            var folderItem = new TreeViewItem
            {
                Header     = BuildHeader(GetFolderIcon(directory), directory.Name ?? "Root"),
                IsExpanded = true

            };

            foreach (SavedDirectory subDir in directory.SubDirectories)
                folderItem.Items.Add(BuildTreeItem(subDir));

            foreach (SavedFile file in directory.Content)
            {
                string fullName = $"{file.FileName}{file.Extension}";
                var fileItem = new TreeViewItem
                {
                    Header = BuildHeader(GetFileIcon(file.Extension), fullName),
                    Tag    = file.ID  
                };
                folderItem.Items.Add(fileItem);
            }

            return folderItem;
        }

        private StackPanel BuildHeader(string icon, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text              = icon,
                FontSize          = 14,
                Margin            = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text              = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0F0"))
            });
            return sp;
        }

        private static string GetFolderIcon(SavedDirectory dir) =>
            (dir.Name == null || dir.Name == "Root") ? "☁" : "📁";

        private static string GetFileIcon(string? ext) => (ext ?? "").ToLower() switch
        {
            ".pdf"                                              => "📄",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp"    => "🖼️",
            ".mp3" or ".wav" or ".flac"                         => "🎵",
            ".mp4" or ".mov" or ".avi"                          => "🎬",
            ".zip" or ".rar" or ".7z"                           => "🗜️",
            ".exe" or ".msi"                                    => "⚙️",
            ".txt"                                              => "📝",
            ".cs" or ".py" or ".js" or ".ts" or ".cpp"         => "💻",
            ".xls" or ".xlsx"                                   => "📊",
            ".doc" or ".docx"                                   => "📃",
            _                                                   => "📎"
        };



        private void SetStatus(string message) =>
            StatusText.Text = message;
    }
}
