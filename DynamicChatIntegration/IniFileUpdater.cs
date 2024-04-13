using Microsoft.Extensions.Logging;
using PeanutButter.INI;

namespace DynamicChatIntegration
{
    internal class IniFileUpdater
    {
        private readonly INIFile _File;

        private readonly Settings _Settings;
        private readonly ILogger _Logger;

        public IniFileUpdater(Settings settings, ILogger<IniFileUpdater> logger)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Sanity check required settings at startup
            AssertBothIniPathsAreDefined();

            // Create original file if it does not already exist
            if (!File.Exists(_Settings.OriginalIniPath))
            {
                _Logger.LogInformation("Creating empty {originalFile} as it does not currently exist", _Settings.OriginalIniPath);
                File.CreateText(_Settings.OriginalIniPath);
            }

            // Create modified file from original if it does not already exist
            if (!File.Exists(_Settings.ModifiedIniPath))
            {
                _Logger.LogInformation("Creating {modifiedFile} as it does not currently exist", _Settings.ModifiedIniPath);
                Restore();
            }

            _File = new INIFile(_Settings.ModifiedIniPath);
        }

        public void Restore()
        {
            AssertBothIniPathsAreDefined();
            _Logger.LogInformation("Restoring {modifiedFile} from {originalFile}", _Settings.ModifiedIniPath, _Settings.OriginalIniPath);
            File.Copy(_Settings.OriginalIniPath, _Settings.ModifiedIniPath, overwrite: true);
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
            if (string.IsNullOrEmpty(_Settings.OriginalIniPath) || string.IsNullOrEmpty(_Settings.ModifiedIniPath))
            {
                throw new InvalidOperationException("Please make sure OriginalIniPath and ModifiedIniPath are set in appsettings.json");
            }
        }
    }
}
