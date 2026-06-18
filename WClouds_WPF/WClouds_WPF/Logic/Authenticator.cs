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

        // AI Agent: Public-Key-Upload passiert NUR hier bei Register,
        // niemals bei Login (siehe EncryptionService.Initialize). Der
        // Keypair muss VOR dem Request existieren (Public Key ist Teil des
        // Bodys), die echte userId aber erst NACH der Server-Antwort
        // bekannt ist - deshalb zuerst unter einem temporären Bezeichner
        // erzeugen und nach Erfolg auf die echte userId umbenennen.
        public async Task<string> Register(string email, string password, string storage_key)
        {
            string hashedPassword = HashPassword(password);
            string encodedKey = EncodeKey(storage_key);

            string tempKeyId = Guid.NewGuid().ToString();
            string publicKey = EncryptionService.GenerateAndStoreNewKeypair(tempKeyId);

            try
            {
                var response = await Webservice.HttpClient.PostAsJsonAsync("/user/register", new
                {
                    email,
                    password = hashedPassword,
                    storage_plan_key = encodedKey,
                    public_key = publicKey
                });
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);
                int userId = doc.RootElement.GetProperty("id").GetInt32();
                EncryptionService.PersistKeypairForUser(tempKeyId, userId);

                return body;
            }
            catch
            {
                EncryptionService.DiscardTempKeypair(tempKeyId);
                throw;
            }
        }

        public async Task<LoginResponse> Login(string email, string password)
        {
            string hashedPassword = HashPassword(password);

            var response = await Webservice.HttpClient.PostAsJsonAsync("/user/login", new { email, password = hashedPassword });
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            LoginResponse loginResponse = JsonSerializer.Deserialize<LoginResponse>(body)!;

            Webservice.SetApiKey(loginResponse.session_key);
            // AI Agent: laedt NUR den lokal vorhandenen Private Key dieses
            // Accounts - generiert NIE einen neuen (siehe Begründung in
            // EncryptionService.Initialize).
            EncryptionService.Initialize(loginResponse.user_id);
            return loginResponse;
        }
    }
}
