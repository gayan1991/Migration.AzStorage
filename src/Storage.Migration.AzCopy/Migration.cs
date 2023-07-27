using Newtonsoft.Json;
using Storage.Migration.Service.Interface;
using Storage.Migration.Service.Model;
using System.Diagnostics;

namespace Storage.Migration.AzCopy
{
    public class Migration
    {
        private readonly MigrationConfig config = null!;
        private const string fileName = "Migration.Config.json";
        private readonly ILogger _logger;
        private readonly IAzService _azService;

        private readonly List<Task> _tasks = new();
        private int count = 0;

        public Migration(IAzService azService, ILogger logger)
        {
            _logger = logger;
            _azService = azService;

            var file = System.IO.File.ReadAllText(fileName);
            _logger.WriteLine($"{fileName} is requested");

            if (string.IsNullOrEmpty(file))
            {
                _logger.WriteLine($"{fileName} is not found");
                return;
            }

            config = JsonConvert.DeserializeObject<MigrationConfig>(file)!;
            _logger.WriteLine($"Configuration is set from Migration Config");
        }

        internal async Task Run()
        {
            await _azService.LogIn();

            foreach (var account in config.StorageAccounts)
            {
                if (string.IsNullOrWhiteSpace(account.SourceStorageKey))
                {
                    _logger.WriteLine($"Making a connection with {account.SourceAccountName}");
                    account.SourceStorageKey = await GetStorageKey(config.SourceSubscription,
                                                                    account.SourceResourceGroup,
                                                                    account.SourceAccountName);
                }

                if (string.IsNullOrWhiteSpace(account.TargetStorageKey))
                {
                    _logger.WriteLine($"Making a connection with {account.TargetAccountName}");
                    account.TargetStorageKey = await GetStorageKey(config.TargetSubscription,
                                                                    account.TargetResourceGroup,
                                                                    account.TargetAccountName);
                }

                if (account.Filter != null)
                {
                    switch (account.Filter.Type)
                    {
                        case FiltrationType.Date:

                            _logger.WriteLine($"Listing all blob containers for {account.SourceAccountName}");
                            var containers = await _azService.GetContainerList(account.SourceAccountName,
                                                                                account.SourceStorageKey,
                                                                                account.SourceUrl);

                            var dt = DateTimeOffset.Parse(account.Filter.Value.Trim());
                            await CopyContainers(account, containers.Where(x => x.LastModified >= dt).Select(x => x.Name).ToArray());
                            break;
                        case FiltrationType.Name:
                            await CopyContainers(account, account.Filter.Filters);
                            break;
                        case FiltrationType.IncludePatterns:
                            await CopyBlobs(account, account.Filter.Filters);
                            break;
                    }
                }
                else
                {
                    _logger.WriteLine($"SAS URL is creation process is started for {account.SourceAccountName}");
                    var source = await _azService.GenerateBlobStorageSASUrl(account.SourceAccountName,
                                                                            account.SourceStorageKey,
                                                                            account.SourceUrl);

                    _logger.WriteLine($"SAS URL is creation process is started for {account.TargetAccountName}");
                    var target = await _azService.GenerateBlobStorageSASUrl(account.TargetAccountName,
                                                                            account.TargetStorageKey,
                                                                            account.TargetUrl);

                    count++;
                    _tasks.Add(_azService.Copy(source, target));
                }
            }

            await Copy();
        }

        #region Private

        private async Task<string> GetStorageKey(string subscription, string resourceGroupName, string accountName)
        {
            await _azService.SelectSubscription(subscription);
            var keys = await _azService.GetStorageKeys(resourceGroupName, accountName);

            if (keys != null && keys.Count > 0)
            {
                return keys.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => x.Value ?? string.Empty).First();
            }

            return string.Empty;
        }

        private async Task CopyContainers(StorageConfig account, string[] containers)
        {
            foreach (var container in containers)
            {
                _logger.WriteLine($"SAS URL is creation process is started for {account.SourceAccountName}:{container}");
                var source = await _azService.GenerateBlobStorageSASUrl(account.SourceAccountName,
                                                                        account.SourceStorageKey,
                                                                        account.SourceUrl,
                                                                        container.Trim());

                _logger.WriteLine($"SAS URL is creation process is started for {account.TargetAccountName}:{container}");
                var target = await _azService.GenerateBlobStorageSASUrl(account.TargetAccountName,
                                                                        account.TargetStorageKey,
                                                                        account.TargetUrl,
                                                                        container.Trim());

                count++;
                _tasks.Add(_azService.Copy(source, target));
            }
        }

        private async Task CopyBlobs(StorageConfig account, string[] filters)
        {
            foreach (var filter in filters)
            {
                var container = ContainerFiltration.ExtractContainerName(filter);

                if (string.IsNullOrWhiteSpace(container.Value))
                {
                    await CopyContainers(account, new string[] { filter });
                    return;
                }

                _logger.WriteLine($"SAS URL is creation process is started for {account.SourceAccountName}:{container.Key}");
                var source = await _azService.GenerateBlobStorageSASUrl(account.SourceAccountName,
                                                                       account.SourceStorageKey,
                                                                       account.SourceUrl,
                                                                       container.Key.Trim());

                _logger.WriteLine($"SAS URL is creation process is started for {account.TargetAccountName}:{container.Key}");
                var target = await _azService.GenerateBlobStorageSASUrl(account.TargetAccountName,
                                                                        account.TargetStorageKey,
                                                                        account.TargetUrl,
                                                                        container.Key.Trim());

                if (container.Value[0] == '*')
                {
                    _tasks.Add(_azService.Copy(source, target, container.Value[1..], AttributeType.Pattern));
                }
                else if (container.Value.Contains("dt"))
                {
                    _tasks.Add(_azService.Copy(source, target, container.Value.Replace("dt", string.Empty), AttributeType.After));
                }
                else
                {
                    _tasks.Add(_azService.Copy(source, target, container.Value));
                }
            }
        }

        private async Task Copy()
        {
            var sw = Stopwatch.StartNew();
            _logger.WriteLine($"AzCopy execution begins for {count} tasks");
            _logger.WriteLine($"Starts at {DateTimeOffset.Now}");

            await Task.WhenAll(_tasks);

            sw.Stop();
            _logger.WriteLine(sw.Elapsed.ToString());
            _logger.WriteLine($"finishes at {DateTimeOffset.Now}");
        }

        private async Task Copy(string source, string target)
        {
            var sw = Stopwatch.StartNew();
            _logger.WriteLine($"AzCopy execution begins");
            _logger.WriteLine($"Starts at {DateTimeOffset.Now}");

            await _azService.Copy(source, target);

            sw.Stop();
            _logger.WriteLine(sw.Elapsed.ToString());
            _logger.WriteLine($"finishes at {DateTimeOffset.Now}");
        }

        #endregion
    }
}