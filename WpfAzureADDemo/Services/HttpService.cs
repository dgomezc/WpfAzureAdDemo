using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace WpfAzureADDemo.Services
{
    public class HttpService
    {
        public async Task<string> GetHttpStringContent(string url, string token = null)
        {
            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (token != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }
    }
}
