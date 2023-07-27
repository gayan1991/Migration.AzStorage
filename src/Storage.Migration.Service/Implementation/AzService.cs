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

        public Task Copy(string source, string target, string includes, AttributeType type = AttributeType.Path)
        {
            var includeAttr = type switch
            {
                AttributeType.Pattern => "--include-pattern",
                AttributeType.Path => "--include-path",
                AttributeType.After => "--include-after",
                _ => "--include-path"
            };

            var copyScript = $"azcopy copy \"{source}\" \"{target}\" --recursive=true --overwrite=false {includeAttr}={includes}";
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

        public async Task<List<BlobData>> GetBlobsList(string storageAccountName, string storageAccessKey, string storageUrl, string filter)
        {
            var lst = new List<BlobData>();
            var accountUri = new Uri(storageUrl);
            var sharedKeyCredential = new StorageSharedKeyCredential(storageAccountName, storageAccessKey);

            var client = new BlobServiceClient(accountUri, sharedKeyCredential);
            var contsinerInfo = ContainerFiltration.ExtractContainerName(filter);

            if (string.IsNullOrWhiteSpace(contsinerInfo.Value))
            {
                var fullURL = await GenerateBlobStorageSASUrl(storageAccountName, storageAccessKey, storageUrl, contsinerInfo.Key);
                lst.Add(new BlobData { StorageURL = fullURL });
                return lst;
            }

            try
            {
                var permissionString = "racwl";
                var startsOn = DateTimeOffset.UtcNow.AddHours(-1);
                var expiresOn = DateTime.Now.AddDays(1);

                var container = client.GetBlobContainerClient(contsinerInfo.Key);
                if (!await container.ExistsAsync())
                {
                    _logger.WriteLine($"Container {contsinerInfo.Key} does not exists");
                    _logger.WriteLine($"Creating container {contsinerInfo.Key}");
                    container.CreateIfNotExists();
                    await Task.Delay(1000);
                }
                var blobSasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = contsinerInfo.Key,
                    ExpiresOn = expiresOn,
                    StartsOn = startsOn,
                    Protocol = SasProtocol.Https,
                };
                blobSasBuilder.SetPermissions(permissionString);
                var sasUri = container.GenerateSasUri(blobSasBuilder);

                if (contsinerInfo.Value[0] != '*')
                {
                    lst.AddRange(await GetBlobsListByPrefix(container, blobSasBuilder, storageUrl, contsinerInfo.Value));
                }
                else
                {
                    lst.AddRange(await GetBlobsListByWildCardSearch(container, blobSasBuilder, storageUrl, contsinerInfo.Value));
                }

                if (!lst.Any())
                {
                    lst.Add(new BlobData { Query = sasUri.Query, ContainerName = contsinerInfo.Key, StorageURL = storageUrl });
                }

                _logger.WriteLine($"SAS URI for blob is: {sasUri.AbsoluteUri}");
            }
            catch (RequestFailedException e)
            {
                _logger.WriteLine(e.ErrorCode!);
                _logger.WriteLine(e.Message);
            }

            return lst;
        }

        #region Private

        private async Task<List<BlobData>> GetBlobsListByPrefix(BlobContainerClient container, BlobSasBuilder blobSasBuilder, string storageUrl, string preFix)
        {
            var lst = new List<BlobData>();
            var getBlobsTsk = container.GetBlobsByHierarchyAsync(prefix: preFix).AsPages(default, null);

            await foreach (Azure.Page<BlobHierarchyItem> containerPage in getBlobsTsk)
            {
                foreach (BlobHierarchyItem containerItem in containerPage.Values)
                {
                    if (!containerItem.IsBlob)
                    {
                        continue;
                    }

                    var item = container.GetBlobClient(containerItem.Blob.Name);

                    if (item != null)
                    {
                        var blobSasUri = item.GenerateSasUri(blobSasBuilder);
                        lst.Add(new BlobData { Name = containerItem.Blob.Name, ContainerName = container.Name, Query = blobSasUri.Query, StorageURL = storageUrl });
                    }

                }
            }

            return lst;
        }

        private async Task<List<BlobData>> GetBlobsListByWildCardSearch(BlobContainerClient container, BlobSasBuilder blobSasBuilder, string storageUrl, string filter)
        {
            var lst = new List<BlobData>();
            filter = filter.Replace("*", string.Empty);
            var getBlobs = container.GetBlobsAsync().AsPages(default, null);

            await foreach (Azure.Page<BlobItem> containerPage in getBlobs)
            {
                foreach (BlobItem blob in containerPage.Values)
                {
                    if (!blob.Name.Contains(filter))
                        continue;

                    var item = container.GetBlobClient(blob.Name);

                    if (item != null)
                    {
                        var blobSasUri = item.GenerateSasUri(blobSasBuilder);
                        lst.Add(new BlobData { Name = blob.Name, ContainerName = container.Name, Query = blobSasUri.Query, StorageURL = storageUrl });
                    }

                }
            }

            return lst;
        }

        #endregion
    }
}
