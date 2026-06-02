using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class Authenticator
    {

        public async Task<string> Login(string email, string password)
        {
            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/authenticate/register", new { email, password });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> Register(string email, string password, string storage_key)
        {
            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/authenticate/login", new { email, password, storage_key });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        private void UpdateLogin()
        {

        }
        private void UpdateUsedStorage()
        {

        }
    }
}
