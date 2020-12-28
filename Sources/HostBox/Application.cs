using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Common.Logging;

using HostBox.Loading;

using Microsoft.Extensions.Hosting;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace HostBox
{
    public class Application : IHostedService
    {
        private static readonly TaskCompletionSource<object> DelayStart = new TaskCompletionSource<object>();

        private static readonly TaskCompletionSource<object> DelayStop = new TaskCompletionSource<object>();

        private readonly ILog logger;

        private readonly IConfiguration appConfiguration;

        private ComponentsLoader.StartResult description;

        public Application(IConfiguration appConfiguration, ComponentConfig config, IApplicationLifetime lifetime)
        {
            if (lifetime == null)
            {
                throw new ArgumentNullException(nameof(lifetime));
            }

            this.appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));

            this.ComponentConfig = config;

            this.logger = config.LoggerFactory.Invoke(this.GetType());

            lifetime.ApplicationStarted.Register(this.OnStarted);
            lifetime.ApplicationStopping.Register(this.OnStopping);
            lifetime.ApplicationStopped.Register(this.OnStopped);
        }

        public ComponentConfig ComponentConfig { get; }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(this.ComponentConfig.Path));

            cancellationToken.Register(() => DelayStart.TrySetCanceled());

            this.description = this.LoadAndRunComponents(cancellationToken);

            this.description.StartTask
                .ContinueWith(
                    t =>
                        {
                            DelayStart.TrySetException(t.Exception);
                            return DelayStart;
                        },
                    cancellationToken,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default)
                .ContinueWith(
                    t =>
                        {
                            DelayStart.TrySetResult(null);
                            return DelayStart;
                        },
                    cancellationToken,
                    TaskContinuationOptions.NotOnFaulted,
                    TaskScheduler.Default);

            return DelayStart.Task;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => DelayStop.TrySetCanceled());

            var componentStopTask = Task.Factory
                .StartNew(
                    () =>
                        {
                            foreach (var component in this.description.Components)
                            {
                                component.Stop();
                            }
                        },
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                .ContinueWith(
                    t =>
                        {
                            DelayStop.TrySetException(t.Exception);
                            return DelayStop;
                        },
                    cancellationToken,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default)
                .ContinueWith(
                    t =>
                        {
                            DelayStop.TrySetResult(null);
                            return DelayStop;
                        },
                    cancellationToken,
                    TaskContinuationOptions.NotOnFaulted,
                    TaskScheduler.Default);

            componentStopTask.ContinueWith(t => DelayStop.TrySetResult(null), cancellationToken);

            return DelayStop.Task;
        }

        private void OnStarted()
        {
            this.logger.Trace("Application started.");
        }

        private void OnStopping()
        {
            this.logger.Trace("Application stopping.");
        }

        private void OnStopped()
        {
            this.logger.Trace("Application stopped.");
        }

        private ComponentsLoader.StartResult LoadAndRunComponents(CancellationToken cancellationToken)
        {
            return new ComponentsLoader(this.ComponentConfig)
                .Load()
                .Run(this.appConfiguration, cancellationToken);
        }
    }
}