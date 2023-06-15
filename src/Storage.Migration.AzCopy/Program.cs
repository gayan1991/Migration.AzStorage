using Microsoft.Extensions.DependencyInjection;
using Storage.Migration.AzCopy;
using Storage.Migration.AzCopy.Util;

var host = MigrationHostBuilder.Build(args);

var app = host.Services.GetRequiredService<Migration>();
await app.Run();
