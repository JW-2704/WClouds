using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace WClouds_WPF.Logic
{
    public interface IStorable
    {
        public List<Info> History { get; set; }
        public async Task<int> GetLatestChangerID()
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync("/lastChanger");
            response.EnsureSuccessStatusCode();
            string id = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<int>(id);
        }
        public async Task<int> GetOwnerID()
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync("/owner");
            response.EnsureSuccessStatusCode();
            string id = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<int>(id);
        }
        public async Task<DateTime> GetLatestDateTime()
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync("/timestamp");
            response.EnsureSuccessStatusCode();
            string time = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DateTime>(time);
        }

        public async Task<double> GetLatestSize()
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync("/size");
            response.EnsureSuccessStatusCode();
            string size = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<double>(size);
        }
    }
}
