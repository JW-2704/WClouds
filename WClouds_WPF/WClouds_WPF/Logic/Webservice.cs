using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public static class Webservice
    {
        public static string APIKey { get; set; }
        
        private static string URL = "http://127.0.0.1:8000";
        public static HttpClient HttpClient { get; set; } = new HttpClient()
        {
            BaseAddress = new Uri(URL)
        };

        // KI | Prompt: Ich brauch wegen dem ApiKey noch das er nicht
        // nach dem schließen der App immer neu generiert wird
        public static void SetApiKey(string apiKey)
        {
            APIKey = apiKey;
            HttpClient.DefaultRequestHeaders.Remove("X-API-Key");
            HttpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

    }
}
