using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace WClouds_WPF.Logic
{
    public interface IStorable
    {
        public int ID { get; set; }
        public List<Info> History { get; set; }

        Task<List<Info>?> GetHistory();
    }
}
