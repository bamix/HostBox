using Common.Logging;
using Microsoft.Extensions.Hosting;

namespace HostBox
{
    public class ApplicationLifetimeLogger
    {
        private static readonly ILog Logger = LogManager.GetLogger<ApplicationLifetimeLogger>();
        
        public ApplicationLifetimeLogger(IHostApplicationLifetime lifetime)
        {
            lifetime.ApplicationStarted.Register(OnStarted);
            lifetime.ApplicationStopping.Register(OnStopping);
            lifetime.ApplicationStopped.Register(OnStopped);
        }

        private static void OnStarted()
        {
            Logger.Trace("Application started.");
        }

        private static void OnStopping()
        {
            Logger.Trace("Application stopping.");
        }

        private static void OnStopped()
        {
            Logger.Trace("Application stopped.");
        }
    }
}