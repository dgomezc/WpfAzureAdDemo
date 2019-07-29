using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using WpfAzureADDemo.Configuration;
using WpfAzureADDemo.Models;

namespace WpfAzureADDemo.Services
{
    public class GraphService
    {
        readonly HttpService _httpService;

        string[] scopes = new string[] { "user.read" };
        string[] scopesToGetTenantNames = new string[] { "Directory.Read.All" };
        string[] scopesToCreateApp = new string[] { "Directory.AccessAsUser.All" };
        string[] scopesForGraph = new string[] { "user.read", "Directory.Read.All", "Directory.AccessAsUser.All", "Files.Read" };
        string[] managementScopes = new string[] { "https://management.azure.com/user_impersonation" };

        public GraphService(HttpService httpService)
        {
            _httpService = httpService;
            var settings = ConfigurationHelper.Instance.AzureADSettings;

            ClientApp = PublicClientApplicationBuilder.Create(settings.ClientId)
               .WithAuthority(AzureCloudInstance.AzurePublic, settings.TenantCommon)
               .WithDefaultRedirectUri()
               .Build();
        }

        public IPublicClientApplication ClientApp { get; }

        public async Task<AuthenticationResult> AuthenticateUser()
        {
            AuthenticationResult authResult = null;

            try
            {
                // TODO: Scopes??
                var account = await GetAccount();
                authResult = await ClientApp
                                    .AcquireTokenSilent(scopes, account)
                                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilent.
                // This indicates you need to call AcquireTokenInteractive to acquire a token

                authResult = await AuthenticationInteractive();
            }
            catch (Exception ex)
            {
                //ResultText.Text = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
            }

            return authResult;
        }

        public async Task<AuthenticationResult> AuthenticationInteractive()
        {
            AuthenticationResult authResult = null;

            try
            {
                //TODO. Consent all scopes?? only when App have tenantID configuration ??

                var account = await GetAccount();

                authResult = await ClientApp.AcquireTokenInteractive(scopes)
                    .WithAccount(account)
                    .WithPrompt(Prompt.SelectAccount)
                    .WithExtraScopesToConsent(managementScopes)
                    .ExecuteAsync();
            }
            catch (MsalException msalex)
            {
                //ResultText.Text = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
            }

            return authResult;
        }

        public async Task<IAccount> GetAccount()
        {
            var accounts = await ClientApp.GetAccountsAsync();
            return accounts.FirstOrDefault();
        }




        public async Task SignOut()
        {
            var account = await GetAccount();

            if (account == null)
            {
                return;
            }

            await ClientApp.RemoveAsync(account);
        }

        public async Task<string> GetAccessToken(string[] scopes, string tenant = null)
        {
            // TODO: Merge this method with Authenticate method ??

            var account = await GetAccount();
            AuthenticationResult result;

            if (string.IsNullOrWhiteSpace(tenant))
            {
                result = await ClientApp.AcquireTokenSilent(scopes, account)
                                           .ExecuteAsync();
            }
            else
            {
                string authority = ClientApp.Authority.Replace(new Uri(ClientApp.Authority).PathAndQuery, $"/{tenant}/");

                result = await ClientApp.AcquireTokenSilent(scopes, account)
                                          .WithAuthority(authority)
                                          .ExecuteAsync();
            }

            return result.AccessToken;
        }

        public async Task<string> GetUser()
        {
            string graphAPIUserEndpoint = "https://graph.microsoft.com/beta/me";
            var accessToken = await GetAccessToken(scopes);
            var userInfo = await _httpService.GetHttpStringContent(graphAPIUserEndpoint, accessToken);

            return userInfo;
        }

        public async Task<IEnumerable<TenantInfo>> GetTenant(IEnumerable<TenantInfo> tenantsInfo)
        {
            string graphAPIEndpointOrganizations = "https://graph.microsoft.com/beta/organization";
            var tenantsResult = new List<TenantInfo>();

            foreach (var tenantInfo in tenantsInfo)
            {
                try
                {
                    var accessToken = await GetAccessToken(scopesToGetTenantNames, tenantInfo.TenantId);
                    var json = await _httpService.GetHttpStringContent(graphAPIEndpointOrganizations, accessToken);
                    var result = JsonConvert.DeserializeObject<GetOrganizationResult>(json);

                    tenantsResult.Add(new TenantInfo
                    {
                         TenantId = tenantInfo.TenantId,
                         DisplayName = result.Value.First().DisplayName
                    });
                }
                catch (Exception ex)
                {
                    // You do not have permissions to get the token with that scope, you have to give permissions to the App
                    tenantsResult.Add(new TenantInfo
                    {
                        TenantId = tenantInfo.TenantId,
                        DisplayName = tenantInfo.DisplayName,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
            return tenantsResult;
        }

        public async Task<string> CreateApp(string appName, string tenantId, AadAuthorityAudience audience)
        {
            var graphUrl = "https://graph.microsoft.com/beta";
            var createAppUri = "applications";
            var serializedApp = JsonConvert.SerializeObject(new { displayName = appName, signInAudience = audience.ToString() });
            var accessToken = await GetAccessToken(scopesToCreateApp, tenantId);
            string result = string.Empty;

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri($"{graphUrl}/");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await httpClient.PostAsync(createAppUri, new StringContent(serializedApp, Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {
                        var content = response.Content;
                        result = await content.ReadAsStringAsync();
                    }
                    else
                    {
                        result = await response.Content.ReadAsStringAsync();
                        // TODO WTS: Please handle other status codes as appropriate to your scenario
                    }
                }
            }

            catch (Exception ex)
            {
                result = $"Error to create app: {ex.Message}";
            }

            return result;

        }
    }
}
