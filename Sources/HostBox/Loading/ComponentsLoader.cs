using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Common.Logging;

using HostBox.Borderline;
using HostBox.Configuration;

using McMaster.NETCore.Plugins;

using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyModel;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace HostBox.Loading
{
    /// <summary>
    /// Обёртка над PluginLoader для разделения загрузки IHostableComponentFactory на 2 этапа:
    /// 1. Загрузка всех assembly
    /// 2. Создание и запуск IHostableComponentFactory
    /// </summary>
    public class ComponentsLoader
    {
        private static readonly ILog Logger = LogManager.GetLogger<ComponentsLoader>();

        private readonly PluginLoader loader;

        public Assembly EntryAssembly { get; private set; }

        public Assembly[] ComponentsAssemblies { get; private set; }

        public List<IHostableComponentFactory> Factories { get; } = new List<IHostableComponentFactory>();

        public ComponentsLoader(ComponentConfig config)
        {
            this.loader = PluginLoader.CreateFromAssemblyFile(
                config.Path,
                new[]
                    {
                        typeof(Borderline.IConfiguration),
                        typeof(DependencyContext)
                    },
                config.SharedLibraryPath);
        }

        public ComponentsLoader Load()
        {
            this.EntryAssembly = this.loader.LoadDefaultAssembly();
            var entryAssemblyName = this.EntryAssembly.GetName(false);

            var dc = DependencyContext.Load(this.loader.LoadDefaultAssembly());

            this.ComponentsAssemblies = dc.GetRuntimeAssemblyNames(RuntimeEnvironment.GetRuntimeIdentifier())
                .Where(n => n != entryAssemblyName)
                .Select(this.loader.LoadAssembly)
                .ToArray();

            foreach (var assembly in this.ComponentsAssemblies)
            {
                var componentFactoryTypes = assembly
                    .GetExportedTypes()
                    .Where(t => t.GetInterfaces().Any(i => typeof(IHostableComponentFactory).IsAssignableFrom(i)))
                    .ToArray();

                if (!componentFactoryTypes.Any())
                {
                    continue;
                }

                var instances = componentFactoryTypes
                    .Select(Activator.CreateInstance)
                    .Cast<IHostableComponentFactory>()
                    .ToArray();

                this.Factories.AddRange(instances);
            }

            return this;
        }

        public StartResult Run(IConfiguration configuration, CancellationToken cancellationToken)
        {
            var cfg = ComponentConfiguration.Create(configuration);

            this.SetSharedLibrariesConfiguration(configuration);

            var componentLoader = new ComponentAssemblyLoader(this.loader);

            var components = this.Factories
                .Select(f => f.CreateComponent(componentLoader, cfg))
                .ToArray();

            var task = Task.Factory.StartNew(
                () =>
                    {
                        foreach (var component in components)
                        {
                            component.Start();
                        }
                    },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            return new StartResult
                {
                    StartTask = task,
                    Components = components
                };
        }

        private void SetSharedLibrariesConfiguration(IConfiguration configuration)
        {
            foreach (var componentAssembly in this.ComponentsAssemblies)
            {
                var configurationFactory = componentAssembly.GetExportedTypes()
                    .FirstOrDefault(x => x.Name == "ConfigurationProvider");

                if (configurationFactory != null)
                {
                    var method = configurationFactory.GetMethod("Set");

                    if (method != null && method.IsStatic)
                    {
                        var parameters = method.GetParameters();

                        if (parameters.Length == 1)
                        {
                            var libraryName = componentAssembly.GetName().Name.ToLower();
                            var configType = parameters[0].ParameterType;

                            var sharedLibConfiguration =
                                configuration
                                    .GetSection($"shared-libraries:{libraryName}")?
                                    .Get(configType);

                            method.Invoke(
                                null,
                                new []
                                    {
                                        sharedLibConfiguration
                                    });

                            Logger.Info(m => m($"Set configuration for shared library {libraryName}"));
                        }
                    }
                }
            }
        }

        public class StartResult
        {
            public IHostableComponent[] Components { get; set; }

            public Task StartTask { get; set; }
        }
    }
}