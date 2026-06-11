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
        public async Task<SavedFile?> UploadFile(SavedFile curFile, int ownerId, int? folderId = null)
        {
            if (curFile.Content == null)
                throw new ArgumentException("File content is empty");

            // KI: Datei verschlüsseln – Server sieht nur Ciphertext
            var (encryptedData, nonce) = EncryptionService.Encrypt(curFile.Content);

            // KI: Multipart-Form senden (encrypted file + nonce + owner_id)
            using MultipartFormDataContent form = new MultipartFormDataContent();

            ByteArrayContent fileContent = new ByteArrayContent(encryptedData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            form.Add(fileContent, "uploaded_file", curFile.FileName ?? "file");

            form.Add(new StringContent(nonce), "nonce");              
            form.Add(new StringContent(ownerId.ToString()), "owner_id");
            form.Add(new StringContent($"{curFile.FileName}{curFile.Extension}"), "original_name"); 

            if (folderId.HasValue)
                form.Add(new StringContent(folderId.Value.ToString()), "folder_id");
            // KI Ende


            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/files/", form);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(body);
        }

        // KI Start | Prompt: Download holt verschlüsselte Datei + Nonce und entschlüsselt sie lokal
        public async Task<byte[]?> DownloadFile(int fileId)
        {
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
            return EncryptionService.Decrypt(encryptedData, nonce);
        }
        // KI Ende

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

            // Erst alle Nonces einlesen
            var nonces = new Dictionary<string, string>();
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nonce"))
                {
                    using var s = entry.Open();
                    using var r = new System.IO.StreamReader(s);
                    string dataFileName = entry.FullName[..^6];
                    nonces[dataFileName] = await r.ReadToEndAsync();
                }
            }

            // Dann Files entschlüsseln
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nonce")) continue;
                if (!nonces.TryGetValue(entry.FullName, out string? nonce)) continue;

                using var entryStream = entry.Open();
                using var ms = new System.IO.MemoryStream();
                await entryStream.CopyToAsync(ms);
                byte[] encrypted = ms.ToArray();

                System.Diagnostics.Debug.WriteLine($"File: {entry.FullName}");
                System.Diagnostics.Debug.WriteLine($"Nonce from dict: {nonce}");
                System.Diagnostics.Debug.WriteLine($"Encrypted length: {encrypted.Length}");

                byte[] decrypted = EncryptionService.Decrypt(encrypted, nonce);

                string fullPath = System.IO.Path.Combine(outputDir, entry.FullName);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
                await File.WriteAllBytesAsync(fullPath, decrypted);
            }
        }

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