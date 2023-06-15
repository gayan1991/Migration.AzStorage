namespace Storage.Migration.Service.Model
{
    public class BlobContainer
    {
        public string Name { get; set; } = null!;
        public DateTimeOffset LastModified { get; set; }
    }
}
