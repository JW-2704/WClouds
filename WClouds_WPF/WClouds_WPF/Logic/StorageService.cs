using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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

        public async Task<SavedDirectory?> UploadDirectory(string absolutePath)
        {
            string json = JsonSerializer.Serialize(new { path = absolutePath });
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/directories", content);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(body);
        }

        public async Task<string?> GetDirectoryInfos(int directoryId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/info/{directoryId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<string>(body);
        }

        public async Task<string?> GetFileInfos(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/info/{fileId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<string>(body);
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