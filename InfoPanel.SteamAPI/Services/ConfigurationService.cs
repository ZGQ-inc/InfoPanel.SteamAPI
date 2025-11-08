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
            Console.WriteLine($"[ConfigurationService] Constructor called with path: {configFilePath}");
            _configFilePath = configFilePath;
            _parser = new FileIniDataParser();
            
            // Configure parser to handle # comments (not just ; comments)
            _parser.Parser.Configuration.CommentString = "#";
            
            Console.WriteLine("[ConfigurationService] About to call LoadConfiguration()");
            LoadConfiguration();
            Console.WriteLine("[ConfigurationService] LoadConfiguration() completed");
        }
        
        #endregion

        #region Configuration Management
        
        /// <summary>
        /// Loads configuration from INI file
        /// </summary>
        private void LoadConfiguration()
        {
            Console.WriteLine($"[ConfigurationService] LoadConfiguration called. Path: {_configFilePath}");
            
            if (string.IsNullOrEmpty(_configFilePath))
            {
                Console.WriteLine("[ConfigurationService] Config file path is not set.");
                Debug.WriteLine("[ConfigurationService] Config file path is not set.");
                return;
            }

            Console.WriteLine($"[ConfigurationService] Checking if file exists: {File.Exists(_configFilePath)}");

            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // Since the plugin comes bundled with an INI file, this should not happen
                    // If it does, create a minimal default config as fallback
                    Console.WriteLine("[ConfigurationService] Config file not found, creating fallback");
                    Debug.WriteLine("[ConfigurationService] Warning: Bundled config file not found, creating minimal fallback.");
                    CreateDefaultConfiguration();
                }
                else
                {
                    Console.WriteLine("[ConfigurationService] Config file found, attempting to load");
                    // Load existing config with safe file reading
                    using var fileStream = new FileStream(_configFilePath, 
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(fileStream);
                    
                    string fileContent = reader.ReadToEnd();
                    _config = _parser.Parser.Parse(fileContent);
                    
                    Console.WriteLine("[ConfigurationService] Configuration parsed successfully");
                    Debug.WriteLine("[ConfigurationService] Configuration loaded successfully.");
                    
                    // Test reading Steam API key immediately
                    var testApiKey = GetSetting("Steam Settings", "ApiKey", "");
                    Console.WriteLine($"[ConfigurationService] Test Steam API Key read: {(string.IsNullOrEmpty(testApiKey) ? "EMPTY" : "SET")}");
                    
                    // Check for missing keys but don't save - preserve the original formatted file
                    // Missing settings will use in-memory defaults from the GetSetting methods
                    if (EnsureMissingKeys())
                    {
                        Debug.WriteLine("[ConfigurationService] Using in-memory defaults for missing configuration keys to preserve original file formatting.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigurationService] Exception during loading: {ex.Message}");
                Debug.WriteLine($"[ConfigurationService] Error loading configuration: {ex.Message}");
                Debug.WriteLine("[ConfigurationService] Creating minimal in-memory config to preserve existing file.");
                
                // Create minimal in-memory configuration as fallback
                // NEVER call CreateDefaultConfiguration() here as it might overwrite the file
                _config = new IniData();
                
                // Add minimal required sections to prevent crashes
                _config["Debug Settings"]["EnableDebugLogging"] = "false";
                _config["Steam Settings"]["ApiKey"] = "<your-steam-api-key-here>";
                _config["Steam Settings"]["SteamId64"] = "<your-steam-id64-here>";
                _config["Steam Settings"]["UpdateIntervalSeconds"] = "30";
                
                // Add Friends Activity Settings with defaults
                _config["Friends Activity Settings"]["ShowAllFriends"] = "true";
                _config["Friends Activity Settings"]["MaxFriendsToDisplay"] = "0";
                _config["Friends Activity Settings"]["FriendsFilter"] = "All";
                _config["Friends Activity Settings"]["FriendsSortBy"] = "LastOnline";
                _config["Friends Activity Settings"]["SortDescending"] = "true";
                _config["Friends Activity Settings"]["FriendsTableColumns"] = "Friend,Status,Playing,LastOnline";
                _config["Friends Activity Settings"]["LastSeenFormat"] = "Smart";
                _config["Friends Activity Settings"]["HiddenStatuses"] = "";
                _config["Friends Activity Settings"]["FriendNameDisplay"] = "DisplayName";
                _config["Friends Activity Settings"]["MaxFriendNameLength"] = "20";
                
                Debug.WriteLine("[ConfigurationService] Minimal config created. Original file preserved for manual recovery.");
            }
        }
        
        /// <summary>
        /// Creates default configuration ONLY if no config file exists.
        /// Never overwrites existing files to preserve user customizations.
        /// </summary>
        private void CreateDefaultConfiguration()
        {
            if (string.IsNullOrEmpty(_configFilePath))
                return;

            // CRITICAL: Never overwrite an existing config file
            if (File.Exists(_configFilePath))
            {
                Debug.WriteLine("[ConfigurationService] Config file exists - will not overwrite. Loading with error recovery.");
                
                // Try to create a minimal in-memory config as fallback
                _config = new IniData();
                
                // Add minimal required sections to prevent crashes
                _config["Debug Settings"]["EnableDebugLogging"] = "false";
                _config["Steam Settings"]["ApiKey"] = "<your-steam-api-key-here>";
                _config["Steam Settings"]["SteamId64"] = "<your-steam-id64-here>";
                
                Debug.WriteLine("[ConfigurationService] Created minimal in-memory config as fallback. Original file preserved.");
                return;
            }

            try
            {
                _config = new IniData();
                
                // Debug Settings
                _config["Debug Settings"]["EnableDebugLogging"] = "false";
                
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
                
                // Friends Activity Settings
                _config["Friends Activity Settings"]["ShowAllFriends"] = "true";
                _config["Friends Activity Settings"]["MaxFriendsToDisplay"] = "0";
                _config["Friends Activity Settings"]["FriendsFilter"] = "All";
                _config["Friends Activity Settings"]["FriendsSortBy"] = "LastOnline";
                _config["Friends Activity Settings"]["SortDescending"] = "true";
                _config["Friends Activity Settings"]["FriendsTableColumns"] = "Friend,Status,Playing,LastOnline";
                _config["Friends Activity Settings"]["LastSeenFormat"] = "Smart";
                _config["Friends Activity Settings"]["HiddenStatuses"] = "";
                _config["Friends Activity Settings"]["FriendNameDisplay"] = "DisplayName";
                _config["Friends Activity Settings"]["MaxFriendNameLength"] = "20";
                
                // Only write to file if the file doesn't exist (new installation)
                _parser.WriteFile(_configFilePath, _config);
                Debug.WriteLine("[ConfigurationService] Created new config file for first-time installation.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error creating default configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks for missing configuration keys and logs them.
        /// Does NOT modify the config to preserve the original formatted INI file.
        /// Missing values will be provided by GetSetting method defaults.
        /// </summary>
        /// <returns>False - never saves to preserve file formatting</returns>
        private bool EnsureMissingKeys()
        {
            if (_config == null)
                return false;

            var missingKeys = new List<string>();
            
            // Check for missing keys across all sections
            var requiredKeys = new Dictionary<string, string[]>
            {
                ["Debug Settings"] = new[] { "EnableDebugLogging" },
                ["Monitoring Settings"] = new[] { "MonitoringIntervalMs", "EnableAutoReconnect", "ConnectionTimeoutMs" },
                ["Display Settings"] = new[] { "ShowStatusMessages", "ShowDetailedMetrics", "UseMetricSystem" },
                ["Steam Settings"] = new[] { "ApiKey", "SteamId64", "UpdateIntervalSeconds", "EnableProfileMonitoring", "EnableLibraryMonitoring", "EnableCurrentGameMonitoring", "EnableAchievementMonitoring", "MaxRecentGames" },
                ["Token Management"] = new[] { "AutoRefreshTokens", "CommunityTokenEnabled", "StoreTokenEnabled", "TokenRefreshIntervalHours", "ManualTokenEntry" },
                ["Advanced Features"] = new[] { "EnableEnhancedBadgeData", "EnableStoreIntegration", "EnableExtendedAchievements", "MaxMonitoredGamesForAchievements" },
                ["Friends Activity Settings"] = new[] { "ShowAllFriends", "MaxFriendsToDisplay", "FriendsFilter", "FriendsSortBy", "SortDescending", "FriendsTableColumns", "LastSeenFormat", "HiddenStatuses", "FriendNameDisplay", "MaxFriendNameLength" }
            };

            foreach (var section in requiredKeys)
            {
                if (!_config.Sections.ContainsSection(section.Key))
                {
                    missingKeys.AddRange(section.Value.Select(key => $"[{section.Key}] {key}"));
                }
                else
                {
                    foreach (var key in section.Value)
                    {
                        if (!_config[section.Key].ContainsKey(key))
                        {
                            missingKeys.Add($"[{section.Key}] {key}");
                        }
                    }
                }
            }

            if (missingKeys.Count > 0)
            {
                Debug.WriteLine($"[ConfigurationService] Found {missingKeys.Count} missing configuration keys. Using in-memory defaults: {string.Join(", ", missingKeys)}");
            }
            else
            {
                Debug.WriteLine("[ConfigurationService] All required configuration keys are present.");
            }
            
            return false; // Never return true to preserve the original formatted file
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

        #region Friends Activity Settings Properties
        
        /// <summary>
        /// Gets whether to show all friends (true) or limit to top 10 (false)
        /// </summary>
        public bool ShowAllFriends => 
            GetBoolSetting("Friends Activity Settings", "ShowAllFriends", true);
        
        /// <summary>
        /// Gets the filter mode for friends display
        /// Options: "All", "OnlineOnly", "Active3Days", "Active5Days", "Active7Days"
        /// </summary>
        public string FriendsFilter => 
            GetSetting("Friends Activity Settings", "FriendsFilter", "All");
        
        /// <summary>
        /// Gets the sorting mode for friends display
        /// Options: "LastOnline", "Name", "Status", "PlayingFirst"
        /// </summary>
        public string FriendsSortBy => 
            GetSetting("Friends Activity Settings", "FriendsSortBy", "LastOnline");
        
        /// <summary>
        /// Gets whether to sort friends in descending order (true) or ascending (false)
        /// </summary>
        public bool SortDescending => 
            GetBoolSetting("Friends Activity Settings", "SortDescending", true);
        
        /// <summary>
        /// Gets the maximum number of friends to display in the table (0 = unlimited)
        /// </summary>
        public int MaxFriendsToDisplay => 
            GetIntSetting("Friends Activity Settings", "MaxFriendsToDisplay", 0);
        
        /// <summary>
        /// Gets the comma-separated list of columns to show in the friends table
        /// </summary>
        public string FriendsTableColumns => 
            GetSetting("Friends Activity Settings", "FriendsTableColumns", "Friend,Status,Playing,LastOnline");
        
        /// <summary>
        /// Gets the format for displaying last seen times
        /// Options: "Relative", "DateTime", "Smart"
        /// </summary>
        public string LastSeenFormat => 
            GetSetting("Friends Activity Settings", "LastSeenFormat", "Smart");
        
        /// <summary>
        /// Gets the comma-separated list of friend statuses to hide
        /// </summary>
        public string HiddenStatuses => 
            GetSetting("Friends Activity Settings", "HiddenStatuses", "");
        
        /// <summary>
        /// Gets how to display friend names
        /// Options: "DisplayName", "WithStatus", "Truncated"
        /// </summary>
        public string FriendNameDisplay => 
            GetSetting("Friends Activity Settings", "FriendNameDisplay", "DisplayName");
        
        /// <summary>
        /// Gets the maximum length for friend names when using truncated display
        /// </summary>
        public int MaxFriendNameLength => 
            GetIntSetting("Friends Activity Settings", "MaxFriendNameLength", 20);
        
        #endregion

        #region Token Management Properties
        
        /// <summary>
        /// Gets whether automatic token refresh is enabled
        /// </summary>
        public bool AutoRefreshTokens => 
            GetBoolSetting("Token Management", "AutoRefreshTokens", true);
        
        /// <summary>
        /// Gets whether community token features are enabled
        /// </summary>
        public bool CommunityTokenEnabled => 
            GetBoolSetting("Token Management", "CommunityTokenEnabled", true);
        
        /// <summary>
        /// Gets whether store token features are enabled
        /// </summary>
        public bool StoreTokenEnabled => 
            GetBoolSetting("Token Management", "StoreTokenEnabled", true);
        
        /// <summary>
        /// Gets the token refresh interval in hours
        /// </summary>
        public int TokenRefreshIntervalHours => 
            GetIntSetting("Token Management", "TokenRefreshIntervalHours", 12);
        
        /// <summary>
        /// Gets whether manual token entry mode is enabled
        /// </summary>
        public bool ManualTokenEntry => 
            GetBoolSetting("Token Management", "ManualTokenEntry", false);
        
        #endregion

        #region Advanced Features Properties
        
        /// <summary>
        /// Gets whether enhanced badge data collection is enabled
        /// </summary>
        public bool EnableEnhancedBadgeData => 
            GetBoolSetting("Advanced Features", "EnableEnhancedBadgeData", false);
        
        /// <summary>
        /// Gets whether store integration is enabled
        /// </summary>
        public bool EnableStoreIntegration => 
            GetBoolSetting("Advanced Features", "EnableStoreIntegration", false);
        
        /// <summary>
        /// Gets whether extended achievement tracking is enabled
        /// </summary>
        public bool EnableExtendedAchievements => 
            GetBoolSetting("Advanced Features", "EnableExtendedAchievements", false);
        
        /// <summary>
        /// Gets the maximum number of games to monitor for achievements
        /// </summary>
        public int MaxMonitoredGamesForAchievements => 
            GetIntSetting("Advanced Features", "MaxMonitoredGamesForAchievements", 5);
        
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

        #region Helper Methods
        
        /// <summary>
        /// Gets a boolean value from the configuration
        /// </summary>
        public bool GetBoolValue(string section, string key, bool defaultValue = false)
        {
            return GetBoolSetting(section, key, defaultValue);
        }
        
        /// <summary>
        /// Gets an integer value from the configuration
        /// </summary>
        public int GetIntValue(string section, string key, int defaultValue = 0)
        {
            return GetIntSetting(section, key, defaultValue);
        }
        
        /// <summary>
        /// Gets a string value from the configuration
        /// </summary>
        public string GetStringValue(string section, string key, string defaultValue = "")
        {
            return GetSetting(section, key, defaultValue);
        }
        
        #endregion
    }
}