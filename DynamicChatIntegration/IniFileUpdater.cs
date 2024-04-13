using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PeanutButter.INI;

namespace DynamicChatIntegration
{
    internal class IniFileUpdater
    {
        private readonly INIFile _File;

        private readonly IOptionsMonitor<Settings> _Settings;
        private readonly ILogger _Logger;

        public IniFileUpdater(IOptionsMonitor<Settings> settings, ILogger<IniFileUpdater> logger)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Sanity check required settings at startup
            AssertBothIniPathsAreDefined();

            // Create original file if it does not already exist
            if (!File.Exists(_Settings.CurrentValue.OriginalIniPath))
            {
                _Logger.LogInformation("Creating empty {originalFile} as it does not currently exist", _Settings.CurrentValue.OriginalIniPath);
                File.CreateText(_Settings.CurrentValue.OriginalIniPath);
            }

            // Create modified file from original if it does not already exist
            if (!File.Exists(_Settings.CurrentValue.ModifiedIniPath))
            {
                _Logger.LogInformation("Creating {modifiedFile} as it does not currently exist", _Settings.CurrentValue.ModifiedIniPath);
                File.Copy(_Settings.CurrentValue.OriginalIniPath, _Settings.CurrentValue.ModifiedIniPath, overwrite: true);
            }

            _File = new INIFile(_Settings.CurrentValue.ModifiedIniPath);
        }

        public void Restore()
        {
            AssertBothIniPathsAreDefined();
            _Logger.LogInformation("Restoring {modifiedFile} from {originalFile}", _Settings.CurrentValue.ModifiedIniPath, _Settings.CurrentValue.OriginalIniPath);
            File.Copy(_Settings.CurrentValue.OriginalIniPath, _Settings.CurrentValue.ModifiedIniPath, overwrite: true);
            _File.Reload();
        }

        public string GetValue(string section, string property)
        {
            return _File.GetValue(section, property);
        }

        public void SetValue(string section, string property, string value)
        {
            _File.SetValue(section, property, value);
            _File.Persist();
        }

        private void AssertBothIniPathsAreDefined()
        {
            if (string.IsNullOrEmpty(_Settings.CurrentValue.OriginalIniPath) || string.IsNullOrEmpty(_Settings.CurrentValue.ModifiedIniPath))
            {
                throw new InvalidOperationException("Please make sure OriginalIniPath and ModifiedIniPath are set in appsettings.json");
            }
        }
    }
}
