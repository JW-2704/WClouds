using System;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class Authenticator
    {
        // KI Start | Prompt: Es soll alles richtig gehashed werden beim registrieren und einloggen, damit es sicher ist
        private static string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }


        private static string EncodeKey(string rawKey)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(rawKey));
        }
        // KI Ende

        public async Task<string> Register(string email, string password, string storage_key)
        {
            string hashedPassword = HashPassword(password);
            string encodedKey = EncodeKey(storage_key);

            var response = await Webservice.HttpClient.PostAsJsonAsync("/user/register",new{email, password = hashedPassword, storage_plan_key = encodedKey});
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<LoginResponse> Login(string email, string password)
        {
            string hashedPassword = HashPassword(password);

            var response = await Webservice.HttpClient.PostAsJsonAsync("/user/login", new { email, password = hashedPassword });
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            LoginResponse loginResponse = JsonSerializer.Deserialize<LoginResponse>(body)!;

            Webservice.SetApiKey(loginResponse.session_key);
            return loginResponse;
        }
    }
}
