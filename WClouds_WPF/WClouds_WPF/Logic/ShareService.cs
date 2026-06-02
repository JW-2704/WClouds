using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public class ShareService
    {
        public async Task ShareFile(List<int> MemberIDs, bool CanRead, bool CanWrite, int FileID)
        {
            var shareRequest = new
            {
                fileId = FileID,
                memberIds = MemberIDs,
                canRead = CanRead,
                canWrite = CanWrite
            };

            HttpResponseMessage response = await Webservice.HttpClient.PostAsJsonAsync("/share/file", shareRequest);
            response.EnsureSuccessStatusCode();
        }
    }
}
