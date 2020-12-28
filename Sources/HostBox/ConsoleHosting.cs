using System;
using System.Threading.Tasks;

using Common.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HostBox
{
    internal class ConsoleHosting : HostingBase
    {
        private readonly IHostBuilder builder;

        protected override ILog Logger => LogManager.GetLogger<ConsoleHosting>();

        public ConsoleHosting(CommandLineArgs args) : base(args)
        {
            this.builder = new HostBuilder()
                .ConfigureHostConfiguration(
                    config =>
                    {
                        config.AddEnvironmentVariables();

                        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);

                        config.AddJsonFile("hostsettings.json", true, false);

                        this.ConfigureLogging(config.Build());
                    })
                .ConfigureAppConfiguration(
                    (ctx, config) =>
                    {
                        this.LoadConfiguration(ctx.Configuration, config);
                    })
                .ConfigureServices(
                    (ctx, services) =>
                    {
                        services
                            .AddSingleton(provider => new ComponentConfig
                            {
                                Path = this.ComponentPath,
                                SharedLibraryPath = args.SharedLibrariesPath,
                                LoggerFactory = LogManager.GetLogger
                            });

                        services.AddSingleton<IHostedService, Application>();
                    });
        }

        public override async Task Run()
        {
            await this.builder.Build().RunAsync();
        }
    }
}