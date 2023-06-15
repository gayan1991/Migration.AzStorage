using Microsoft.Extensions.DependencyInjection;
using Storage.Migration.Service.Implementation;
using Storage.Migration.Service.Interface;

namespace Storage.Migration.AzCopy
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAzService, AzService>();
            services.AddSingleton<Service.Interface.ILogger, Logger>();
        }
    }
}
