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

    }
}
