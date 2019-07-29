using Microsoft.Extensions.Configuration;
using System;

namespace WpfAzureADDemo.Configuration
{
    public class ConfigurationHelper
    {
        private readonly IConfiguration _config;

        private static ConfigurationHelper _instance;

        public static ConfigurationHelper Instance
        {
            get
            {
                return _instance ?? (_instance = new ConfigurationHelper());                
            }
        }

        private ConfigurationHelper()
        {
            var configurationBuilder = new ConfigurationBuilder()
                   .SetBasePath(Environment.CurrentDirectory)
                   .AddJsonFile("settings.json");

            _config = configurationBuilder.Build();

            AzureADSettings = _config
                                    .GetSection("AzureAD")
                                    .Get<AzureADSettings>();
        }

        public AzureADSettings AzureADSettings { get; }
    }
}
