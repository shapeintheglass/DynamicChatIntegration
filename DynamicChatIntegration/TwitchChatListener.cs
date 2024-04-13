using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Client;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DynamicChatIntegration
{
    internal class TwitchChatListener
    {
        private IOptionsMonitor<Settings> _Settings;
        private ILogger _Logger;
        private CommandProcessor _CommandProcessor;

        private TwitchClient _Client;
        private HashSet<string> _AllowedUsers;

        public TwitchChatListener(IOptionsMonitor<Settings> settings, ILogger logger, CommandProcessor commandProcessor)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _CommandProcessor = commandProcessor ?? throw new ArgumentNullException(nameof(commandProcessor));
            LoadAllowedUsers(_Settings.CurrentValue);
            _Settings.OnChange(LoadAllowedUsers);
        }

        private void LoadAllowedUsers(Settings newSettings)
        {
            _Logger.LogDebug("Loading allowed users set...");
            _AllowedUsers = new HashSet<string>();
            foreach (string username in newSettings.DebugCommandsAllowedUsers)
            {
                var sanitized = username.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    _AllowedUsers.Add(sanitized);
                }
            }

            if (newSettings.RestrictDebugCommandsToAllowedUsers)
            {
                _Logger.LogInformation("Debug commands are restricted to: {users}", string.Join(", ", _AllowedUsers));
            }
            else
            {
                _Logger.LogInformation("Debug commands are available to all users.");
            }
        }

        public bool Init()
        {
            if (string.IsNullOrWhiteSpace(_Settings.CurrentValue.AccessToken))
            {
                _Logger.LogError("Please set an access token in appsettings.json: https://twitchtokengenerator.com/quick/JqwV9wWVq0");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_Settings.CurrentValue.BotUsername))
            {
                _Logger.LogError("Please set a bot username in appsettings.json.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_Settings.CurrentValue.Channel))
            {
                _Logger.LogError("Please set a channel name to read from in appsettings.json.");
                return false;
            }

            ConnectionCredentials credentials = new ConnectionCredentials(_Settings.CurrentValue.BotUsername, _Settings.CurrentValue.AccessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 100,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            _Client = new TwitchClient(customClient);
            

            _Client.Initialize(credentials, _Settings.CurrentValue.Channel);

            _Client.OnLog += Client_OnLog;
            _Client.OnJoinedChannel += Client_OnJoinedChannel;
            _Client.OnMessageReceived += Client_OnMessageReceived;
            _Client.OnConnected += Client_OnConnected;

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
            if (_Settings.CurrentValue.RestrictDebugCommandsToAllowedUsers)
            {
                allowDebugCmds = _Settings.CurrentValue.DebugCommandsAllowedUsers.Contains(user.ToLower());
            }
            else
            {
                allowDebugCmds = true;
            }

            if (_CommandProcessor.IsValidCommand(msg, allowDebugCmds))
            {
                _Logger.LogInformation("{user} invoked `{cmd}`", user, msg);
                var resp = _CommandProcessor.ProcessCommand(msg, allowDebugCmds);
                if (_Settings.CurrentValue.PostResponsesInChat && !string.IsNullOrEmpty(resp))
                {
                    _Client.SendMessage(e.ChatMessage.Channel, resp);
                }
            }
        }
    }
}
