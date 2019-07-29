using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Identity.Client;

namespace WpfAzureADDemo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            // podemos usar : PublicClientApplicationOptions 
            // IPublicClientApplication app = PublicClientApplicationBuilder.CreateWithApplicationOptions(options)

            _clientApp = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, Tenant)
            .WithDefaultRedirectUri()
            .Build();
        }

        private static string ClientId = "my_client_id";

        //private static string Tenant = "my_tenant";
        private static string Tenant = "common";

        private static IPublicClientApplication _clientApp;

        public static IPublicClientApplication PublicClientApp { get { return _clientApp; } }
    }
}
