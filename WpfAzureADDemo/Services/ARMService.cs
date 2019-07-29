using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfAzureADDemo.Models;

namespace WpfAzureADDemo.Services
{
    public class ARMService
    {
        readonly HttpService _httpService;
        readonly string armTenantsEndpoint = "https://management.azure.com/tenants?api-version=2017-08-01";

        public ARMService(HttpService htppService)
        {
            _httpService = htppService;
        }

        public async Task<IEnumerable<TenantInfo>> GetTenants(string token)
        {
            // TODO: Get token to _azureADAuthService

            var result = await _httpService.GetHttpStringContent(armTenantsEndpoint, token);
            var tenants = JsonConvert.DeserializeObject<GetTenantResult>(result).Value;
            return tenants;
        }
    }
}
