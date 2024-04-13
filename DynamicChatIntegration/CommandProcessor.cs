using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace DynamicChatIntegration
{
    internal class CommandProcessor
    {
        private readonly IOptionsMonitor<Settings> _Settings;
        private readonly ILogger _Logger;

        private readonly Action _ResetFunc;
        private readonly Func<string, string, string> _GetFunc;
        private readonly Action<string, string, string> _SetFunc;

        private const string SectionGroupName = "section";
        private const string PropertyGroupName = "property";
        private const string ValueGroupName = "value";

        private IDictionary<string, string> _Commands;
        private Regex _CompiledSetRegex;
        private Regex _CompiledGetRegex;

        public CommandProcessor(IOptionsMonitor<Settings> settings, ILogger logger, Action resetFunc,
            Func<string, string, string> getFunc, Action<string, string, string> setFunc)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ResetFunc = resetFunc ?? throw new ArgumentNullException(nameof(resetFunc));
            _GetFunc = getFunc ?? throw new ArgumentNullException(nameof(getFunc));
            _SetFunc = setFunc ?? throw new ArgumentNullException(nameof(setFunc));
            LoadCommandsAndRegex(_Settings.CurrentValue);
            _Settings.OnChange(LoadCommandsAndRegex);
        }

        private void LoadCommandsAndRegex(Settings newSettings)
        {
            _Logger.LogDebug("Loading commands dictionary...");
            var cmdStringArray = newSettings.Commands;
            _Commands = new Dictionary<string, string>();
            foreach (string[] sub in cmdStringArray)
            {
                if (sub.Length < 2)
                {
                    _Logger.LogDebug("Invalid command found. Requires at least two values");
                    continue;
                }
                _Commands[sub[0].Trim().ToLower()] = sub[1].Trim();
            }
            var cmdsString = string.Join("\n", _Commands.Select(pair => $"{pair.Key.PadRight(20)}==>   {pair.Value}"));
            _Logger.LogInformation("Successfully loaded commands:\n\n{cmd}\n", cmdsString);

            _Logger.LogDebug("Loading compiled regular expressions...");
            try
            {
                _CompiledSetRegex = new Regex(newSettings.CommandSetRegex, RegexOptions.Compiled);
                _CompiledGetRegex = new Regex(newSettings.CommandGetRegex, RegexOptions.Compiled);
            }
            catch (Exception e)
            {
                _Logger.LogError(e, "Invalid regex. Please fix and restart.");
                throw;
            }
            _Logger.LogDebug("Successfully loaded regex settings");
        }

        public bool IsValidCommand(string line, bool allowDebugCmd)
        {
            // Sanity check to filter out badly formatted lines
            if (string.IsNullOrWhiteSpace(line) || line.Length > _Settings.CurrentValue.MaxMessageLength)
            {
                _Logger.LogDebug("Invalid command: Empty or too long");
                return false;
            }

            // Sanitize
            line = line.Trim();

            // Reject if line contains non-ascii characters
            if (!line.All(char.IsAscii))
            {
                _Logger.LogDebug("Invalid command: Contains one or more non-ascii characters");
                return false;
            }

            // Check if line matches known command
            var caseInsensitive = line.ToLower();
            if (_Commands.TryGetValue(caseInsensitive, out string? _))
            {
                _Logger.LogDebug("Valid command: Matches known command {cmd}", caseInsensitive);
                return true;
            }

            // Check if line matches debug command prefix and debug commands are allowed
            if (allowDebugCmd &&
                line.StartsWith(_Settings.CurrentValue.CommandPrefix + _Settings.CurrentValue.CommandDelimiter,
                StringComparison.InvariantCultureIgnoreCase))
            {
                _Logger.LogDebug("Valid command: Debug commands are allowed and matches debug command");
                return true;
            }

            return false;
        }

        public void ProcessCommand(string line, bool allowDebugCmd)
        {
            // Sanitize
            line = line.Trim();

            // Pass through known substitutions
            var caseInsensitive = line.ToLower();
            if (_Commands.TryGetValue(caseInsensitive, out string? substitutedDebugCommand))
            {
                _Logger.LogDebug("Executing `{cmd}` as `{sub}`", caseInsensitive, substitutedDebugCommand);
                ProcessDebugCommand(substitutedDebugCommand);
            }
            else if (allowDebugCmd)
            {
                ProcessDebugCommand(line);
            }
        }

        private void ProcessDebugCommand(string cmd)
        {
            // Sanity check: Only respond to commands that start with the given prefix
            if (!cmd.StartsWith(_Settings.CurrentValue.CommandPrefix + _Settings.CurrentValue.CommandDelimiter, StringComparison.InvariantCultureIgnoreCase))
            {
                _Logger.LogDebug("Did not recognize debug command. Did not start with [{prefix}]", _Settings.CurrentValue.CommandPrefix + _Settings.CurrentValue.CommandDelimiter);
                return;
            }

            // Split and process remainder
            var cmdSubstring = cmd.Substring(_Settings.CurrentValue.CommandPrefix.Length + _Settings.CurrentValue.CommandDelimiter.Length).Trim();

            if (cmdSubstring.Equals(_Settings.CurrentValue.CommandReset, StringComparison.InvariantCultureIgnoreCase))
            {
                _Logger.LogDebug("Recognized as reset command.");
                _ResetFunc();
                return;
            }

            // Process read command
            var readCmdMatch = _CompiledGetRegex.Match(cmdSubstring);
            if (readCmdMatch.Success)
            {
                var section = readCmdMatch.Groups[SectionGroupName].Value;
                var property = readCmdMatch.Groups[PropertyGroupName].Value;
                var value = _GetFunc(section, property);
                _Logger.LogDebug("Recognized as get command. [{section}]{property} = {value}", section, property, value);
                return;
            }

            // Process write command
            var setCmdMatch = _CompiledSetRegex.Match(cmdSubstring);
            if (setCmdMatch.Success)
            {
                var section = setCmdMatch.Groups[SectionGroupName].Value;
                var property = setCmdMatch.Groups[PropertyGroupName].Value;
                var value = setCmdMatch.Groups[ValueGroupName].Value;
                _SetFunc(section, property, value);
                _Logger.LogDebug("Recognized as set command. [{section}]{property} = {value}", section, property, value);
                return;
            }
        }
    }
}
