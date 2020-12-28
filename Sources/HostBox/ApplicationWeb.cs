using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using HostBox.Borderline;
using HostBox.Configuration;
using HostBox.Loading;

using McMaster.NETCore.Plugins;

using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyModel;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace HostBox
{
    public class ApplicationWeb
    {
        private Assembly[] componentAssemblies;

        private readonly List<IHostableComponentFactory> factories = new List<IHostableComponentFactory>();

        private readonly PluginLoader loader;

        public ApplicationWeb(ComponentConfig config)
        {
            this.ComponentConfig = config;
            this.loader = PluginLoader.CreateFromAssemblyFile(
                this.ComponentConfig.Path,
                new Type[2]
                    {
                        typeof(Borderline.IConfiguration),
                        typeof(DependencyContext)
                    },
                this.ComponentConfig.SharedLibraryPath);
        }

        private ComponentConfig ComponentConfig { get; }

        public Type LoadComponents()
        {
            var entryAssembly = this.loader.LoadDefaultAssembly();
            var entryAssemblyName = entryAssembly.GetName(false);

            var dc = DependencyContext.Load(this.loader.LoadDefaultAssembly());

            this.componentAssemblies = dc.GetRuntimeAssemblyNames(RuntimeEnvironment.GetRuntimeIdentifier())
                .Where(n => n != entryAssemblyName)
                .Select(this.loader.LoadAssembly)
                .ToArray();

            foreach (var assembly in this.componentAssemblies)
            {
                var componentFactoryTypes = assembly
                    .GetExportedTypes()
                    .Where(
                        t => t.GetInterfaces()
                            .Any(i => i == typeof(IHostableComponentFactory)))
                    .ToArray();

                if (!componentFactoryTypes.Any())
                    continue;

                var instances = componentFactoryTypes
                    .Select(Activator.CreateInstance)
                    .Cast<IHostableComponentFactory>()
                    .ToArray();

                this.factories.AddRange(instances);
            }

            return entryAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.Name == "HostingStartup");
        }

        public void Run(IConfiguration appConfiguration)
        {
            var cfg = ComponentConfiguration.Create(appConfiguration);
            this.SetSharedLibrariesConfiguration(this.componentAssemblies, appConfiguration);
            var componentLoader = new ComponentAssemblyLoader(this.loader);

            var components = this.factories
                .Select(f => f.CreateComponent(componentLoader, cfg))
                .ToArray();

            foreach (var component in components)
            {
                component.Start();
            }
        }

        private void SetSharedLibrariesConfiguration(Assembly[] assemblies, IConfiguration appConfiguration)
        {
            foreach (var componentAssembly in assemblies)
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
                            var libraryName = componentAssembly.GetName()
                                .Name.ToLower();
                            var configType = parameters[0]
                                .ParameterType;

                            var sharedLibConfiguration =
                                appConfiguration
                                    .GetSection($"shared-libraries:{libraryName}")
                                    ?
                                    .Get(configType);

                            method.Invoke(
                                null,
                                new[]
                                    {
                                        sharedLibConfiguration
                                    });
                        }
                    }
                }
            }
        }
    }
}