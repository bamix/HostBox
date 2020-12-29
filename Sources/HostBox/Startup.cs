using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HostBox
{
    /// <summary>
    /// Пустой стартап, т.к. WebHostBuilder не позволяет сбилдиться, если не указан Startup.
    /// Вся конфигурация задаётся в IHostingStartup в целевой сборке
    /// </summary>
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
        }
    }
}