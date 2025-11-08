using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IniParser;
using IniParser.Model;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Manages plugin configuration settings using INI file format
    /// Follows InfoPanel's standard pattern for configuration management
    /// </summary>
    public class ConfigurationService
    {
        #region Fields
        
        private readonly string? _configFilePath;
        private IniData? _config;
        private readonly FileIniDataParser _parser;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes the configuration service with the specified config file path
        /// </summary>
        /// <param name="configFilePath">Path to the INI configuration file</param>
        public ConfigurationService(string? configFilePath)
        {
            _configFilePath = configFilePath;
            _parser = new FileIniDataParser();
            LoadConfiguration();
        }
        
        #endregion

        #region Configuration Management
        
        /// <summary>
        /// Loads configuration from INI file
        /// </summary>
        private void LoadConfiguration()
        {
            if (string.IsNullOrEmpty(_configFilePath))
            {
                Debug.WriteLine("[ConfigurationService] Config file path is not set.");
                return;
            }

            try
            {
                if (!File.Exists(_configFilePath))
                {
                    CreateDefaultConfiguration();
                    Debug.WriteLine("[ConfigurationService] Config file created with default values.");
                }
                else
                {
                    // Load existing config with safe file reading
                    using var fileStream = new FileStream(_configFilePath, 
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(fileStream);
                    
                    string fileContent = reader.ReadToEnd();
                    _config = _parser.Parser.Parse(fileContent);
                    
                    Debug.WriteLine("[ConfigurationService] Configuration loaded successfully.");
                    
                    // Auto-add missing keys for backward compatibility
                    if (EnsureMissingKeys())
                    {
                        SaveConfiguration();
                        Debug.WriteLine("[ConfigurationService] Updated config with missing keys.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error loading configuration: {ex.Message}");
                CreateDefaultConfiguration();
            }
        }
        
        /// <summary>
        /// Creates default configuration file with sensible defaults
        /// </summary>
        private void CreateDefaultConfiguration()
        {
            if (string.IsNullOrEmpty(_configFilePath))
                return;

            try
            {
                _config = new IniData();
                
                // Debug Settings
                _config["Debug Settings"]["EnableDebugLogging"] = "false";
                _config["Debug Settings"]["DebugLogLevel"] = "Info";
                
                // Monitoring Settings
                _config["Monitoring Settings"]["MonitoringIntervalMs"] = "1000";
                _config["Monitoring Settings"]["EnableAutoReconnect"] = "true";
                _config["Monitoring Settings"]["ConnectionTimeoutMs"] = "5000";
                
                // Display Settings
                _config["Display Settings"]["ShowStatusMessages"] = "true";
                _config["Display Settings"]["ShowDetailedMetrics"] = "true";
                _config["Display Settings"]["UseMetricSystem"] = "true";
                
                // Steam Settings
                _config["Steam Settings"]["ApiKey"] = "<your-steam-api-key-here>";
                _config["Steam Settings"]["SteamId64"] = "<your-steam-id64-here>";
                _config["Steam Settings"]["UpdateIntervalSeconds"] = "30";
                _config["Steam Settings"]["EnableProfileMonitoring"] = "true";
                _config["Steam Settings"]["EnableLibraryMonitoring"] = "true";
                _config["Steam Settings"]["EnableCurrentGameMonitoring"] = "true";
                _config["Steam Settings"]["EnableAchievementMonitoring"] = "false";
                _config["Steam Settings"]["MaxRecentGames"] = "5";
                
                _parser.WriteFile(_configFilePath, _config);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error creating default configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Ensures all required configuration keys exist (for version upgrades)
        /// </summary>
        /// <returns>True if any keys were added</returns>
        private bool EnsureMissingKeys()
        {
            if (_config == null)
                return false;

            bool configUpdated = false;
            
            // Ensure Debug Settings
            if (!_config["Debug Settings"].ContainsKey("EnableDebugLogging"))
            {
                _config["Debug Settings"]["EnableDebugLogging"] = "false";
                configUpdated = true;
            }
            if (!_config["Debug Settings"].ContainsKey("DebugLogLevel"))
            {
                _config["Debug Settings"]["DebugLogLevel"] = "Info";
                configUpdated = true;
            }
            
            // Ensure Monitoring Settings
            if (!_config["Monitoring Settings"].ContainsKey("MonitoringIntervalMs"))
            {
                _config["Monitoring Settings"]["MonitoringIntervalMs"] = "1000";
                configUpdated = true;
            }
            if (!_config["Monitoring Settings"].ContainsKey("EnableAutoReconnect"))
            {
                _config["Monitoring Settings"]["EnableAutoReconnect"] = "true";
                configUpdated = true;
            }
            if (!_config["Monitoring Settings"].ContainsKey("ConnectionTimeoutMs"))
            {
                _config["Monitoring Settings"]["ConnectionTimeoutMs"] = "5000";
                configUpdated = true;
            }
            
            // Ensure Display Settings
            if (!_config["Display Settings"].ContainsKey("ShowStatusMessages"))
            {
                _config["Display Settings"]["ShowStatusMessages"] = "true";
                configUpdated = true;
            }
            if (!_config["Display Settings"].ContainsKey("ShowDetailedMetrics"))
            {
                _config["Display Settings"]["ShowDetailedMetrics"] = "true";
                configUpdated = true;
            }
            if (!_config["Display Settings"].ContainsKey("UseMetricSystem"))
            {
                _config["Display Settings"]["UseMetricSystem"] = "true";
                configUpdated = true;
            }
            
            // Ensure Steam Settings
            if (!_config["Steam Settings"].ContainsKey("ApiKey"))
            {
                _config["Steam Settings"]["ApiKey"] = "<your-steam-api-key-here>";
                configUpdated = true;
            }
            
            // Handle migration from old SteamId to SteamId64
            if (_config["Steam Settings"].ContainsKey("SteamId") && !_config["Steam Settings"].ContainsKey("SteamId64"))
            {
                // Migrate old SteamId to SteamId64
                var oldSteamId = _config["Steam Settings"]["SteamId"];
                _config["Steam Settings"]["SteamId64"] = oldSteamId;
                _config["Steam Settings"].RemoveKey("SteamId"); // Remove old key
                configUpdated = true;
            }
            
            if (!_config["Steam Settings"].ContainsKey("SteamId64"))
            {
                _config["Steam Settings"]["SteamId64"] = "<your-steam-id64-here>";
                configUpdated = true;
            }
            if (!_config["Steam Settings"].ContainsKey("UpdateIntervalSeconds"))
            {
                _config["Steam Settings"]["UpdateIntervalSeconds"] = "30";
                configUpdated = true;
            }
            if (!_config["Steam Settings"].ContainsKey("EnableProfileMonitoring"))
            {
                _config["Steam Settings"]["EnableProfileMonitoring"] = "true";
                configUpdated = true;
            }
            if (!_config["Steam Settings"].ContainsKey("EnableLibraryMonitoring"))
            {
                _config["Steam Settings"]["EnableLibraryMonitoring"] = "true";
                configUpdated = true;
            }
            if (!_config["Steam Settings"].ContainsKey("EnableCurrentGameMonitoring"))
            {
                _config["Steam Settings"]["EnableCurrentGameMonitoring"] = "true";
                configUpdated = true;
            }
            if (!_config["Steam Settings"].ContainsKey("EnableAchievementMonitoring"))
            {
                _config["Steam Settings"]["EnableAchievementMonitoring"] = "false";
                configUpdated = true;
            }
            if (!_config["Steam Settings"].ContainsKey("MaxRecentGames"))
            {
                _config["Steam Settings"]["MaxRecentGames"] = "5";
                configUpdated = true;
            }
            
            return configUpdated;
        }
        
        /// <summary>
        /// Saves current configuration to file
        /// </summary>
        public void SaveConfiguration()
        {
            if (string.IsNullOrEmpty(_configFilePath) || _config == null)
                return;

            try
            {
                _parser.WriteFile(_configFilePath, _config);
                Debug.WriteLine("[ConfigurationService] Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error saving configuration: {ex.Message}");
            }
        }
        
        #endregion

        #region Setting Accessors
        
        /// <summary>
        /// Gets a string setting value
        /// </summary>
        public string GetSetting(string section, string key, string defaultValue = "")
        {
            try
            {
                if (_config == null)
                    return defaultValue;
                
                var value = _config[section][key];
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch
            {
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Gets a boolean setting value
        /// </summary>
        public bool GetBoolSetting(string section, string key, bool defaultValue = false)
        {
            var value = GetSetting(section, key);
            var result = bool.TryParse(value, out var parsedResult) ? parsedResult : defaultValue;
            return result;
        }
        
        /// <summary>
        /// Gets an integer setting value with validation
        /// </summary>
        public int GetIntSetting(string section, string key, int defaultValue = 0)
        {
            var value = GetSetting(section, key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Gets a double setting value
        /// </summary>
        public double GetDoubleSetting(string section, string key, double defaultValue = 0.0)
        {
            var value = GetSetting(section, key);
            return double.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Sets a setting value
        /// </summary>
        public void SetSetting(string section, string key, string value)
        {
            if (_config == null)
                return;
            
            _config[section][key] = value ?? "";
        }
        
        /// <summary>
        /// Sets a boolean setting value
        /// </summary>
        public void SetBoolSetting(string section, string key, bool value)
        {
            SetSetting(section, key, value.ToString().ToLowerInvariant());
        }
        
        /// <summary>
        /// Sets an integer setting value
        /// </summary>
        public void SetIntSetting(string section, string key, int value)
        {
            SetSetting(section, key, value.ToString());
        }
        
        /// <summary>
        /// Sets a double setting value
        /// </summary>
        public void SetDoubleSetting(string section, string key, double value)
        {
            SetSetting(section, key, value.ToString("F2"));
        }
        
        #endregion

        #region Common Settings Properties
        
        /// <summary>
        /// Gets whether debug logging is enabled
        /// </summary>
        public bool IsDebugLoggingEnabled => 
            GetBoolSetting("Debug Settings", "EnableDebugLogging", false);
        
        /// <summary>
        /// Gets the debug log level
        /// </summary>
        public string DebugLogLevel => 
            GetSetting("Debug Settings", "DebugLogLevel", "Info");
        
        /// <summary>
        /// Gets the monitoring interval in milliseconds
        /// </summary>
        public int MonitoringIntervalMs => 
            GetIntSetting("Monitoring Settings", "MonitoringIntervalMs", 1000);
        
        /// <summary>
        /// Gets whether auto-reconnect is enabled
        /// </summary>
        public bool EnableAutoReconnect => 
            GetBoolSetting("Monitoring Settings", "EnableAutoReconnect", true);
        
        /// <summary>
        /// Gets the connection timeout in milliseconds
        /// </summary>
        public int ConnectionTimeoutMs => 
            GetIntSetting("Monitoring Settings", "ConnectionTimeoutMs", 5000);
        
        /// <summary>
        /// Gets whether to show status messages
        /// </summary>
        public bool ShowStatusMessages => 
            GetBoolSetting("Display Settings", "ShowStatusMessages", true);
        
        /// <summary>
        /// Gets whether to show detailed metrics
        /// </summary>
        public bool ShowDetailedMetrics => 
            GetBoolSetting("Display Settings", "ShowDetailedMetrics", true);
        
        /// <summary>
        /// Gets whether to use metric system for units
        /// </summary>
        public bool UseMetricSystem => 
            GetBoolSetting("Display Settings", "UseMetricSystem", true);
        
        #endregion

        #region Steam Settings Properties
        
        /// <summary>
        /// Gets the Steam Web API key
        /// </summary>
        public string SteamApiKey => 
            GetSetting("Steam Settings", "ApiKey", "");
        
        /// <summary>
        /// Gets the Steam ID64 to monitor (64-bit format, 17 digits starting with 7656119)
        /// </summary>
        public string SteamId64 => 
            GetSetting("Steam Settings", "SteamId64", "");
        
        /// <summary>
        /// Gets the Steam ID (backward compatibility - returns SteamId64)
        /// </summary>
        [Obsolete("Use SteamId64 instead for clarity about the 64-bit format")]
        public string SteamId => SteamId64;
        
        /// <summary>
        /// Validates if the provided Steam ID is in valid SteamID64 format
        /// </summary>
        public bool IsValidSteamId64(string steamId64)
        {
            if (string.IsNullOrWhiteSpace(steamId64))
                return false;
                
            // SteamID64 should be exactly 17 digits and start with 7656119
            return steamId64.Length == 17 && 
                   steamId64.All(char.IsDigit) && 
                   steamId64.StartsWith("7656119");
        }
        
        /// <summary>
        /// Gets whether the current SteamId64 configuration is valid
        /// </summary>
        public bool HasValidSteamId64 => IsValidSteamId64(SteamId64);
        
        /// <summary>
        /// Gets the update interval for Steam data in seconds
        /// </summary>
        public int UpdateIntervalSeconds => 
            GetIntSetting("Steam Settings", "UpdateIntervalSeconds", 30);
        
        /// <summary>
        /// Gets whether profile monitoring is enabled
        /// </summary>
        public bool EnableProfileMonitoring => 
            GetBoolSetting("Steam Settings", "EnableProfileMonitoring", true);
        
        /// <summary>
        /// Gets whether library monitoring is enabled
        /// </summary>
        public bool EnableLibraryMonitoring => 
            GetBoolSetting("Steam Settings", "EnableLibraryMonitoring", true);
        
        /// <summary>
        /// Gets whether current game monitoring is enabled
        /// </summary>
        public bool EnableCurrentGameMonitoring => 
            GetBoolSetting("Steam Settings", "EnableCurrentGameMonitoring", true);
        
        /// <summary>
        /// Gets whether achievement monitoring is enabled
        /// </summary>
        public bool EnableAchievementMonitoring => 
            GetBoolSetting("Steam Settings", "EnableAchievementMonitoring", false);
        
        /// <summary>
        /// Gets the maximum number of recent games to track
        /// </summary>
        public int MaxRecentGames => 
            GetIntSetting("Steam Settings", "MaxRecentGames", 5);
        
        #endregion

        #region Validation Methods
        
        /// <summary>
        /// Validates that required settings are present and valid
        /// </summary>
        public bool ValidateConfiguration()
        {
            try
            {
                // Validate core settings
                if (MonitoringIntervalMs <= 0)
                {
                    Debug.WriteLine("[ConfigurationService] Invalid MonitoringIntervalMs value");
                    return false;
                }
                
                if (ConnectionTimeoutMs <= 0)
                {
                    Debug.WriteLine("[ConfigurationService] Invalid ConnectionTimeoutMs value");
                    return false;
                }
                
                // Steam-specific validation
                if (string.IsNullOrWhiteSpace(SteamApiKey) || SteamApiKey == "<your-steam-api-key-here>")
                {
                    Debug.WriteLine("[ConfigurationService] Steam API Key is required but not set");
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(SteamId64) || SteamId64 == "<your-steam-id64-here>")
                {
                    Debug.WriteLine("[ConfigurationService] Steam ID64 is required but not set");
                    return false;
                }
                
                if (!IsValidSteamId64(SteamId64))
                {
                    Debug.WriteLine($"[ConfigurationService] Steam ID64 format is invalid: {SteamId64}");
                    return false;
                }
                
                if (UpdateIntervalSeconds < 10)
                {
                    Debug.WriteLine("[ConfigurationService] UpdateIntervalSeconds must be at least 10 seconds to respect rate limits");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error validating configuration: {ex.Message}");
                return false;
            }
        }
        
        #endregion
    }
}