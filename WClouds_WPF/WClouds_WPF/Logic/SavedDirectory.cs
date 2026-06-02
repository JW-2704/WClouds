using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class SavedDirectory : IStorable
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public List<SavedDirectory> SubDirectories { get; set; } = new List<SavedDirectory>();
        public List<SavedFile> Content { get; set; } = new List<SavedFile>();
        public List<Info> History { get; set;} = new List<Info>();

        public async Task<List<Info>?> GetHistory()
        {
            HttpResponseMessage response = await Webservice.HttpClient.GetAsync($"/history/directory/{ID}");
            response.EnsureSuccessStatusCode();
            string history = await response.Content.ReadAsStringAsync();
            History = JsonSerializer.Deserialize<List<Info>>(history) ?? new List<Info>();
            return History;
        }
    }
}
