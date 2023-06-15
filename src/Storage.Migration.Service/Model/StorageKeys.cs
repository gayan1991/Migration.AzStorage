namespace Storage.Migration.Service.Model
{
    public class StorageKeys
    {
        public DateTimeOffset? CreationTime { get; set; }
        public string? KeyName { get; set; }
        public string? Permissions { get; set; }
        public string? Value { get; set; }
    }
}
