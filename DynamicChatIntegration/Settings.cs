using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
namespace DynamicChatIntegration
{
    public class Settings
    {
        public string[] DebugCommandsAllowedUsers { get; set; }

        public string[][] Commands { get; set; }

        public string CommandSetRegex { get; set; }

        public string CommandGetRegex { get; set; }

        public string AccessToken { get; set; }

        public string Channel { get; set; }

        public string BotUsername { get; set; }

        public bool PostResponsesInChat { get; set; }

        public bool RestrictDebugCommandsToAllowedUsers { get; set; }

        public string OriginalIniPath { get; set; }

        public string ModifiedIniPath { get; set; }

        public string CommandPrefix { get; set; }

        public string CommandReset { get; set; }

        public string CommandDelimiter { get; set; }

        public int MaxMessageLength { get; set; } = int.MaxValue;
    }
}
