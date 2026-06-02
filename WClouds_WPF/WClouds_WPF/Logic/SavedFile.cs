using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class SavedFile : IStorable
    {
        public int ID { get; set; }
        public string? FileName { get; set; }
        public string? Extension { get; set; } // .png
        public byte[]? Content { get; set; }
        public List<Info> History {  get; set; } = new List<Info>();

        public async Task<List<Info>?> GetHistory()
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/history/file/{ID}");
            response.EnsureSuccessStatusCode();
            string history = await response.Content.ReadAsStringAsync();
            History = JsonSerializer.Deserialize<List<Info>>(history) ?? new List<Info>();
            return History;
        }
    }
}
