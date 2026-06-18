using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class StorageService
    {
        // AI Agent: holt den eigenen gewrappten DEK fuer eine Datei und
        // entpackt ihn lokal mit dem eigenen Private Key - wird beim
        // Download, beim Overwrite (kein Re-Wrap noetig, gleicher DEK) und
        // beim Teilen gebraucht (ShareDialog muss den DEK kennen, um ihn
        // fuer den Empfaenger neu zu wrappen). Deshalb public statt privat.
        public async Task<byte[]> GetDek(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/{fileId}/key");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            string wrappedKey = doc.RootElement.GetProperty("wrapped_key").GetString()!;
            return EncryptionService.UnwrapKey(wrappedKey);
        }

        public async Task<SavedFile?> GetFile(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/info/{fileId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(body);
        }

        public async Task<SavedDirectory?> GetDirectory(int directoryId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/{directoryId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(body);
        }

        // KI Start | Prompt: Upload verschlüsselt die Datei bevor sie zum Server geschickt wird
        // AI Agent: pro Datei ein frischer DEK statt des alten globalen
        // Geraete-Keys; der DEK wird fuer den Owner selbst gewrappt
        // mitgeschickt, weil der Server den rohen DEK nie sehen darf.
        public async Task<SavedFile?> UploadFile(SavedFile curFile, int ownerId, int? folderId = null)
        {
            if (curFile.Content == null)
                throw new ArgumentException("File content is empty");

            byte[] dek = EncryptionService.GenerateDek();
            var (encryptedData, nonce) = EncryptionService.Encrypt(curFile.Content, dek);
            string wrappedKeyForOwner = EncryptionService.WrapKey(dek, EncryptionService.GetOwnPublicKeyBase64());

            // KI: Multipart-Form senden (encrypted file + nonce + wrapped key)
            using MultipartFormDataContent form = new MultipartFormDataContent();

            ByteArrayContent fileContent = new ByteArrayContent(encryptedData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            form.Add(fileContent, "uploaded_file", curFile.FileName ?? "file");

            form.Add(new StringContent(nonce), "nonce");
            form.Add(new StringContent($"{curFile.FileName}{curFile.Extension}"), "original_name");
            form.Add(new StringContent(wrappedKeyForOwner), "wrapped_key_for_owner");

            if (folderId.HasValue)
                form.Add(new StringContent(folderId.Value.ToString()), "folder_id");
            // KI Ende


            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/files/", form);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(body);
        }

        // KI Start | Prompt: Download holt verschlüsselte Datei + Nonce und entschlüsselt sie lokal
        // AI Agent: holt zuerst den eigenen gewrappten DEK (gated durch
        // require_file_access auf dem Server - read reicht), entpackt ihn
        // lokal, entschluesselt damit erst den Inhalt.
        public async Task<byte[]?> DownloadFile(int fileId)
        {
            byte[] dek = await GetDek(fileId);

            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/download/{fileId}");
            response.EnsureSuccessStatusCode();

            // Nonce aus dem Response-Header lesen
            string? nonce = null;
            if (response.Headers.TryGetValues("X-Nonce", out var values))
                nonce = System.Linq.Enumerable.FirstOrDefault(values);

            if (nonce == null)
                throw new Exception("Nonce missing in response – cannot decrypt");

            byte[] encryptedData = await response.Content.ReadAsByteArrayAsync();

            // Lokal entschlüsseln – Server hat die Datei nie im Klartext gesehen
            return EncryptionService.Decrypt(encryptedData, dek, nonce);
        }
        // KI Ende

        // AI Agent: neues Feature - Inhalt einer Datei ersetzen (braucht
        // can_write). Behaelt bewusst den GLEICHEN DEK (nur frischer Nonce)
        // - kein Re-Wrap fuer alle Berechtigten noetig, AES-GCM erlaubt das
        // sicher solange der Nonce pro Verschluesselung neu ist.
        public async Task OverwriteFile(int fileId, byte[] newContent)
        {
            byte[] dek = await GetDek(fileId);
            var (encryptedData, nonce) = EncryptionService.Encrypt(newContent, dek);

            using MultipartFormDataContent form = new MultipartFormDataContent();
            ByteArrayContent fileContent = new ByteArrayContent(encryptedData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            form.Add(fileContent, "uploaded_file", "file");
            form.Add(new StringContent(nonce), "nonce");

            HttpResponseMessage response = await Webservice.HttpClient.PutAsync($"/files/{fileId}", form);
            response.EnsureSuccessStatusCode();
        }


        // KI Start | Prompt: DownloadDirectory soll die ganze Ordnerstruktur mitnehmen
        public async Task DownloadDirectory(int folderId, string savePath)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/download/{folderId}");
            response.EnsureSuccessStatusCode();

            string folderName = folderId.ToString();
            if (response.Headers.TryGetValues("X-Folder-Name", out var vals))
                folderName = vals.First();

            byte[] zipBytes = await response.Content.ReadAsByteArrayAsync();
            using var zipStream = new System.IO.MemoryStream(zipBytes);
            using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

            string outputDir = System.IO.Path.Combine(savePath, folderName);
            Directory.CreateDirectory(outputDir);

            // Erst alle Nonces und gewrappten Keys einlesen
            // AI Agent: .key-Eintraege sind das Pendant zu den .nonce-
            // Eintraegen - ohne den gewrappten DEK kann nach dem Umbau auf
            // Envelope-Encryption nichts mehr entschluesselt werden.
            var nonces = new Dictionary<string, string>();
            var deks = new Dictionary<string, byte[]>();
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nonce"))
                {
                    using var s = entry.Open();
                    using var r = new System.IO.StreamReader(s);
                    string dataFileName = entry.FullName[..^6];
                    nonces[dataFileName] = await r.ReadToEndAsync();
                }
                else if (entry.FullName.EndsWith(".key"))
                {
                    using var s = entry.Open();
                    using var r = new System.IO.StreamReader(s);
                    string dataFileName = entry.FullName[..^4];
                    string wrappedKey = await r.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(wrappedKey))
                        deks[dataFileName] = EncryptionService.UnwrapKey(wrappedKey);
                }
            }

            // Dann Files entschlüsseln
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nonce") || entry.FullName.EndsWith(".key")) continue;
                if (!nonces.TryGetValue(entry.FullName, out string? nonce)) continue;
                if (!deks.TryGetValue(entry.FullName, out byte[]? dek)) continue;

                using var entryStream = entry.Open();
                using var ms = new System.IO.MemoryStream();
                await entryStream.CopyToAsync(ms);
                byte[] encrypted = ms.ToArray();

                byte[] decrypted = EncryptionService.Decrypt(encrypted, dek, nonce);

                string fullPath = System.IO.Path.Combine(outputDir, entry.FullName);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
                await File.WriteAllBytesAsync(fullPath, decrypted);
            }
        }
        // KI Ende

        // KI Start | Prompt: UploadDirectory soll die ganze Ordnerstruktur mitnehmen
        public async Task UploadDirectory(string absolutePath, int ownerId, int? parentFolderId = null)
        {
            string folderName = Path.GetFileName(absolutePath);

            // Ordner im Backend anlegen
            var body = new { name = folderName, owner_id = ownerId, parent_id = parentFolderId };
            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/directories/", body);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            SavedDirectory? created = JsonSerializer.Deserialize<SavedDirectory>(json);
            if (created == null) return;

            // Alle Files in diesem Ordner hochladen
            foreach (string filePath in Directory.GetFiles(absolutePath))
            {
                byte[] rawBytes = await File.ReadAllBytesAsync(filePath);
                var file = new SavedFile
                {
                    FileName = Path.GetFileNameWithoutExtension(filePath),
                    Extension = Path.GetExtension(filePath),
                    Content = rawBytes
                };
                await UploadFile(file, ownerId, created.ID);
            }

            // Rekursiv alle Unterordner
            foreach (string subDir in Directory.GetDirectories(absolutePath))
                await UploadDirectory(subDir, ownerId, created.ID);
        }
        // KI Ende

        public async Task<Info?> GetDirectoryInfos(int directoryId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/info/{directoryId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Info>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<Info?> GetFileInfos(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/info/{fileId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Info>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<List<HistoryEntry>> GetFileHistory(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/{fileId}/history");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<HistoryEntry>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        public async Task<byte[]?> DownloadHistoryBackup(int historyId, int fileId)
        {
            byte[] dek = await GetDek(fileId);

            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/history/{historyId}/download");
            response.EnsureSuccessStatusCode();

            string? nonce = null;
            if (response.Headers.TryGetValues("X-Nonce", out var values))
                nonce = System.Linq.Enumerable.FirstOrDefault(values);
            if (nonce == null)
                throw new Exception("Nonce fehlt in der Antwort");

            byte[] encryptedData = await response.Content.ReadAsByteArrayAsync();
            return EncryptionService.Decrypt(encryptedData, dek, nonce);
        }

        public async Task DeleteFile(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.DeleteAsync($"/files/{fileId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteDirectory(int directoryId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.DeleteAsync($"/directories/{directoryId}");
            response.EnsureSuccessStatusCode();
        }

        // KI Start | Prompt: Ich brauch noch für jeden User von Anfang an ein Root
        public async Task<SavedDirectory?> GetRootDirectory(int userId)
        {
            // Erst alle Files des Users holen um die Root-Folder-ID zu finden
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/root/{userId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(body);
        }
    }
}