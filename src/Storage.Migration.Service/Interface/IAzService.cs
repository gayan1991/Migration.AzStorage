using Storage.Migration.Service.Model;

namespace Storage.Migration.Service.Interface
{
    public interface IAzService
    {
        Task LogIn();
        Task SelectSubscription(string subscriptionName);
        Task<List<StorageKeys>> GetStorageKeys(string resourceGroupName, string storageAccountName);
        Task<string> GenerateBlobStorageSASUrl(string storageAccountName, string storageAccessKey, string storageUrl, string containerName = null!);
        Task Copy(string source, string target);
        Task Copy(string source, string target, string includes, AttributeType type = AttributeType.Path);
        Task<List<BlobContainer>> GetContainerList(string storageAccountName, string storageAccessKey, string storageUrl);
        Task<List<BlobData>> GetBlobsList(string storageAccountName, string storageAccessKey, string storageUrl, string filter);
    }
}
