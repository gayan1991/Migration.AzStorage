namespace Storage.Migration.Service.Model
{
    public class MigrationConfig
    {
        public List<StorageConfig> StorageAccounts { get; set; } = null!;
        public string SourceSubscription { get; set; } = null!;
        public string TargetSubscription { get; set; } = null!;
    }

    public class StorageConfig
    {
        public string SourceResourceGroup { get; set; } = null!;
        public string SourceUrl { get; set; } = null!;
        public string SourceStorageKey { get; set; } = null!;
        public string TargetResourceGroup { get; set; } = null!;
        public string TargetUrl { get; set; } = null!;
        public string TargetStorageKey { get; set; } = null!;
        public ContainerFiltration Filter { get; set; } = null!;


        #region Computed Properties

        public string SourceAccountName => ExtractName(SourceUrl);
        public string TargetAccountName => ExtractName(TargetUrl);

        #endregion

        private static string ExtractName(string url)
        {
            var myUri = new Uri(url);
            var storageAccountName = myUri.Host;
            var index = storageAccountName.IndexOf(".blob");
            return storageAccountName[..index];
        }
    }

    public class ContainerFiltration
    {
        public FiltrationType Type { get; set; }
        public string Value { get; set; } = null!;
        public string[] Filters { get; set; } = null!;

        public static KeyValuePair<string, string> ExtractContainerName(string filter)
        {
            var index = filter.IndexOf(":");

            if (index == -1)
            {
                return new KeyValuePair<string, string>(filter, string.Empty);
            }

            return new KeyValuePair<string, string>(filter[..index], filter[(index + 1)..]);
        }
    }

    public enum FiltrationType : byte
    {
        Name = 0,
        Date = 1,
        IncludePatterns = 2
    }

    public enum AttributeType : byte
    {
        Path = 0,
        Pattern = 1,
        After = 2
    }
}
