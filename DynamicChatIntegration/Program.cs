using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace DynamicChatIntegration
{
    public class Program
    {

        public static void Main(string[] args)
        {
            var argsAsSet = args.ToHashSet();
            if (argsAsSet.Contains("--help") || argsAsSet.Contains("-h"))
            {
                Console.WriteLine($"Usage: {args[0]} [--debug] [--verbose]");
                Console.WriteLine("--debug: Read commands from stdin for debugging purposes");
                Console.WriteLine("--verbose: Print verbose logs");
                return;
            }

            bool debugMode = argsAsSet.Contains("--debug") || argsAsSet.Contains("-d");
            bool verboseMode = argsAsSet.Contains("--verbose") || argsAsSet.Contains("-v");

            using ILoggerFactory factory = LoggerFactory.Create(builder => builder
                .AddSimpleConsole(options =>
                { 
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                })
                .SetMinimumLevel(verboseMode ? LogLevel.Debug : LogLevel.Information));

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var configuration = builder.Build();

           var serviceProvider = new ServiceCollection()
                .Configure<Settings>(configuration.GetSection("Settings"))
                .BuildServiceProvider();

            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();

            IniFileUpdater fileUpdater = new IniFileUpdater(optionsMonitor, factory.CreateLogger<IniFileUpdater>());
            CommandProcessor commandProcessor = new CommandProcessor(optionsMonitor, factory.CreateLogger<CommandProcessor>(),
                fileUpdater.Restore, fileUpdater.GetValue, fileUpdater.SetValue);

            if (debugMode)
            {
                Console.WriteLine("Running in debug mode. Commands will be read from console.");
                Console.WriteLine("Enter an empty line to end application.");
                while (true)
                {
                    Console.Write("> ");
                    var cmd = Console.ReadLine();
                    if (string.IsNullOrEmpty(cmd))
                    {
                        break;
                    }
                    commandProcessor.ProcessCommand(cmd, true);
                }
                return;
            }

            var twitchChatListener = new TwitchChatListener(optionsMonitor,
                factory.CreateLogger<TwitchChatListener>(), commandProcessor);
            if (twitchChatListener.Init())
            {
                twitchChatListener.Connect();
            }
            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }
    }
}