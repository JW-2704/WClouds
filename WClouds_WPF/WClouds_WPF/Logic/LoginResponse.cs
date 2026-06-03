using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class LoginResponse
    {
        public string session_key { get; set; }
        public int user_id { get; set; }

        
    }
    
}
