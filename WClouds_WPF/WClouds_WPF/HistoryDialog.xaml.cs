using Microsoft.Win32;
using Serilog;
using System;
using System.IO;
using System.Windows;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    public partial class HistoryDialog : Window
    {
        private readonly StorageService _storage = new StorageService();
        private readonly int _fileId;
        private readonly bool _isFolder;

        public HistoryDialog(int fileId, string name, bool isFolder)
        {
            InitializeComponent();
            _fileId = fileId;
            _isFolder = isFolder;

            TitleText.Text = $"Verlauf — {name}";
            SubText.Text = isFolder
                ? "Änderungsverlauf dieses Ordners (kein Download für Ordner)"
                : "Alle gespeicherten Versionen dieser Datei";

            Loaded += async (_, _) => await LoadHistory();
        }

        private async System.Threading.Tasks.Task LoadHistory()
        {
            try
            {
                var entries = await _storage.GetFileHistory(_fileId);
                HistoryList.ItemsSource = entries;
                StatusText.Text = entries.Count == 0
                    ? "Kein Verlauf vorhanden."
                    : $"{entries.Count} Einträge gefunden.";
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Failed to load history for file {FileId}", _fileId);
                StatusText.Text = "⚠ Verlauf konnte nicht geladen werden.";
            }
        }

        private async void DownloadBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            if (btn.Tag is not HistoryEntry entry) return;

            var dialog = new SaveFileDialog
            {
                Title = "Backup speichern unter…",
                Filter = "Alle Dateien (*.*)|*.*",
                FileName = $"backup_{entry.Date}"
            };
            if (dialog.ShowDialog() != true) return;

            StatusText.Text = "Lade Backup herunter…";
            btn.IsEnabled = false;
            try
            {
                byte[]? decrypted = await _storage.DownloadHistoryBackup(entry.HistoryId, _fileId);
                if (decrypted == null) { StatusText.Text = "⚠ Download fehlgeschlagen."; return; }
                await File.WriteAllBytesAsync(dialog.FileName, decrypted);
                Log.Logger.Information("Downloaded history backup {HistoryId} to {Path}", entry.HistoryId, dialog.FileName);
                StatusText.Text = $"✔ Backup gespeichert: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "History backup download failed for {HistoryId}", entry.HistoryId);
                StatusText.Text = "⚠ Download fehlgeschlagen.";
                MessageBox.Show($"Fehler:\n{ex.Message}");
            }
            finally { btn.IsEnabled = entry.HasBackup; }
        }
    }
}
