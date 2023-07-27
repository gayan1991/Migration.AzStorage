namespace Storage.Migration.Service.Model
{
    public class BlobData
    {
        public string Name { get; set; } = null!;
        public string ContainerName { get; set; } = null!;
        public string StorageURL { get; set; } = null!;
        public string Query { get; set; } = null!;
        public string IncludePaths { get; set; } = null!;
        public string IncludePatterns { get; set; } = null!;

        public override string ToString()
        {
            if (string.IsNullOrEmpty(ContainerName))
            {
                return StorageURL;
            }

            return $"{StorageURL}{ContainerName}/{Name}{Query}";
        }
    }
}
