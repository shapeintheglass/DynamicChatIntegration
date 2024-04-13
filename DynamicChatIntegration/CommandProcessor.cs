using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DynamicChatIntegration
{
    internal class CommandProcessor
    {
        private readonly Settings _Settings;
        private readonly ILogger _Logger;

        private readonly Action _ResetFunc;
        private readonly Func<string, string, string> _GetFunc;
        private readonly Action<string, string, string> _SetFunc;

        private const string SectionGroupName = "section";
        private const string PropertyGroupName = "property";
        private const string ValueGroupName = "value";

        private Regex _SetRegex = new Regex("^(\\[(?<section>[\\w_\\-\\.]+)\\])? ?(?<property>[\\w_\\-\\.]+) ?= ?(?<value>[\\x00-\\x7F]*)$", RegexOptions.Compiled);
        private Regex _GetRegex = new Regex("^(\\[(?<section>[\\w_\\-\\.]+)\\])? ?(?<property>[\\w_\\-\\.]+)$", RegexOptions.Compiled);
        private Dictionary<string, string> _Substitutions;

        public CommandProcessor(Settings settings, ILogger logger, Action resetFunc,
            Func<string, string, string> getFunc, Action<string, string, string> setFunc)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ResetFunc = resetFunc ?? throw new ArgumentNullException(nameof(resetFunc));
            _GetFunc = getFunc ?? throw new ArgumentNullException(nameof(getFunc));
            _SetFunc = setFunc ?? throw new ArgumentNullException(nameof(setFunc));

            LoadRegexAndSubstitutions();

            _Settings.RegisterChangeCallback(_ => { LoadRegexAndSubstitutions(); }, null);
        }

        private void LoadRegexAndSubstitutions()
        {
            _Logger.LogDebug("Loading regex and substitutions");
            try
            {
                _SetRegex = new Regex(_Settings.CommandSetRegex, RegexOptions.Compiled);
                _GetRegex = new Regex(_Settings.CommandGetRegex, RegexOptions.Compiled);
            }
            catch (Exception e)
            {
                _Logger.LogError(e, "Invalid regex used in settings. Please verify and restart.");
                throw;
            }
            _Logger.LogDebug("Successfully loaded regex settings");

            _Substitutions = [];
            foreach (string[] sub in _Settings.Commands)
            {
                if (sub.Length < 2)
                {
                    _Logger.LogDebug("Invalid command found. Requires at least two values");
                    continue;
                }
                _Substitutions[sub[0].Trim().ToLower()] = sub[1].Trim();
            }
            var cmdsString = string.Join("\n", _Substitutions.Select(pair => $"{pair.Key.PadRight(20)}==>   {pair.Value}"));
            _Logger.LogInformation("Successfully loaded commands:\n\n{cmd}\n", cmdsString);
        }

        public bool IsValidCommand(string line, bool allowDebugCmd)
        {
            // Sanity check to filter out badly formatted lines
            if (string.IsNullOrWhiteSpace(line) || line.Length > _Settings.MaxMessageLength)
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
            if (_Substitutions.TryGetValue(caseInsensitive, out string? _))
            {
                _Logger.LogDebug("Valid command: Matches known command {cmd}", caseInsensitive);
                return true;
            }

            // Check if line matches debug command prefix and debug commands are allowed
            if (allowDebugCmd &&
                line.StartsWith(_Settings.CommandPrefix + _Settings.CommandDelimiter,
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
            if (_Substitutions.TryGetValue(caseInsensitive, out string? substitutedDebugCommand))
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
            if (!cmd.StartsWith(_Settings.CommandPrefix + _Settings.CommandDelimiter, StringComparison.InvariantCultureIgnoreCase))
            {
                _Logger.LogDebug("Did not recognize debug command. Did not start with [{prefix}]", _Settings.CommandPrefix + _Settings.CommandDelimiter);
                return;
            }

            // Split and process remainder
            var cmdSubstring = cmd.Substring(_Settings.CommandPrefix.Length + _Settings.CommandDelimiter.Length).Trim();

            if (cmdSubstring.Equals(_Settings.CommandReset, StringComparison.InvariantCultureIgnoreCase))
            {
                _Logger.LogInformation("Recognized as reset command.");
                _ResetFunc();
                return;
            }

            // Process read command
            var readCmdMatch = _GetRegex.Match(cmdSubstring);
            if (readCmdMatch.Success)
            {
                var section = readCmdMatch.Groups[SectionGroupName].Value;
                var property = readCmdMatch.Groups[PropertyGroupName].Value;
                var value = _GetFunc(section, property);
                _Logger.LogInformation("Recognized as get command. [{section}]{property} = {value}", section, property, value);
                return;
            }

            // Process write command
            var setCmdMatch = _SetRegex.Match(cmdSubstring);
            if (setCmdMatch.Success)
            {
                var section = setCmdMatch.Groups[SectionGroupName].Value;
                var property = setCmdMatch.Groups[PropertyGroupName].Value;
                var value = setCmdMatch.Groups[ValueGroupName].Value;
                _SetFunc(section, property, value);
                _Logger.LogInformation("Recognized as set command. [{section}]{property} = {value}", section, property, value);
                return;
            }
        }
    }
}
