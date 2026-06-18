using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class ShareService
    {
        // AI Prompt: Please implement the missing Share Service Requests
        // AI Agent: memberIds:list<int> -> grants:[{memberId, wrappedKey}]
        // - jeder Empfaenger braucht einen individuell mit seinem Public
        // Key gewrappten DEK, ein gemeinsames ID-Array reicht nicht mehr.
        // ShareDialog teilt ohnehin nur an einen Empfaenger pro Durchlauf,
        // daher reicht ein einzelner Grant statt einer Liste.
        public async Task ShareFile(int MemberID, string WrappedKey, bool CanRead, bool CanWrite, int FileID)
        {
            var shareRequest = new
            {
                fileId = FileID,
                grants = new[] { new { memberId = MemberID, wrappedKey = WrappedKey } },
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
        public async Task<List<SharedFile>?> GetSharedWithMe(int UserID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/share/shared-with-me/{UserID}");
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<SharedFile>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }


    // KI Start | Prompt: Wieso klapp
    public class FileAccess
    {
        [JsonPropertyName("member_id")]
        public int MemberId { get; set; }

        [JsonPropertyName("can_read")]
        public bool CanRead { get; set; }

        [JsonPropertyName("can_write")]
        public bool CanWrite { get; set; }
    }
    public class SharedFile
    {
        [JsonPropertyName("ID")]
        public int ID { get; set; }

        [JsonPropertyName("FileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("Extension")]
        public string Extension { get; set; } = "";

        [JsonPropertyName("CanRead")]
        public bool CanRead { get; set; }

        [JsonPropertyName("CanWrite")]
        public bool CanWrite { get; set; }
    }
}