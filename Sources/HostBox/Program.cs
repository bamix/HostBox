using System;
using System.Threading.Tasks;

using Common.Logging;

namespace HostBox
{
    internal class Program
    {
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
                    var host = CreateHost(commandLineArgs);

                    Logger.Trace(m => m("Starting hostbox."));
                    await host.Run();
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

        private static IHosting CreateHost(CommandLineArgs commandLineArgs)
        {
            if (commandLineArgs.Web)
            {
                return new WebHosting(commandLineArgs);
            }

            return new ConsoleHosting(commandLineArgs);
        }
    }
}
