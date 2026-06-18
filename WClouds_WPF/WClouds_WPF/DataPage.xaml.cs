using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    public partial class DataPage : Page
    {
        private readonly StorageService storageService = new StorageService();
        private readonly ShareService shareService = new ShareService();

        // Kompletter Ordnerbaum des Users, einmal geladen, damit beim
        // Navigieren im FolderTree nicht jedes Mal die ganze Struktur neu
        // vom Server geholt werden muss - nur die Info pro Eintrag wird
        // pro Ordnerwechsel frisch nachgeladen.
        private SavedDirectory? rootDirectory;
        private List<SharedFile>? sharedFilesCache;

        private int? selectedFileId = null;
        private string? selectedFileName;

        public DataPage()
        {
            InitializeComponent();
        }

        private int? GetSelectedFolderId()
        {
            Log.Logger.Debug("GetSelectedFolderId called");
            // AI Agent: Tag == -1 ist der Sentinel-Wert fuer den
            // "Geteilt mit mir"-Knoten (siehe BuildSharedTreeItem) -
            // kein echter Ordner. War dieser Knoten ausgewaehlt, wurde
            // -1 bisher als Ziel-Ordner-ID an Upload/UploadDirectory
            // durchgereicht -> Backend findet Ordner "-1" nicht -> 404
            // -> verwirrender Fehler-Popup beim Hoch-/Ordnerladen.
            if (FolderTree.SelectedItem is TreeViewItem item && item.Tag is int folderId && folderId != -1)
                return folderId;
            return null;
        }

        public async Task LoadFiles()
        {
            SetStatus("Lade Dateien…");
            Log.Logger.Information("Loading files for user {UserId}", App.CurrentUserId);
            try
            {
                FolderTree.Items.Clear();
                FileList.ItemsSource = null;

                rootDirectory = await storageService.GetRootDirectory(App.CurrentUserId);
                if (rootDirectory != null)
                {
                    FolderTree.Items.Add(BuildFolderTreeItem(rootDirectory));
                    await LoadFolderContents(rootDirectory);
                }

                sharedFilesCache = await shareService.GetSharedWithMe(App.CurrentUserId);
                if (sharedFilesCache != null && sharedFilesCache.Count > 0)
                    FolderTree.Items.Add(BuildSharedTreeItem(sharedFilesCache));

                if (FolderTree.Items.Count == 0)
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

        // Baut den Ordnerbaum (nur Ordner, keine Dateien mehr - die
        // stehen jetzt rechts in der Detailliste mit den Info-Spalten).
        private TreeViewItem BuildFolderTreeItem(SavedDirectory directory)
        {
            var folderItem = new TreeViewItem
            {
                Header = BuildHeader(GetFolderIcon(directory), directory.Name ?? "Root"),
                Tag = directory.ID
            };

            foreach (SavedDirectory subDir in directory.SubDirectories)
                folderItem.Items.Add(BuildFolderTreeItem(subDir));

            return folderItem;
        }

        // KI Start | Prompt: Bau mir die Treeview für die geteilten Dateien
        private TreeViewItem BuildSharedTreeItem(List<SharedFile> sharedFiles)
        {
            Log.Logger.Debug("BuildSharedTreeItem called with {Count} shared files", sharedFiles?.Count ?? 0);

            // Die einzelnen Dateien werden nicht mehr als Kind-Knoten im
            // Baum aufgehängt, sondern erst beim Auswaehlen dieses Knotens
            // in die rechte Detailliste geladen (siehe LoadSharedContents).
            return new TreeViewItem
            {
                Header = BuildHeader("🤝", "Geteilt mit mir"),
                Tag = -1   // sentinel: not a real folder ID
            };
        }
        // KI Ende

        private static SavedDirectory? FindDirectory(SavedDirectory? root, int id)
        {
            if (root == null) return null;
            if (root.ID == id) return root;

            foreach (var sub in root.SubDirectories)
            {
                var found = FindDirectory(sub, id);
                if (found != null) return found;
            }
            return null;
        }

        private async void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeViewItem item) return;

            if (item.Tag is int tag && tag == -1)
            {
                await LoadSharedContents();
                return;
            }

            if (item.Tag is int folderId)
            {
                SavedDirectory? dir = FindDirectory(rootDirectory, folderId);
                if (dir != null)
                    await LoadFolderContents(dir);
            }
        }

        // Füllt die rechte Liste mit dem Inhalt eines Ordners - pro
        // Unterordner/Datei wird Info (Name, Size, ChangedDate,
        // ChangedTime, Owner, ChangedUser) vom Server nachgeladen.
        private async Task LoadFolderContents(SavedDirectory directory)
        {
            SetStatus($"Lade Inhalt von \"{directory.Name ?? "Root"}\"…");

            var entries = new List<FileExplorerEntry>();

            foreach (SavedDirectory sub in directory.SubDirectories)
            {
                Info? info = await storageService.GetDirectoryInfos(sub.ID);
                if (info != null)
                    entries.Add(new FileExplorerEntry(info, sub.ID, isFolder: true));
            }

            foreach (SavedFile file in directory.Content)
            {
                Info? info = await storageService.GetFileInfos(file.ID);
                if (info != null)
                    entries.Add(new FileExplorerEntry(info, file.ID, isFolder: false));
            }

            FileList.ItemsSource = entries;
            UpdateButtonStates(null);
            SetStatus("Bereit.");
        }

        // Nutzt die in LoadFiles() bereits geladene Liste (sharedFilesCache)
        // - kein erneuter Server-Call fuer die Liste selbst noetig, nur die
        // Info pro Datei wird geholt (GetFileInfos braucht nur can_read,
        // das haben geteilte Dateien per Definition).
        private async Task LoadSharedContents()
        {
            SetStatus("Lade geteilte Dateien…");

            var entries = new List<FileExplorerEntry>();
            if (sharedFilesCache != null)
            {
                foreach (SharedFile file in sharedFilesCache)
                {
                    Info? info = await storageService.GetFileInfos(file.ID);
                    Info effective = info ?? new Info(null, null, 0, "", "", $"{file.FileName}{file.Extension}");

                    entries.Add(new FileExplorerEntry(
                        effective, file.ID, isFolder: false,
                        isShared: true, canRead: file.CanRead, canWrite: file.CanWrite));
                }
            }

            FileList.ItemsSource = entries;
            UpdateButtonStates(null);
            SetStatus(entries.Count == 0 ? "Keine geteilten Dateien." : "Bereit.");
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates(FileList.SelectedItem as FileExplorerEntry);
        }

        private void UpdateButtonStates(FileExplorerEntry? entry)
        {
            if (entry == null)
            {
                selectedFileId = null;
                selectedFileName = null;
                DownloadBtn.IsEnabled = false;
                ShareBtn.IsEnabled = false;
                OverwriteBtn.IsEnabled = false;
                DeleteBtn.IsEnabled = false;
                HistoryBtn.IsEnabled = false;
                return;
            }

            if (entry.IsFolder)
            {
                selectedFileId = null;
                selectedFileName = null;
                DownloadBtn.IsEnabled = true;
                ShareBtn.IsEnabled = false;
                OverwriteBtn.IsEnabled = false;
                DeleteBtn.IsEnabled = !entry.IsShared;
                HistoryBtn.IsEnabled = true;
                return;
            }

            selectedFileId = entry.Id;
            selectedFileName = entry.Name;
            DownloadBtn.IsEnabled = !entry.IsShared || entry.CanRead;
            ShareBtn.IsEnabled = !entry.IsShared;
            OverwriteBtn.IsEnabled = !entry.IsShared || entry.CanWrite;
            DeleteBtn.IsEnabled = !entry.IsShared;
            HistoryBtn.IsEnabled = !entry.IsShared || entry.CanRead;
        }

        // Doppelklick auf einen Ordner in der Liste -> im FolderTree
        // dorthin navigieren (löst FolderTree_SelectedItemChanged aus).
        private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileList.SelectedItem is not FileExplorerEntry entry || !entry.IsFolder) return;
            SelectFolderInTree(FolderTree.Items, entry.Id);
        }

        private bool SelectFolderInTree(ItemCollection items, int folderId)
        {
            foreach (var obj in items)
            {
                if (obj is not TreeViewItem item) continue;

                if (item.Tag is int id && id == folderId)
                {
                    item.IsSelected = true;
                    item.IsExpanded = true;
                    return true;
                }

                if (SelectFolderInTree(item.Items, folderId))
                {
                    item.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

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

            if (FileList.SelectedItem is FileExplorerEntry entry)
            {
                if (entry.IsFolder)
                {
                    await DownloadFolder(entry.Id);
                    return;
                }

                if (entry.IsShared && !entry.CanRead)
                {
                    SetStatus("⚠ Kein Lesezugriff auf diese Datei.");
                    return;
                }

                await DownloadSingleFile(entry.Id, entry.Name, entry.IsShared);
                return;
            }

            // Nichts in der Liste ausgewählt -> aktuell geöffneten Ordner herunterladen
            if (FolderTree.SelectedItem is TreeViewItem folderItem && folderItem.Tag is int folderId && folderId != -1)
                await DownloadFolder(folderId);
        }

        private async Task DownloadSingleFile(int fileId, string fileName, bool isShared)
        {
            Log.Logger.Debug(isShared ? "Downloading shared file {FileId}" : "Downloading own file {FileId}", fileId);
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
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Download failed for file {FileId}", fileId);
                SetStatus("⚠ Download fehlgeschlagen.");
                MessageBox.Show(ex.Message);
            }
            finally { DownloadBtn.IsEnabled = true; }
        }

        private async Task DownloadFolder(int folderId)
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
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Download folder failed for {FolderId}", folderId);
                SetStatus("⚠ Download fehlgeschlagen.");
                MessageBox.Show(ex.Message);
            }
            finally { DownloadBtn.IsEnabled = true; }
        }
        // KI Ende

        // AI Agent: neues Feature - Inhalt einer Datei ersetzen, fuer
        // eigene Dateien immer moeglich, fuer geteilte nur mit can_write
        // (siehe Gating in UpdateButtonStates).
        private async void OverwriteFile_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFileId == null) return;

            var dialog = new OpenFileDialog
            {
                Title = "Neue Version auswählen",
                Filter = "Alle Dateien (*.*)|*.*",
                Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;

            SetStatus($"Überschreibe \"{selectedFileName}\"…");
            OverwriteBtn.IsEnabled = false;
            try
            {
                byte[] newContent = await File.ReadAllBytesAsync(dialog.FileName);
                await storageService.OverwriteFile(selectedFileId.Value, newContent);
                Log.Logger.Information("Overwrote file {FileId} with {Path}", selectedFileId, dialog.FileName);
                SetStatus($"✔ \"{selectedFileName}\" überschrieben.");
                await LoadFiles();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Overwrite failed for file {FileId}", selectedFileId);
                SetStatus("⚠ Überschreiben fehlgeschlagen.");
                MessageBox.Show($"Fehler beim Überschreiben:\n{ex.Message}");
            }
            finally { OverwriteBtn.IsEnabled = true; }
        }

        private void HistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileExplorerEntry entry) return;

            var dialog = new HistoryDialog(entry.Id, entry.Name, entry.IsFolder)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }

        private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not FileExplorerEntry entry) return;

            string label = entry.IsFolder ? $"den Ordner \"{entry.Name}\"" : $"die Datei \"{entry.Name}\"";
            string warning = entry.IsFolder ? "\nAlle Inhalte werden unwiderruflich gelöscht." : "";
            var result = MessageBox.Show(
                $"Möchtest du {label} wirklich löschen?{warning}",
                "Löschen bestätigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            SetStatus($"Lösche \"{entry.Name}\"…");
            DeleteBtn.IsEnabled = false;
            try
            {
                if (entry.IsFolder)
                    await storageService.DeleteDirectory(entry.Id);
                else
                    await storageService.DeleteFile(entry.Id);

                Log.Logger.Information("Deleted {Type} {Id} by user {UserId}",
                    entry.IsFolder ? "folder" : "file", entry.Id, App.CurrentUserId);
                SetStatus($"✔ \"{entry.Name}\" gelöscht.");
                await LoadFiles();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Delete failed for {Id}", entry.Id);
                SetStatus("⚠ Löschen fehlgeschlagen.");
                MessageBox.Show($"Fehler beim Löschen:\n{ex.Message}");
                DeleteBtn.IsEnabled = true;
            }
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

        public static string GetFileIcon(string? ext) => ("." + (ext ?? "").ToLower().TrimStart('.')) switch
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
            rootDirectory = null;
            FileList.ItemsSource = null;
            NavigationService.Navigate(new SignInPage());
        }
    }

    public record FileExplorerEntry : Info
    {
        public int Id { get; }
        public bool IsFolder { get; }

        // KI Start | Prompt: Wie kann ich erkennen ob die ausgewählte Datei eine eigene Datei ist oder eine geteilte Datei?
        // (Frueher als IsOwnFile() ueber den Parent-Knoten der TreeView
        // geraten - jetzt direkt als Flag gesetzt, wenn der Eintrag gebaut
        // wird: eigene Dateien/Ordner -> IsShared = false in
        // LoadFolderContents, geteilte Dateien -> IsShared = true in
        // LoadSharedContents.)
        public bool IsShared { get; }
        // KI Ende

        public bool CanRead { get; }
        public bool CanWrite { get; }

        public FileExplorerEntry(Info info, int id, bool isFolder,
            bool isShared = false, bool canRead = true, bool canWrite = true)
            : base(info.ChangedDate, info.ChangedTime, info.Size, info.ChangedUser, info.Owner, info.Name)
        {
            Id = id;
            IsFolder = isFolder;
            IsShared = isShared;
            CanRead = canRead;
            CanWrite = canWrite;
        }

        public string Icon => IsFolder ? "📁" : DataPage.GetFileIcon(Path.GetExtension(Name));

        public string SizeDisplay => $"{Size:N3} MB";

        public string ChangedDisplay =>
            ChangedDate != null ? $"{ChangedDate} {ChangedTime}" : "—";

        // Schloss-Symbol nur bei geteilten Dateien ohne Schreibrecht
        public Visibility LockVisibility =>
            (IsShared && !CanWrite) ? Visibility.Visible : Visibility.Collapsed;
    }
}