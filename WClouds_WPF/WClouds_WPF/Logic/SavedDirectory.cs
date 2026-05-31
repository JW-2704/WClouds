using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class SavedDirectory : IStorable
    {
        
        public List<SavedFile> Content { get; }
        public List<Info> History { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
