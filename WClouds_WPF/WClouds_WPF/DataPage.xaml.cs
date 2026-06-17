using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    public partial class DataPage : Page
    {
        private readonly StorageService storageService = new StorageService();
        private readonly ShareService shareService = new ShareService();

        private int? selectedFileId = null;
        private string? selectedFileName;

        public DataPage()
        {
            InitializeComponent();
        }

        private int? GetSelectedFolderId()
        {
            Log.Logger.Debug("GetSelectedFolderId called");
            if (FileTree.SelectedItem is TreeViewItem item)
            {
                if (item.Tag is int folderId) return folderId;
                if (item.Tag is (int, string)) return null;
                if (item.Tag is (int, string, bool, bool)) return null;
            }
            return null;
        }

        public async Task LoadFiles()
        {
            SetStatus("Lade Dateien…");
            Log.Logger.Information("Loading files for user {UserId}", App.CurrentUserId);
            try
            {
                FileTree.Items.Clear();
                SavedDirectory? root = await storageService.GetRootDirectory(App.CurrentUserId);
                if (root != null)
                    FileTree.Items.Add(BuildTreeItem(root));

                List<SharedFile>? sharedFiles = await shareService.GetSharedWithMe(App.CurrentUserId);
                if (sharedFiles != null && sharedFiles.Count > 0)
                    FileTree.Items.Add(BuildSharedTreeItem(sharedFiles));

                if (FileTree.Items.Count == 0)
                    SetStatus("Keine Dateien gefunden.");
                else
                    SetStatus("Bereit.");
            }
            catch
            {
                SetStatus("Keine Dateien gefunden.");
                Log.Logger.Error("Failed to load files for user {UserId}", App.CurrentUserId);
            }
        }

        // KI Start | Prompt: Bau mir die Treeview für die geteilten Dateien
        private TreeViewItem BuildSharedTreeItem(List<SharedFile> sharedFiles)
        {

            Log.Logger.Debug("BuildSharedTreeItem called with {Count} shared files", sharedFiles?.Count ?? 0);

            var sharedFolder = new TreeViewItem
            {
                Header = BuildHeader("🤝", "Geteilt mit mir"),
                Tag = -1   // sentinel: not a real folder ID
            };

            foreach (SharedFile file in sharedFiles)
            {
                string fullName = $"{file.FileName}{file.Extension}";
                string icon = GetFileIcon(file.Extension);
                string label = fullName + (file.CanWrite ? "" : " 🔒");

                var fileItem = new TreeViewItem
                {
                    Header = BuildHeader(icon, label),
                    Tag = (file.ID, fullName, file.CanRead, file.CanWrite)
                };
                sharedFolder.Items.Add(fileItem);
            }

            return sharedFolder;
        }
        // KI Ende

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Log.Logger.Information("Refresh clicked by user {UserId}", App.CurrentUserId);
            selectedFileId = null;
            DownloadBtn.IsEnabled = false;
            await LoadFiles();
        }

        // KI Start | Prompt: UploadFile_Click soll die Datei verschlüsseln bevor sie hochgeladen wird
        private async void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            Log.Logger.Information("UploadFile_Click triggered by user {UserId}", App.CurrentUserId);
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

            SetStatus($"Lade hoch: {fileName}{extension}…");
            UploadFileBtn.IsEnabled = false;

            try
            {
                byte[] rawBytes = await File.ReadAllBytesAsync(path);
                var file = new SavedFile { FileName = fileName, Extension = extension, Content = rawBytes };
                await storageService.UploadFile(file, App.CurrentUserId, GetSelectedFolderId());
                Log.Logger.Information("File uploaded: {FileName}{Ext} by user {UserId}", fileName, extension, App.CurrentUserId);
                SetStatus($"✔ {fileName}{extension} erfolgreich hochgeladen.");
                await LoadFiles();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Upload failed for {FileName}{Ext} by user {UserId}", fileName, extension, App.CurrentUserId);
                SetStatus("⚠ Upload fehlgeschlagen.");
                MessageBox.Show($"Fehler beim Hochladen:\n{ex.Message}");
            }
            finally { UploadFileBtn.IsEnabled = true; }
        }
        // KI Ende


        // KI Start | Prompt: DownloadFile_Click soll die Datei herunterladen und entschlüsseln und auch ganze Ordner herunterladen können
        private async void DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            Log.Logger.Information("DownloadFile_Click triggered by user {UserId}", App.CurrentUserId);
            if (FileTree.SelectedItem is not TreeViewItem item) return;

            if (item.Tag is (int sharedFileId, string sharedFileName, bool canRead, bool _))
            {
                Log.Logger.Debug("Downloading shared file {SharedFileId} (canRead={CanRead})", sharedFileId, canRead);
                if (!canRead)
                {
                    SetStatus("⚠ Kein Lesezugriff auf diese Datei.");
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Datei speichern unter…",
                    Filter = "Alle Dateien (*.*)|*.*",
                    FileName = sharedFileName
                };
                if (dialog.ShowDialog() != true) return;
                SetStatus("Lade herunter…");
                DownloadBtn.IsEnabled = false;
                try
                {
                    byte[]? decrypted = await storageService.DownloadFile(sharedFileId);
                    if (decrypted == null) { SetStatus("⚠ Download fehlgeschlagen."); return; }
                    await File.WriteAllBytesAsync(dialog.FileName, decrypted);
                    Log.Logger.Information("Downloaded shared file {SharedFileId} to {Path}", sharedFileId, dialog.FileName);
                    SetStatus($"✔ Datei gespeichert unter {dialog.FileName}");
                }
                catch (Exception ex) { Log.Logger.Error(ex, "Download failed for shared file {SharedFileId}", sharedFileId); SetStatus("⚠ Download fehlgeschlagen."); MessageBox.Show(ex.Message); }
                finally { DownloadBtn.IsEnabled = true; }
            }

            else if (item.Tag is (int fileId, string fileName))
            {
                Log.Logger.Debug("Downloading own file {FileId}", fileId);
                var dialog = new SaveFileDialog
                {
                    Title = "Datei speichern unter…",
                    Filter = "Alle Dateien (*.*)|*.*",
                    FileName = fileName
                };
                if (dialog.ShowDialog() != true) return;
                SetStatus("Lade herunter…");
                DownloadBtn.IsEnabled = false;
                try
                {
                    byte[]? decrypted = await storageService.DownloadFile(fileId);
                    if (decrypted == null) { SetStatus("⚠ Download fehlgeschlagen."); return; }
                    await File.WriteAllBytesAsync(dialog.FileName, decrypted);
                    Log.Logger.Information("Downloaded file {FileId} to {Path}", fileId, dialog.FileName);
                    SetStatus($"✔ Datei gespeichert unter {dialog.FileName}");
                }
                catch (Exception ex) { Log.Logger.Error(ex, "Download failed for file {FileId}", fileId); SetStatus("⚠ Download fehlgeschlagen."); MessageBox.Show(ex.Message); }
                finally { DownloadBtn.IsEnabled = true; }
            }
            else if (item.Tag is int folderId && folderId != -1)
            {
                Log.Logger.Debug("Downloading folder {FolderId}", folderId);
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Zielordner auswählen" };
                if (dialog.ShowDialog() != true) return;
                SetStatus("Lade Ordner herunter…");
                DownloadBtn.IsEnabled = false;
                try
                {
                    await storageService.DownloadDirectory(folderId, dialog.FolderName);
                    Log.Logger.Information("Downloaded folder {FolderId} to {Path}", folderId, dialog.FolderName);
                    SetStatus("✔ Ordner gespeichert.");
                }
                catch (Exception ex) { Log.Logger.Error(ex, "Download folder failed for {FolderId}", folderId); SetStatus("⚠ Download fehlgeschlagen."); MessageBox.Show(ex.Message); }
                finally { DownloadBtn.IsEnabled = true; }
            }
        }
        // KI Ende

        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item)
            {
                if (item.Tag is (int sharedFileId, string sharedFileName, bool canRead, bool canWrite))
                {
                    selectedFileId = sharedFileId;
                    selectedFileName = sharedFileName;
                    DownloadBtn.IsEnabled = canRead;
                    ShareBtn.IsEnabled = false;
                }

                else if (item.Tag is (int fileId, string fileName))
                {
                    selectedFileId = fileId;
                    selectedFileName = fileName;
                    DownloadBtn.IsEnabled = true;
                    ShareBtn.IsEnabled = IsOwnFile(fileId);
                }
                else if (item.Tag is int folderId && folderId != -1)
                {
                    selectedFileId = null;
                    selectedFileName = null;
                    DownloadBtn.IsEnabled = true;
                    ShareBtn.IsEnabled = false;
                }
                else
                {
                    selectedFileId = null;
                    selectedFileName = null;
                    DownloadBtn.IsEnabled = false;
                    ShareBtn.IsEnabled = false;
                }
            }
        }

        // KI Start | Prompt: Wie kann ich erkennen ob die ausgewählte Datei eine eigene Datei ist oder eine geteilte Datei?
        private bool IsOwnFile(int fileId)
        {
            if (FileTree.SelectedItem is not TreeViewItem selected) return false;
            var parent = selected.Parent as TreeViewItem;
            return parent?.Tag is not -1 || parent?.Tag is null;
        }
        // KI Ende

        private TreeViewItem BuildTreeItem(SavedDirectory directory)
        {
            var folderItem = new TreeViewItem
            {
                Header = BuildHeader(GetFolderIcon(directory), directory.Name ?? "Root"),
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


        // KI Start | Prompt: Ich will die Icons und Labels in der TreeView, wie mach ich das
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
        // KI Ende

        // KI Start | Prompt: Ich will die Icons für die Dateien und Ordner, wie mach ich das
        private static string GetFolderIcon(SavedDirectory dir) =>
            (dir.Name == null || dir.Name == "Root") ? "☁" : "📁";

        private static string GetFileIcon(string? ext) => ("." + (ext ?? "").ToLower().TrimStart('.')) switch
        {
            ".pdf" => "📄",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
            ".mp3" or ".wav" or ".flac" => "🎵",
            ".mp4" or ".mov" or ".avi" => "🎬",
            ".zip" or ".rar" or ".7z" => "🗜️",
            ".exe" or ".msi" => "⚙️",
            ".txt" => "📝",
            ".cs" or ".py" or ".js" or ".ts" or ".cpp" => "💻",
            ".xls" or ".xlsx" => "📊",
            ".doc" or ".docx" => "📃",
            _ => "📎"
        };
        // KI Ende

        private void SetStatus(string message) => StatusText.Text = message;


        // KI Start | Prompt: Wie würde das sharen aussehen im xaml.cs teil
        private void ShareBtn_Click(object sender, RoutedEventArgs e)
        {
            Log.Logger.Information("ShareBtn_Click invoked by user {UserId} for selectedFileId={SelectedFileId}", App.CurrentUserId, selectedFileId);
            if (selectedFileId == null)
            {
                Log.Logger.Warning("Share attempted without selection by user {UserId}", App.CurrentUserId);
                SetStatus("⚠ Bitte zuerst eine Datei auswählen.");
                return;
            }

            var dialog = new ShareDialog(selectedFileId.Value)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                Log.Logger.Information("File {SelectedFileId} (\"{SelectedFileName}\") shared by user {UserId}", selectedFileId, selectedFileName, App.CurrentUserId);
                SetStatus($"✔ \"{selectedFileName}\" erfolgreich geteilt.");
            }
        }
        // KI Ende

        private async void UploadFolder_Click(object sender, RoutedEventArgs e)
        {
            Log.Logger.Information("UploadFolder_Click invoked by user {UserId}", App.CurrentUserId);
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner zum Hochladen auswählen" };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
                
            int? parentFolderId = GetSelectedFolderId();
            SetStatus("Lade Ordner hoch…");
            UploadFileBtn.IsEnabled = false;

            try
            {
                await storageService.UploadDirectory(dialog.FolderName, App.CurrentUserId, parentFolderId);
                Log.Logger.Information("Folder uploaded from {Path} by user {UserId} into parentFolderId={Parent}", dialog.FolderName, App.CurrentUserId, parentFolderId);
                SetStatus("Ordner erfolgreich hochgeladen.");
                await LoadFiles();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "UploadFolder failed from {Path} by user {UserId}", dialog.FolderName, App.CurrentUserId);
                SetStatus("Upload fehlgeschlagen.");
                MessageBox.Show($"Fehler: {ex.Message}");
            }
            finally { UploadFileBtn.IsEnabled = true; }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            Webservice.SetApiKey(string.Empty);
            App.CurrentUserId = 0;
            NavigationService.Navigate(new SignInPage());
        }
    }
}