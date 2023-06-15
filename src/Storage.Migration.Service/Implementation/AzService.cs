using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Storage.Migration.Service.Interface;
using Storage.Migration.Service.Model;
using Storage.Migration.Service.Util;

namespace Storage.Migration.Service.Implementation
{
    public class AzService : IAzService
    {
        private readonly ILogger _logger;

        public AzService(ILogger logger)
        {
            _logger = logger;
        }

        public Task LogIn()
        {
            return Command.Execute("az login");
        }

        public Task SelectSubscription(string subscriptionName)
        {
            return Command.Execute($"az account set --subscription {subscriptionName}");
        }

        public async Task<List<StorageKeys>> GetStorageKeys(string resourceGroupName, string storageAccountName)
        {
            var result = await Command.ExecuteWithOutput($"az storage account keys list --resource-group {resourceGroupName} --account-name {storageAccountName}");

            if (string.IsNullOrEmpty(result))
            {
                _logger.WriteLine("Storage Keys: empty output");
                return null!;
            }

            var rtnObj = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StorageKeys>>(result);
            return rtnObj!;
        }

        public Task Copy(string source, string target)
        {
            var copyScript = $"azcopy copy \"{source}\" \"{target}\" --recursive=true --overwrite=false";
            _logger.WriteLine(copyScript);
            return Command.Execute(copyScript, true);
        }

        public async Task<string> GenerateBlobStorageSASUrl(string storageAccountName, string storageAccessKey, string storageUrl, string containerName = null!)
        {
            var urlStr = string.Empty;
            var accountUri = new Uri(storageUrl);
            var sharedKeyCredential = new StorageSharedKeyCredential(storageAccountName, storageAccessKey);

            var client = new BlobServiceClient(accountUri, sharedKeyCredential);
            try
            {
                var permissionString = "racwl";
                var startsOn = DateTimeOffset.UtcNow.AddHours(-1);
                var expiresOn = DateTime.Now.AddDays(1);

                if (string.IsNullOrWhiteSpace(containerName))
                {
                    var sasBuilder = new AccountSasBuilder()
                    {
                        StartsOn = startsOn,
                        ExpiresOn = expiresOn,
                        ResourceTypes = AccountSasResourceTypes.All,
                        Services = AccountSasServices.Blobs,
                    };
                    sasBuilder.SetPermissions(permissionString);
                    var sasUri = client.GenerateAccountSasUri(sasBuilder);
                    urlStr = sasUri.AbsoluteUri;
                }
                else
                {
                    var container = client.GetBlobContainerClient(containerName);
                    if (!await container.ExistsAsync())
                    {
                        _logger.WriteLine($"Container {containerName} does not exists");
                        _logger.WriteLine($"Creating container {containerName}");
                        container.CreateIfNotExists();
                        await Task.Delay(1000);
                    }

                    var blobSasBuilder = new BlobSasBuilder()
                    {
                        BlobContainerName = containerName,
                        ExpiresOn = expiresOn,
                        StartsOn = startsOn,
                        Protocol = SasProtocol.Https,
                    };
                    blobSasBuilder.SetPermissions(permissionString);
                    var sasUri = container.GenerateSasUri(blobSasBuilder);
                    urlStr = sasUri.AbsoluteUri;
                }

                _logger.WriteLine($"SAS URI for blob is: {urlStr}");
            }
            catch (RequestFailedException e)
            {
                _logger.WriteLine(e.ErrorCode!);
                _logger.WriteLine(e.Message);
            }

            return urlStr;
        }

        public async Task<List<BlobContainer>> GetContainerList(string storageAccountName, string storageAccessKey, string storageUrl)
        {
            var lst = new List<BlobContainer>();
            var accountUri = new Uri(storageUrl);
            var sharedKeyCredential = new StorageSharedKeyCredential(storageAccountName, storageAccessKey);

            var client = new BlobServiceClient(accountUri, sharedKeyCredential);

            try
            {
                var resultSegment =
                    client.GetBlobContainersAsync(BlobContainerTraits.Metadata, "", default)
                    .AsPages(default, null);

                await foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
                {
                    foreach (BlobContainerItem containerItem in containerPage.Values)
                    {
                        lst.Add(new BlobContainer { Name = containerItem.Name, LastModified = containerItem.Properties.LastModified });
                    }
                }
            }
            catch (RequestFailedException e)
            {
                _logger.WriteLine(e.Message);
                throw;
            }

            return lst;
        }
    }
}
