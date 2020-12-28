using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Common.Logging;

using HostBox.Loading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace HostBox
{
    internal class WebHosting : HostingBase
    {
        protected override ILog Logger => LogManager.GetLogger<WebHosting>();

        private readonly IWebHostBuilder builder;

        public WebHosting(CommandLineArgs args) : base(args)
        {
            var loader = new ComponentsLoader(
                new ComponentConfig
                    {
                        Path = this.ComponentPath,
                        SharedLibraryPath = args.SharedLibrariesPath,
                        LoggerFactory = LogManager.GetLogger
                    }).Load();

            var startupType = loader.EntryAssembly.GetExportedTypes()
                .First(t => typeof(IHostingStartup).IsAssignableFrom(t));

            var configurationRoot = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddEnvironmentVariables()
                .AddEnvironmentVariables("ASPNETCORE_")
                .AddJsonFile("hostsettings.json", true, false)
                .Build();

            this.ConfigureLogging(configurationRoot);

            this.builder = WebHost.CreateDefaultBuilder()
              .UseStartup<Startup>()
              .UseConfiguration(configurationRoot)
              .ConfigureAppConfiguration(
                  (ctx, config) =>
                      {
                          this.LoadConfiguration(ctx.Configuration, config);
                      })
              .ConfigureServices((ctx, services) =>
                  {
                      loader.Run(ctx.Configuration, CancellationToken.None);
                  });

          ((IHostingStartup)Activator.CreateInstance(startupType)).Configure(this.builder);
        }

        public override async Task Run()
        {
            await this.builder.Build().RunAsync();
        }
    }
}