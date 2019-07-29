namespace WpfAzureADDemo.Models
{
    public class TenantInfo
    {
        public string TenantId { get; set; }
        
        public string DisplayName { get; set; }

        public bool IsValid { get; set; } = true;

        public string ErrorMessage { get; set; }
    }
}
