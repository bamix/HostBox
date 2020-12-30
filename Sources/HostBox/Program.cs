using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Common.Logging;
using Common.Logging.Configuration;
using HostBox.Configuration;
using HostBox.Loading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace HostBox
{
    internal class Program
    {
        private const string ConfigurationNameEnvVariable = "configuration";
        private static ILog Logger => LogManager.GetLogger<Program>();

        private static async Task Main(string[] args = null)
        {
            CommandLineArgs commandLineArgs = null;
            try
            {
                commandLineArgs = CommandLineArgsProvider.Get(args);

                if (commandLineArgs.StartConfirmationRequired)
                {
                    Console.WriteLine("Press enter to start");
                    Console.ReadLine();
                }

                if (commandLineArgs.CommandLineArgsValid)
                {
                    await CreateHostBuilder(commandLineArgs)
                        .Build()
                        .RunAsync();
                }
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Fatal("Hosting failed.", ex);
                }
                else
                {
                    Console.WriteLine($"Error: {ex}");
                }

                throw;
            }
            finally
            {
                if (commandLineArgs?.FinishConfirmationRequired ?? false)
                {
                    Console.WriteLine("Press enter to finish");
                    Console.ReadLine();
                }
            }
        }

        /// <summary>
        /// В версии .netcore 2.2 HostBuilder и WebHostBuilder это абсолютно разные классы. HostBuilder не позволяет создавать доступ к Web. Поэтому создаются разные стратегии
        /// </summary>
        /// <param name="commandLineArgs"></param>
        /// <returns></returns>
        private static IHostBuilder CreateHostBuilder(CommandLineArgs commandLineArgs)
        {
            var componentPath = Path.GetFullPath(commandLineArgs.Path, Directory.GetCurrentDirectory());
            
            var loader = new ComponentsLoader(
                new ComponentConfig
                {
                    Path = componentPath,
                    SharedLibraryPath = commandLineArgs.SharedLibrariesPath,
                    LoggerFactory = LogManager.GetLogger
                }).Load();
            
            var builder = new HostBuilder()
                .ConfigureHostConfiguration(
                    config =>
                    {
                        config.AddEnvironmentVariables();

                        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);

                        config.AddJsonFile("hostsettings.json", true, false);

                        ConfigureLogging(config.Build());
                    })
                .ConfigureAppConfiguration(
                    (ctx, config) =>
                    {
                        LoadConfiguration(ctx.Configuration, config, componentPath, commandLineArgs);
                    })
                .ConfigureServices(
                    (ctx, services) =>
                    {
                        loader.Run(ctx.Configuration, CancellationToken.None);
                    });
            
            if (commandLineArgs.Web)
            {
                var startupType = loader.EntryAssembly.GetExportedTypes()
                    .First(t => typeof(IHostingStartup).IsAssignableFrom(t));
                builder.ConfigureWebHost(b =>
                {
                    b.UseStartup<Startup>();
                    ((IHostingStartup)Activator.CreateInstance(startupType))?.Configure(b);
                });
            }

            return builder;
        }
        
        private static void ConfigureLogging(IConfiguration config)
        {
            var logConfiguration = new LogConfiguration();
            config.GetSection("common:logging").Bind(logConfiguration);
            LogManager.Configure(logConfiguration);
        }
        
        private static void LoadConfiguration(IConfiguration currentConfiguration, IConfigurationBuilder config, string componentPath, CommandLineArgs args)
        {
            Logger.Trace(m => m("Loading hostable component using path [{0}].", componentPath));

            var componentBasePath = Path.GetDirectoryName(componentPath);

            config.SetBasePath(componentBasePath);

            var configName = currentConfiguration[ConfigurationNameEnvVariable];

            Logger.Info(m => m("Application was launched with configuration '{0}'.", configName));

            config.LoadSharedLibraryConfigurationFiles(Logger, componentBasePath, args.SharedLibrariesPath);

            var configProvider = new ConfigFileNamesProvider(configName, componentBasePath);

            var templateValuesSource =
                new JsonConfigurationSource
                {
                    Path = configProvider.GetTemplateValuesFile(),
                    FileProvider = null,
                    ReloadOnChange = false,
                    Optional = true
                };

            templateValuesSource.ResolveFileProvider();

            var templateValuesProvider = templateValuesSource.Build(config);

            templateValuesProvider.Load();

            foreach (var configFile in configProvider.EnumerateConfigFiles())
            {
                config.AddJsonTemplateFile(configFile, false, false, templateValuesProvider, args.PlaceholderPattern);

                Logger.Trace(m => m("Configuration file [{0}] is loaded.", configFile));
            }
        }
    }
}
