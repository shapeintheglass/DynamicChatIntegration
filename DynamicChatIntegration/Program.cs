using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DynamicChatIntegration
{
    internal class Program
    {
        public class Options
        {
            [Option('d', "debug", Required = false, HelpText = "Read commands from console for debug purposes")]
            public bool Debug { get; set; }

            [Option('v', "verbose", Required = false, HelpText = "Include verbose logging messages")]
            public bool Verbose { get; set; }
        }


        public static void Main(string[] args)
        {
            bool runInDebugMode = false;
            bool verbose = false;
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(
                o => { runInDebugMode = o.Debug; verbose = o.Verbose; });

            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information));
            ILogger logger = factory.CreateLogger("Program");

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var configuration = builder.Build();
            var settings = new Settings(configuration);

            IniFileUpdater fileUpdater = new IniFileUpdater(settings, factory.CreateLogger<IniFileUpdater>());
            CommandProcessor commandProcessor = new CommandProcessor(settings, factory.CreateLogger<CommandProcessor>(),
                fileUpdater.Restore, fileUpdater.GetValue, fileUpdater.SetValue);

            if (runInDebugMode)
            {
                Console.WriteLine("Running in debug mode. Commands will be read from console.");
                Console.WriteLine("Enter an empty line to end application.");
                while (true)
                {
                    var cmd = Console.ReadLine();
                    if (string.IsNullOrEmpty(cmd))
                    {
                        break;
                    }
                    commandProcessor.ProcessCommand(cmd, true);
                }
                return;
            }


            var twitchChatListener = new TwitchChatListener(settings,
                factory.CreateLogger<TwitchChatListener>(), commandProcessor);
            if (twitchChatListener.Init())
            {
                try
                {
                    twitchChatListener.Connect();
                }
                finally
                {
                    twitchChatListener.Disconnect();
                }
            }
            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }
    }
}