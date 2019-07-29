using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using WpfAzureADDemo.Models;

namespace WpfAzureADDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[] scopes = new string[] { "user.read" };
        string[] scopesToGetTenantNames = new string[] { "Directory.Read.All" };
        string[] scopesToCreateApp = new string[] { "Directory.AccessAsUser.All" };
        string[] scopesForGraph = new string[] { "user.read", "Directory.Read.All", "Directory.AccessAsUser.All" };

        string[] managementScopes = new string[] { "https://management.azure.com/user_impersonation" };

        private readonly IPublicClientApplication _app = App.PublicClientApp;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText.Text = "Loading...";
            TokenInfoText.Text = "Loading...";

            var authResult = await AuthenticateUser();

            if (authResult is null)
            {
                return;
            }

            ResultText.Text = $"The user {authResult.Account.Username} has successfully logged";
            TokenInfoText.Text = $"Username: {authResult.Account.Username}" + Environment.NewLine;
            TokenInfoText.Text += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;

            this.SignInButton.Visibility = Visibility.Collapsed;
            //this.SignInWithSdkButton.Visibility = Visibility.Collapsed;
            this.SignOutButton.Visibility = Visibility.Visible;

            this.GetUserInfoButton.Visibility = Visibility.Visible;
            this.GetTenantARMButton.Visibility = Visibility.Visible;
            this.GetTenantGraphButton.Visibility = Visibility.Visible;
            this.CreateAppButton.Visibility = Visibility.Visible;
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var account = await GetAccount();

            if(account == null)
            {
                return;
            }

            try
            {
                await _app.RemoveAsync(account);

                this.ResultText.Text = "User has signed-out";
                this.TokenInfoText.Text = string.Empty;

                this.SignInButton.Visibility = Visibility.Visible;
                this.SignInWithSdkButton.Visibility = Visibility.Visible;
                this.SignOutButton.Visibility = Visibility.Collapsed;

                this.GetUserInfoButton.Visibility = Visibility.Collapsed;
                this.GetTenantARMButton.Visibility = Visibility.Collapsed;
                this.GetTenantGraphButton.Visibility = Visibility.Collapsed;
                this.CreateAppButton.Visibility = Visibility.Collapsed;

            }
            catch (MsalException ex)
            {
                ResultText.Text = $"Error signing-out user: {ex.Message}";
            }
        }

        private async void GetUserInfoButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText.Text = "Loading...";

            var userInfo = await GetUserWithGraphHttpRequest();

            ResultText.Text = userInfo;
        }

        private async void GetTenantWithARMButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText.Text = "Loading...";

            var tenant = await GetTenantsWithARM();

            var tenantsInfo = tenant.Select(t => $"{t.DisplayName} - tenant: {t.TenantId}");
            ResultText.Text = string.Join("\n\n", tenantsInfo);
        }

        private async void GetTenantWithGraphButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText.Text = "Loading...";

            var tenants = await GetTenantsWithARM();
            var organizations = await GetTenantWithGraph(tenants);

            var organizationNames = organizations.Select(s => $"{s.Key} - {s.Value}");
            ResultText.Text = string.Join("\n\n", organizationNames);
        }

        private async Task<AuthenticationResult> AuthenticateUser()
        {
            AuthenticationResult authResult = null;

            try
            {
                // TODO: Scopes??
                var account = await GetAccount();
                authResult = await _app
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
                ResultText.Text = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
            }

            return authResult;
        }

        private async Task<AuthenticationResult> AuthenticationInteractive()
        {
            AuthenticationResult authResult = null;

            try
            {
                //TODO. Consent all scopes?? only when App have tenantID configuration ??

                var account = await GetAccount();

                authResult = await _app.AcquireTokenInteractive(scopes)
                    .WithAccount(account)
                    .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                    .WithExtraScopesToConsent(managementScopes)
                    //.WithExtraScopesToConsent(scopesToGetTenantNames)
                    //.WithExtraScopesToConsent(scopesToCreateApp)
                    .ExecuteAsync();
            }
            catch (MsalException msalex)
            {
                ResultText.Text = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
            }

            return authResult;
        }

        private async Task<string> GetUserWithGraphHttpRequest()
        {
            string graphAPIUserEndpoint = "https://graph.microsoft.com/beta/me";
            var accessToken = await GetAccessToken(scopes);
            var userInfo = await GetHttpContentWithToken(graphAPIUserEndpoint, accessToken);

            return userInfo;
        }

        private async Task<IEnumerable<TenantInfo>> GetTenantsWithARM()
        {
            string azureManagementEndpoint = "https://management.azure.com/tenants?api-version=2017-08-01";

            var accessToken = await GetAccessToken(managementScopes);
            var result = await GetHttpContentWithToken(azureManagementEndpoint, accessToken);
            var tenants = JsonConvert.DeserializeObject<GetTenantResult>(result).Value;

            return tenants;
        }

        private async Task<Dictionary<string, string>> GetTenantWithGraph(IEnumerable<TenantInfo> tenantsInfo)
        {
            string graphAPIEndpointOrganizations = "https://graph.microsoft.com/beta/organization";
            var organizations = new Dictionary<string, string>();
            var tenants = tenantsInfo.Select(s => s.TenantId);

            foreach (var tenant in tenants)
            {
                try
                {
                    var accessToken = await GetAccessToken(scopesToGetTenantNames, tenant);
                    var json = await GetHttpContentWithToken(graphAPIEndpointOrganizations, accessToken);
                    var result = JsonConvert.DeserializeObject<GetOrganizationResult>(json);

                    organizations.Add(tenant, result.Value.First().DisplayName);
                }
                catch (Exception ex)
                {
                    // You do not have permissions to get the token with that scope, you have to give permissions to the App
                    organizations.Add(tenant, ex.Message);
                }
            }
            return organizations;
        }

        private async Task<string> GetAccessToken(string[] scopes, string tenant = null)
        {
            // TODO: Merge this method with Authenticate method ??

            var account = await GetAccount();
            AuthenticationResult result;

            if (string.IsNullOrWhiteSpace(tenant))
            {
                result = await _app.AcquireTokenSilent(scopes, account)
                                           .ExecuteAsync();
            }
            else
            {
                string authority = _app.Authority.Replace(new Uri(_app.Authority).PathAndQuery, $"/{tenant}/");

                result = await _app.AcquireTokenSilent(scopes, account)
                                          .WithAuthority(authority)
                                          .ExecuteAsync();
            }

            return result.AccessToken;
        }

        private async Task<string> GetHttpContentWithToken(string url, string token)
        {
            try
            {
                var httpClient = new HttpClient();
                HttpResponseMessage response;

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        private async Task<IAccount> GetAccount()
        {
            return (await _app
                            .GetAccountsAsync())
                            .FirstOrDefault();
        }













        private GraphServiceClient _graphClient;

        private async Task<User> AuthenticateWithSdk()
        {
            // TODO : Set all Graph scopes??
            var authProvider = new InteractiveAuthenticationProvider(_app);
            _graphClient = new GraphServiceClient(authProvider);

            // force auth, and get data
            var user = await GetUserInfoWithSdk();
            return user;

        }

        private async Task<User> GetUserInfoWithSdk()
        {


            var user = await _graphClient?
                                    .Me
                                    .Request()
                                    .GetAsync();
            return user;
        }

        private async Task<Microsoft.Graph.Application> CreateAppWithSdk(string appName)
        {
            var application = new Microsoft.Graph.Application
            {
                DisplayName = appName
            };

            var createdApp = await _graphClient?.Applications
                                                    .Request()
                                                    .AddAsync(application);

            return createdApp;
        }

        private async void SignInWithSdkButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText.Text = "Loading...";
            TokenInfoText.Text = "Loading...";

            var user = await AuthenticateWithSdk();

            ResultText.Text = $"The user {user.DisplayName} has successfully logged";
            TokenInfoText.Text = $"Username: {user.DisplayName}" + Environment.NewLine;
            TokenInfoText.Text += $"Token Expires: {user.RefreshTokensValidFromDateTime.Value.ToLocalTime()}" + Environment.NewLine;

            this.SignInButton.Visibility = Visibility.Collapsed;
            this.SignInWithSdkButton.Visibility = Visibility.Collapsed;
            this.SignOutButton.Visibility = Visibility.Visible;

            this.GetUserInfoButton.Visibility = Visibility.Visible;
            this.GetTenantARMButton.Visibility = Visibility.Visible;
            this.GetTenantGraphButton.Visibility = Visibility.Visible;
            this.CreateAppButton.Visibility = Visibility.Visible;
        }

        private async void CreateAppButton_Click(object sender, RoutedEventArgs e)
        {
            var appName = "My new App to test";

            //await CreateAppWithGraphHttpRequest(appName);
            var app = await CreateAppWithSdk(appName);
        }

        private async Task CreateAppWithGraphHttpRequest(string appName)
        {
            var graphUrl = "https://graph.microsoft.com/beta";
            var createAppUri = "applications";
            var serializedApp = JsonConvert.SerializeObject( new { displayName = appName });
            var accessToken = await GetAccessToken(scopesToCreateApp);

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
                        var text = await content.ReadAsStringAsync();
                    }
                    else
                    {
                        var result = await response.Content.ReadAsStreamAsync();
                        // TODO WTS: Please handle other status codes as appropriate to your scenario
                    }
                }
            }

            catch (Exception ex)
            {
            }

        }
    }
}
