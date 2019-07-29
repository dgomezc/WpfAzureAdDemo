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
using Newtonsoft.Json.Linq;
using WpfAzureADDemo.Configuration;
using WpfAzureADDemo.Models;
using WpfAzureADDemo.Services;

namespace WpfAzureADDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly ARMService _armService;
        readonly GraphService _graphService;
        readonly AzureADSettings azureSettings = ConfigurationHelper.Instance.AzureADSettings;

        string[] managementScopes = new string[] { "https://management.azure.com/user_impersonation" };
        
        public MainWindow()
        {
            InitializeComponent();

            var httpService = new HttpService();
            _graphService = new GraphService(httpService);
            _armService = new ARMService(httpService);
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText.Text = "Loading...";

            var authResult = await _graphService.AuthenticateUser();
            ChangeButtonStatus(authResult);
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _graphService.SignOut();

                ChangeButtonStatus();

            }
            catch (MsalException ex)
            {
                ResultText.Text = $"Error signing-out user: {ex.Message}";
            }
        }

        private async void GetTenantWithARMButton_Click(object sender, RoutedEventArgs e)
        {
            TenantList.Items.Clear();
            ResultText.Text = "Loading...";

            var token = await _graphService.GetAccessToken(managementScopes);
            var tenants = await _armService.GetTenants(token);

            ShowTenantInfo(tenants);
        }

        private async void GetTenantWithGraphButton_Click(object sender, RoutedEventArgs e)
        {
            TenantList.Items.Clear();
            ResultText.Text = "Loading...";

            var token = await _graphService.GetAccessToken(managementScopes);
            var tenantsByArm = await _armService.GetTenants(token);
            var tenants = await _graphService.GetTenant(tenantsByArm);

            ShowTenantInfo(tenants);
        }
                
        private async void CreateAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultText.Text = "Creating app...";

                var appName = AppNameText.Text;
                var tenantId = (TenantList.SelectedItem as TenantInfo).TenantId;
                var audience = (AadAuthorityAudience)AudienceComboBox.SelectedItem;

                var result = await _graphService.CreateApp(appName, tenantId, audience);

                ResultText.Text = JToken.Parse(result).ToString(Formatting.Indented);
            }
            catch (Exception ex)
            {
                ResultText.Text = ex.Message;
            }
        }

        private async void CreateAppWithSdkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultText.Text = "Creating app...";

                var appName = AppNameText.Text;
                var tenantId = (TenantList.SelectedItem as TenantInfo).TenantId;
                var graphSdkService = new GraphSdkService(_graphService, tenantId);
                var audience = (AadAuthorityAudience)AudienceComboBox.SelectedItem;

                var app = await graphSdkService.CreateApp(appName, audience);
                var appJson = JsonConvert.SerializeObject(app, Formatting.Indented);

                ResultText.Text = appJson;
            }
            catch (ServiceException ex)
            {
                ResultText.Text = ex.InnerException?.Message ?? ex.Message;
            }
            catch (Exception ex)
            {
                ResultText.Text = ex.Message;
            }
        }

        private void ChangeButtonStatus(AuthenticationResult result = null)
        {
            var isLogged = result != null;

            if(isLogged)
            {
                ResultText.Text = $"The user {result.Account.Username} has successfully logged";
                UserInfoText.Text = result.Account.Username;
            }
            else
            {
                ResultText.Text = "User has signed-out";
                UserInfoText.Text = string.Empty;
                TenantList.Items.Clear();
            }

            SignInButton.Visibility = isLogged ? Visibility.Collapsed : Visibility.Visible;
            SignOutButton.Visibility = isLogged ? Visibility.Visible : Visibility.Collapsed;
            UserInfoText.Visibility = isLogged ? Visibility.Visible : Visibility.Collapsed;

            GetTenantARMButton.IsEnabled = isLogged;
            GetTenantGraphButton.IsEnabled = isLogged;            
        }

        private void ShowTenantInfo(IEnumerable<TenantInfo> tenants)
        {
            foreach (var tenant in tenants.Where(t => t.IsValid))
            {
                TenantList.Items.Add(tenant);
            }

            var tenantJson = JsonConvert.SerializeObject(tenants, Formatting.Indented);
            ResultText.Text = tenantJson;
        }

        private void TenantList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateCreateApp();
        }

        private void AppNameText_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateCreateApp();
        }

        private void AudienceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateCreateApp();
        }

        private void ValidateCreateApp()
        {
            var hasSelectedItem = TenantList.SelectedItem != null;
            var hasValidAppName = !string.IsNullOrWhiteSpace(AppNameText.Text);
            var hasValidAudience = AudienceComboBox.SelectedItem != null; 

            CreateAppButton.IsEnabled = hasSelectedItem && hasValidAppName && hasValidAudience;
            CreateAppWithSdkButton.IsEnabled = hasSelectedItem && hasValidAppName && hasValidAudience;
        }
    }
}
