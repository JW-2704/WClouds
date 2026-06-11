using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class ShareService
    {
        // AI Prompt: Please implement the missing Share Service Requests
        public async Task ShareFile(List<int> MemberIDs, bool CanRead, bool CanWrite, int FileID)
        {
            var shareRequest = new
            {
                fileId = FileID,
                memberIds = MemberIDs,
                canRead = CanRead,
                canWrite = CanWrite
            };

            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/share/file", shareRequest);
            response.EnsureSuccessStatusCode();
        }

        public async Task RevokeAccess(int FileID, int MemberID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.DeleteAsync($"/share/file/{FileID}/member/{MemberID}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<FileAccess>?> GetFileMembers(int FileID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/share/file/{FileID}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FileAccess>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }

    public class FileAccess
    {
        public int MemberId { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
    }
}