using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public int Storage_Plan { get; set; }

        public async Task<User?> GetUser(int UserID)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/user/{UserID}");
            response.EnsureSuccessStatusCode();
            string details = await response.Content.ReadAsStringAsync();
            // AI Agent: Backend liefert lowercase JSON-Keys (id, email,
            // storage_plan), ohne PropertyNameCaseInsensitive blieben Id/
            // Email/Storage_Plan immer auf ihrem Default-Wert (0/null).
            return JsonSerializer.Deserialize<User>(details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task UpdateLogin(string email)
        {
            HttpResponseMessage response = await Webservice.HttpClient.PatchAsJsonAsync("/user/updatelogin", new { email, lastLogin = DateTime.UtcNow });
            response.EnsureSuccessStatusCode();
        }

        public async Task UpdateUsedStorage(int UserID, long usedBytes)
        {
            HttpResponseMessage response = await Webservice.HttpClient.PatchAsJsonAsync("/user/updateusedstorage", new { UserID, usedBytes });
            response.EnsureSuccessStatusCode();
        }
        public async Task<int?> GetUserIdByEmail(string email)
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/user/by-email/{Uri.EscapeDataString(email)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            User? user = JsonSerializer.Deserialize<User>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return user?.Id;
        }
    }
}
