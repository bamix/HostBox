using System.IO;
using System.Threading.Tasks;

using Common.Logging;
using Common.Logging.Configuration;

using HostBox.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace HostBox
{
    internal abstract class HostingBase : IHosting
    {
        private const string ConfigurationNameEnvVariable = "configuration";

        protected CommandLineArgs Args;

        protected string ComponentPath;

        protected HostingBase(CommandLineArgs args)
        {
            this.Args = args;
            this.ComponentPath = Path.GetFullPath(this.Args.Path, Directory.GetCurrentDirectory());
        }

        protected abstract ILog Logger { get; }

        public abstract Task Run();

        protected void ConfigureLogging(IConfiguration config)
        {
            var logConfiguration = new LogConfiguration();
            config.GetSection("common:logging").Bind(logConfiguration);
            LogManager.Configure(logConfiguration);
        }

        protected void LoadConfiguration(IConfiguration currentConfiguration, IConfigurationBuilder config)
        {
            this.Logger.Trace(m => m("Loading hostable component using path [{0}].", this.ComponentPath));

            var componentBasePath = Path.GetDirectoryName(this.ComponentPath);

            config.SetBasePath(componentBasePath);

            var configName = currentConfiguration[ConfigurationNameEnvVariable];

            this.Logger.Info(m => m("Application was launched with configuration '{0}'.", configName));

            config.LoadSharedLibraryConfigurationFiles(
                this.Logger, componentBasePath,
                this.Args.SharedLibrariesPath);

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
                config.AddJsonTemplateFile(configFile, false, false, templateValuesProvider, this.Args.PlaceholderPattern);

                this.Logger.Trace(m => m("Configuration file [{0}] is loaded.", configFile));
            }
        }
    }
}