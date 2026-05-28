using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class SavedFile : IStorable
    {
        public List<Info> History { get => throw new NotImplementedException();
            set => throw new NotImplementedException(); }
        public byte[] Content { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; } // .png
    }
}
