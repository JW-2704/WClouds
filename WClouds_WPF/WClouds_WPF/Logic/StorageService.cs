using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace WClouds_WPF.Logic
{
    public class StorageService
    {

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        public async Task<SavedFile?> GetFile(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/{fileId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(body, JsonOptions);
        }

        public async Task<SavedDirectory?> GetDirectory(int directoryId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/{directoryId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(body, JsonOptions);
        }

        public async Task<SavedFile?> UploadFile(SavedFile curFile)
        {
            string json = JsonSerializer.Serialize(curFile);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/files", content);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(body, JsonOptions);
        }

        public async Task<SavedDirectory?> UploadDirectory(string absolutePath)
        {
            string json = JsonSerializer.Serialize(new { path = absolutePath });
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/directories", content);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(body, JsonOptions);
        }

        public async Task<string?> GetDirectoryInfos(int directoryId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directories/info/{directoryId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<string>(body, JsonOptions);
        }

        public async Task<string?> GetFileInfos(int fileId)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/info/{fileId}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<string>(body, JsonOptions);
        }
    }
}
