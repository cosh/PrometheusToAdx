namespace KustoWriterApp.Controllers
{
    public class SettingsKusto
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? TenantId { get; set; }
        public string? ClusterName { get; set; }
        public string? DbName { get; set; }
        public string? TableName { get; set; }
        public string? MappingName { get; set; }
        public int MaxRetries { get; set; } = 10;
        public int MsBetweenRetries { get; set; } = 60000;
    }
}
