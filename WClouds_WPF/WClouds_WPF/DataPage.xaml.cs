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
        private string? selectedFileName;

        public DataPage()
        {
            InitializeComponent();
            LoadFiles();
        }

        // KI Start | Prompt: Methode um folderid zu bekommen
        private int? GetSelectedFolderId()
        {
            if (FileTree.SelectedItem is TreeViewItem item)
            {
                if (item.Tag is int folderId) return folderId;
                if (item.Tag is (int, string)) return null;
            }
            return null;
        }


        public async Task LoadFiles()
        {
            SetStatus("Lade Dateien…");
            try
            {
                SavedDirectory? root = await storageService.GetRootDirectory(App.CurrentUserId);
                if (root == null)
                {
                    SetStatus("Keine Dateien gefunden.");
                    return;
                }

                FileTree.Items.Clear();
                FileTree.Items.Add(BuildTreeItem(root));
            }
            catch
            {
                SetStatus("Keine Dateien gefunden.");
                
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

            string path = dialog.FileName;
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);

            // KI Start| Prompt: Mach GUI schöner
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


                await storageService.UploadFile(file, App.CurrentUserId, GetSelectedFolderId());

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
            // KI Ende
        }

        
        private async void DownloadFile_Click(object sender, RoutedEventArgs e)
        {


            if (selectedFileId == null) return;


            var dialog = new SaveFileDialog
            {
                Title  = "Datei speichern unter…",
                Filter = "Alle Dateien (*.*)|*.*",
                FileName = selectedFileName
            };

            if (dialog.ShowDialog() != true) return;

            // KI Start | Prompt: Ich brauch die Implementierung vom Download Button
            SetStatus("Lade herunter…");
            DownloadBtn.IsEnabled = false;

            try
            {

                byte[]? decrypted = await storageService.DownloadFile(selectedFileId.Value);

                if (decrypted == null)
                {
                    SetStatus("Download fehlgeschlagen.");
                    return;
                }
                
                await File.WriteAllBytesAsync(dialog.FileName, decrypted);
                SetStatus($"Datei gespeichert unter {dialog.FileName}");
            }
            catch (Exception ex)
            {
                SetStatus("Download fehlgeschlagen.");
                MessageBox.Show($"Fehler beim Herunterladen: {ex.Message}");
            }
            finally
            {
                DownloadBtn.IsEnabled = selectedFileId != null;
            }
            // KI Ende
        }

        // KI Start | Prompt: Mach GUI schöner
        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is (int fileId, string fileName))
            {
                selectedFileId = fileId;
                selectedFileName = fileName;
                DownloadBtn.IsEnabled = true;
            }
            else
            {
                selectedFileId = null;
                selectedFileName = null;
                DownloadBtn.IsEnabled = false;
            }
        }
        // KI Ende


        private TreeViewItem BuildTreeItem(SavedDirectory directory)
        {
            var folderItem = new TreeViewItem
            {
                Header     = BuildHeader(GetFolderIcon(directory), directory.Name ?? "Root"),
                Tag = directory.ID
            };

            foreach (SavedDirectory subDir in directory.SubDirectories)
                folderItem.Items.Add(BuildTreeItem(subDir));

            foreach (SavedFile file in directory.Content)
            {
                string fullName = $"{file.FileName}{file.Extension}";
                var fileItem = new TreeViewItem
                {
                    Header = BuildHeader(GetFileIcon(file.Extension), fullName),
                    Tag = (file.ID, fullName)
                };
                folderItem.Items.Add(fileItem);
            }

            return folderItem;
        }

        // KI Start | Prompt: Mach GUI schöner
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

        private static string GetFileIcon(string? ext) => ("." + (ext ?? "").ToLower().TrimStart('.')) switch
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
        // KI Ende

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            Webservice.SetApiKey(string.Empty);
            App.CurrentUserId = 0;
            NavigationService.Navigate(new SignInPage());
        }
        
    }
}
