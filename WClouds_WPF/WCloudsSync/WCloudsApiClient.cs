using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using WClouds_WPF.Logic;

namespace WCloudsSync
{
    /// <summary>
    /// Schlanker HTTP-Client, der nur die für WCloudsSync benötigten
    /// Backend-Endpunkte aufruft.  Alle Daten werden lokal entschlüsselt.
    /// </summary>
    public class WCloudsApiClient
    {
        private readonly HttpClient _http;
        private readonly int        _userId;

        public WCloudsApiClient(SessionData session)
        {
            _userId = session.UserId;
            _http   = new HttpClient { BaseAddress = new Uri(session.ApiUrl) };
            _http.DefaultRequestHeaders.Add("X-API-Key", session.ApiKey);

            EncryptionService.Initialize(_userId);
        }

        // ── Verzeichnis-Struktur ─────────────────────────────────────────────

        public async Task<RemoteDirectory?> GetRootDirectoryAsync()
        {
            var resp = await _http.GetAsync($"/directories/root/{_userId}");
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<RemoteDirectory>(
                await resp.Content.ReadAsStringAsync(),
                JsonOpts);
        }

        public async Task<RemoteDirectory?> GetDirectoryAsync(int id)
        {
            var resp = await _http.GetAsync($"/directories/{id}");
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<RemoteDirectory>(
                await resp.Content.ReadAsStringAsync(),
                JsonOpts);
        }

        public async Task<List<SharedFileDto>?> GetSharedWithMeAsync()
        {
            var resp = await _http.GetAsync($"/files/shared/{_userId}");
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<List<SharedFileDto>>(
                await resp.Content.ReadAsStringAsync(),
                JsonOpts);
        }

        // ── Datei-Download (entschlüsselt) ───────────────────────────────────

        public async Task<byte[]?> DownloadFileAsync(int fileId)
        {
            // 1. DEK holen und entpacken
            var keyResp = await _http.GetAsync($"/files/{fileId}/key");
            if (!keyResp.IsSuccessStatusCode) return null;

            using var keyDoc = JsonDocument.Parse(await keyResp.Content.ReadAsStringAsync());
            string wrappedKey = keyDoc.RootElement.GetProperty("wrapped_key").GetString()!;
            byte[] dek = EncryptionService.UnwrapKey(wrappedKey);

            // 2. Verschlüsselte Datei holen
            var fileResp = await _http.GetAsync($"/files/download/{fileId}");
            if (!fileResp.IsSuccessStatusCode) return null;

            string? nonce = null;
            if (fileResp.Headers.TryGetValues("X-Nonce", out var vals))
                foreach (var v in vals) { nonce = v; break; }
            if (nonce == null) return null;

            byte[] encrypted = await fileResp.Content.ReadAsByteArrayAsync();
            return EncryptionService.Decrypt(encrypted, dek, nonce);
        }

        // ── Datei-Größe ──────────────────────────────────────────────────────────

        /// <summary>Gibt die Dateigröße in MB zurück (Info.Size vom Backend).</summary>
        public async Task<double> GetFileSizeAsync(int fileId)
        {
            var resp = await _http.GetAsync($"/files/info/{fileId}");
            if (!resp.IsSuccessStatusCode) return 0;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("size", out var prop))
                return prop.GetDouble();
            return 0;
        }

        // ── Datei-Upload (verschlüsselt) ─────────────────────────────────────

        public async Task<bool> UploadFileAsync(int fileId, byte[] content)
        {
            // Bestehenden DEK laden (kein Re-Wrap nötig, gleicher DEK)
            var keyResp = await _http.GetAsync($"/files/{fileId}/key");
            if (!keyResp.IsSuccessStatusCode) return false;

            using var keyDoc = JsonDocument.Parse(await keyResp.Content.ReadAsStringAsync());
            string wrappedKey = keyDoc.RootElement.GetProperty("wrapped_key").GetString()!;
            byte[] dek = EncryptionService.UnwrapKey(wrappedKey);

            var (encryptedData, nonce) = EncryptionService.Encrypt(content, dek);

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(encryptedData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            form.Add(fileContent, "uploaded_file", "file");
            form.Add(new StringContent(nonce), "nonce");

            var resp = await _http.PutAsync($"/files/{fileId}", form);
            return resp.IsSuccessStatusCode;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class RemoteDirectory
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<RemoteDirectory> SubDirectories { get; set; } = new();
        public List<RemoteFile>      Content        { get; set; } = new();
    }

    public class RemoteFile
    {
        public int     Id        { get; set; }
        public string? FileName  { get; set; }
        public string? Extension { get; set; }
    }

    public class SharedFileDto
    {
        public int     Id        { get; set; }
        public string? FileName  { get; set; }
        public string? Extension { get; set; }
        public bool    CanRead   { get; set; }
        public bool    CanWrite  { get; set; }
    }
}
