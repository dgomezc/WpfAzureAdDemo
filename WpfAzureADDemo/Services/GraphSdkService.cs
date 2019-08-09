using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using WpfAzureADDemo.Configuration;

namespace WpfAzureADDemo.Services
{
    public class GraphSdkService
    {
        string[] scopesToCreateApp = new string[] { "Directory.AccessAsUser.All", "Files.Read" };
        // readonly IPublicClientApplication _app;
        readonly GraphServiceClient _graphClient;

        public GraphSdkService(GraphService graphService, string tenantId)
        {
            //var settings = ConfigurationHelper.Instance.AzureADSettings;

            //_app = PublicClientApplicationBuilder.Create(settings.ClientId)
            //   .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            //   .WithDefaultRedirectUri()
            //   .Build();

            //var authProvider = new InteractiveAuthenticationProvider(_app);
            //_graphClient = new GraphServiceClient(authProvider);

            _graphClient = new GraphServiceClient(
                        new DelegateAuthenticationProvider(
                            async (requestMessage) =>
                            {
                                var token = await graphService.GetAccessToken(scopesToCreateApp, tenantId);
                                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
                            }));
        }

        public async Task<User> Authenticate()
        {    
            // force auth, and get data
            var user = await GetUser();
            return user;
        }

        public async Task<User> GetUser()
        {
            var user = await _graphClient?
                                    .Me
                                    .Request()
                                    .GetAsync();
            return user;
        }

        public async Task<Application> CreateApp(string appName, AadAuthorityAudience audience)
        {
            var application = new Application
            {
                DisplayName = appName,
                SignInAudience = audience.ToString()
            };

            var createdApp = await _graphClient?.Applications
                                                    .Request()
                                                    .AddAsync(application);

            return createdApp;
        }

        public async Task<IGraphServiceApplicationsCollectionPage> GetApps()
        {
            var apps = await _graphClient?
                                    .Applications
                                    .Request()
                                    .GetAsync();

            return apps;
        }

    }
}
