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
        public async Task<SavedFile?> GetFile(int FileID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/{FileID}");
            response.EnsureSuccessStatusCode();
            string file = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(file);

        }


        public async Task<SavedDirectory?> GetDirectory(int DirectoryID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/directorys/{DirectoryID}");
            response.EnsureSuccessStatusCode();
            string directory = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(directory);
        }


        public async Task<SavedFile?> UploadFile(SavedFile cur_file)
        {
            string json = JsonSerializer.Serialize(cur_file);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/files", content);
            response.EnsureSuccessStatusCode();
            string file = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(file);
        }


        public async Task<SavedDirectory?> UploadDirectory(string absolutePath)
        {
            string json = JsonSerializer.Serialize(absolutePath);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await Webservice.HttpClient.PostAsync("/directorys", content);
            response.EnsureSuccessStatusCode();
            string directory = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedDirectory>(directory);
        }


        public async Task<string?> GetDirectoryInfos(int DirectoryID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"directorys/info/{DirectoryID}");
            response.EnsureSuccessStatusCode();
            string directory = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<string>(directory);
        }


        public async Task<string?> GetFileInfos(int FileID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/files/info/{FileID}");
            response.EnsureSuccessStatusCode();
            string file = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<string>(file);
        }
    }
}
