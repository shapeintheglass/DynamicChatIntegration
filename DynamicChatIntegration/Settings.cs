using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace DynamicChatIntegration
{
    internal class Settings
    {
        private IConfiguration _Configuration;
        private IChangeToken _ChangeToken;

        private const string TwitchSettingsSectionName = "TwitchSettings";
        private const string CommandSettingsSectionName = "CommandSettings";
        private const string IniFileSettingsSectionName = "IniFileSettings";
        private const string DebugCommandSettingsSectionName = "DebugCommandSettings";

        public Settings(IConfiguration configuration)
        {
            _Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _ChangeToken = _Configuration.GetReloadToken();
        }

        public void RegisterChangeCallback(Action<object?> callback, object? state)
        {
            _ChangeToken.RegisterChangeCallback(callback, state);
        }

        internal string AccessToken => _Configuration[$"{TwitchSettingsSectionName}:AccessToken"] ?? "";

        internal string Channel => _Configuration[$"{TwitchSettingsSectionName}:Channel"] ?? "";

        internal string BotUsername => _Configuration[$"{TwitchSettingsSectionName}:BotUsername"] ?? "";

        internal bool RestrictDebugCommandsToAllowedUsers => bool.TryParse(
            _Configuration[$"{TwitchSettingsSectionName}:RestrictDebugCommandsToAllowedUsers"],
            out var parsed) ? parsed : false;

        internal string[] DebugCommandsAllowedUsers =>
            _Configuration.GetSection(TwitchSettingsSectionName).GetSection("DebugCommandsAllowedUsers").Get<string[]>()
            ?? Array.Empty<string>();

        internal string[][] Commands => 
            _Configuration.GetSection(CommandSettingsSectionName).GetSection("Commands").Get<string[][]>()
            ?? Array.Empty<string[]>();

        internal string OriginalIniPath => _Configuration[$"{IniFileSettingsSectionName}:OriginalIniPath"] ?? "";

        internal string ModifiedIniPath => _Configuration[$"{IniFileSettingsSectionName}:ModifiedIniPath"] ?? "";

        internal string CommandPrefix => _Configuration[$"{DebugCommandSettingsSectionName}:CommandPrefix"] ?? "";

        internal string CommandReset => _Configuration[$"{DebugCommandSettingsSectionName}:CommandReset"] ?? "";

        internal string CommandDelimiter => _Configuration[$"{DebugCommandSettingsSectionName}:CommandDelimiter"] ?? "";

        internal string CommandSetRegex => _Configuration[$"{DebugCommandSettingsSectionName}:CommandSetRegex"] ?? "";

        internal string CommandGetRegex => _Configuration[$"{DebugCommandSettingsSectionName}:CommandGetRegex"] ?? "";

        internal int MaxMessageLength =>
            int.TryParse(_Configuration[$"{DebugCommandSettingsSectionName}:MaxMessageLength"],
                out var parsed) ? parsed : int.MaxValue;
    }
}
