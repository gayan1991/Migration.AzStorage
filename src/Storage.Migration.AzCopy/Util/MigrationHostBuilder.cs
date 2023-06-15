using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Storage.Migration.AzCopy.Util
{
    public class MigrationHostBuilder
    {
        private static IHost _hostBuilder;

        internal static IHost MigrationHost => _hostBuilder;

        internal static IHost Build(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (_, services) =>
                    {
                        var startup = new Startup();
                        startup.ConfigureServices(services);

                        services.AddSingleton<Migration, Migration>();
                        services.BuildServiceProvider();
                    });

            return _hostBuilder ??= hostBuilder.Build();
        }
    }
}
