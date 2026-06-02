using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class BackupService
    {
        public async Task<List<SavedDirectory>?> GetBackupFolder(int BackupDirectoryID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/backup/directory/{BackupDirectoryID}");
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<SavedDirectory>>(json);
        }

        public async Task<SavedFile?> GetBackupFile(int BackupFileID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/backup/file/{BackupFileID}");
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SavedFile>(json);
        }
    }       
}
