using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Client;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Microsoft.Extensions.Logging;

namespace DynamicChatIntegration
{
    internal class TwitchChatListener
    {
        private Settings _Settings;
        private ILogger _Logger;
        private CommandProcessor _CommandProcessor;

        private HashSet<string> _AllowedDebugUsernames;

        private TwitchClient _Client;

        public TwitchChatListener(Settings settings, ILogger logger, CommandProcessor commandProcessor)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _CommandProcessor = commandProcessor ?? throw new ArgumentNullException(nameof(commandProcessor));
        }

        public bool Init()
        {
            if (string.IsNullOrWhiteSpace(_Settings.AccessToken))
            {
                _Logger.LogError("Please set an access token in appsettings.json: https://id.twitch.tv/oauth2/authorize?response_type=token&client_id=z6b0tfei2a12oebv47aat3vckndozj&redirect_uri=https://twitchapps.com/tokengen/&scope=chat%3Aread");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_Settings.BotUsername))
            {
                _Logger.LogError("Please set a bot username in appsettings.json.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_Settings.Channel))
            {
                _Logger.LogError("Please set a channel name to read from in appsettings.json.");
                return false;
            }

            ConnectionCredentials credentials = new ConnectionCredentials(_Settings.BotUsername, _Settings.AccessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 100,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            _Client = new TwitchClient(customClient);
            _Client.Initialize(credentials, _Settings.Channel);

            _Client.OnLog += Client_OnLog;
            _Client.OnJoinedChannel += Client_OnJoinedChannel;
            _Client.OnMessageReceived += Client_OnMessageReceived;
            _Client.OnConnected += Client_OnConnected;

            LoadAllowedUsers();
            _Settings.RegisterChangeCallback(_ => { LoadAllowedUsers(); }, null);

            return true;
        }

        public void Connect()
        {
            _Client.Connect();
        }

        public void Disconnect()
        {
            _Client.Disconnect();
        }

        private void LoadAllowedUsers()
        {
            _Logger.LogDebug("Loading allowed users");
            _AllowedDebugUsernames = new HashSet<string>();
            foreach (string username in _Settings.DebugCommandsAllowedUsers) {
                var sanitized = username.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    _AllowedDebugUsernames.Add(sanitized);
                }
            }
            _Logger.LogInformation("Debug command access is currently: {status}",
                _Settings.RestrictDebugCommandsToAllowedUsers ? "Restricted to allowed users only" : "Unrestricted (anyone can access)");
            if (_Settings.RestrictDebugCommandsToAllowedUsers)
            {
                _Logger.LogInformation("Loaded allowlist: {users}", string.Join(", ", _AllowedDebugUsernames));
            }
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            _Logger.LogInformation(e.Data);
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            _Logger.LogInformation("Connected to {channel}", e.AutoJoinChannel);
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            _Logger.LogInformation("Joined {channel}", e.Channel);
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var user = e.ChatMessage.Username;
            var msg = e.ChatMessage.Message;
            _Logger.LogDebug("{user}: {msg}", user, msg);

            // If we are restricting debug commands, assert user is in allowlist
            bool allowDebugCmds;
            if (_Settings.RestrictDebugCommandsToAllowedUsers)
            {
                allowDebugCmds = _AllowedDebugUsernames.Contains(user.ToLower());
            }
            else
            {
                allowDebugCmds = true;
            }

            if (_CommandProcessor.IsValidCommand(msg, allowDebugCmds))
            {
                _Logger.LogInformation("{user} invoked `{cmd}`", user, msg);
                _CommandProcessor.ProcessCommand(msg, allowDebugCmds);
            }
        }
    }
}
