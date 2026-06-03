using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class Authenticator
    {
        public async Task<string> Register(string email, string password, string storage_key)
        {
            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/user/register", new { email, password, storage_plan_key = storage_key });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<LoginResponse> Login(string email, string password)
        {
            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/user/login", new { email, password });
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LoginResponse>(body)!;
        }
    }
}
