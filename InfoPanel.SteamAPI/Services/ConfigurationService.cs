using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IniParser;
using IniParser.Model;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for configuration management and default values
    /// </summary>
    public static class ConfigurationConstants
    {
        #region Section Names
        public const string DEBUG_SETTINGS_SECTION = "Debug Settings";
        public const string MONITORING_SETTINGS_SECTION = "Monitoring Settings";
        public const string DISPLAY_SETTINGS_SECTION = "Display Settings";
        public const string STEAM_SETTINGS_SECTION = "Steam Settings";
        public const string TOKEN_MANAGEMENT_SECTION = "Token Management";
        public const string ADVANCED_FEATURES_SECTION = "Advanced Features";
        public const string FRIENDS_ACTIVITY_SECTION = "Friends Activity Settings";
        public const string ENHANCED_LOGGING_SECTION = "Enhanced Logging";
        #endregion
        
        #region Update Intervals (seconds)
        public const int DEFAULT_UPDATE_INTERVAL = 15;
        public const int DEFAULT_FAST_UPDATE_INTERVAL = 5;
        public const int DEFAULT_MEDIUM_UPDATE_INTERVAL = 15;
        public const int DEFAULT_SLOW_UPDATE_INTERVAL = 60;
        public const int MINIMUM_UPDATE_INTERVAL = 10;
        #endregion
        
        #region Monitoring Settings
        public const int DEFAULT_MONITORING_INTERVAL_MS = 1000;
        public const int DEFAULT_CONNECTION_TIMEOUT_MS = 5000;
        #endregion
        
        #region Steam ID Validation
        public const int STEAM_ID64_LENGTH = 17;
        public const string STEAM_ID64_PREFIX = "7656119";
        #endregion
        
        #region Default Limits
        public const int DEFAULT_MAX_RECENT_GAMES = 5;
        public const int DEFAULT_MAX_FRIENDS_DISPLAY = 0; // 0 = unlimited
        public const int DEFAULT_MAX_FRIEND_NAME_LENGTH = 20;
        public const int DEFAULT_MAX_MONITORED_ACHIEVEMENT_GAMES = 5;
        public const int DEFAULT_TOKEN_REFRESH_HOURS = 12;
        #endregion
        
        #region Default Values
        public const int DEFAULT_INT_VALUE = 0;
        public const double DEFAULT_DOUBLE_VALUE = 0.0;
        public const bool DEFAULT_BOOL_VALUE = false;
        public const string DEFAULT_STRING_VALUE = "";
        #endregion
        
        #region Default Filter/Display Settings
        public const string DEFAULT_FRIENDS_FILTER = "All";
        public const string DEFAULT_FRIENDS_SORT_BY = "PlayingFirst";
        public const string DEFAULT_FRIENDS_COLUMNS = "Friend,Status,Playing,LastOnline";
        public const string DEFAULT_LAST_SEEN_FORMAT = "Smart";
        public const string DEFAULT_FRIEND_NAME_DISPLAY = "DisplayName";
        #endregion
        
        #region Placeholder Values
        public const string API_KEY_PLACEHOLDER = "<your-steam-api-key-here>";
        public const string STEAM_ID_PLACEHOLDER = "<your-steam-id64-here>";
        #endregion
        
        #region Parser Configuration
        public const string COMMENT_STRING = "#";
        #endregion
    }

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
        private readonly EnhancedLoggingService? _enhancedLogger;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes the configuration service with the specified config file path
        /// </summary>
        /// <param name="configFilePath">Path to the INI configuration file</param>
        /// <param name="enhancedLogger">Optional enhanced logging service for structured logging</param>
        public ConfigurationService(string? configFilePath, EnhancedLoggingService? enhancedLogger = null)
        {
            _enhancedLogger = enhancedLogger;
            _enhancedLogger?.LogDebug("ConfigurationService.Constructor", "Initializing configuration service", new { ConfigFilePath = configFilePath });
            
            _configFilePath = configFilePath;
            _parser = new FileIniDataParser();
            
            // Configure parser to handle # comments (not just ; comments)
            _parser.Parser.Configuration.CommentString = ConfigurationConstants.COMMENT_STRING;
            
            _enhancedLogger?.LogDebug("ConfigurationService.Constructor", "About to load configuration");
            LoadConfiguration();
            _enhancedLogger?.LogDebug("ConfigurationService.Constructor", "Configuration loading completed");
        }
        
        #endregion

        #region Configuration Management
        
        /// <summary>
        /// Loads configuration from INI file
        /// </summary>
        private void LoadConfiguration()
        {
            _enhancedLogger?.LogDebug("ConfigurationService.LoadConfiguration", "Loading configuration", new { ConfigFilePath = _configFilePath });
            
            if (string.IsNullOrEmpty(_configFilePath))
            {
                _enhancedLogger?.LogWarning("ConfigurationService.LoadConfiguration", "Config file path is not set");
                Debug.WriteLine("[ConfigurationService] Config file path is not set.");
                return;
            }

            _enhancedLogger?.LogDebug("ConfigurationService.LoadConfiguration", "Checking if file exists", new { FileExists = File.Exists(_configFilePath) });

            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // Since the plugin comes bundled with an INI file, this should not happen
                    // If it does, create a minimal default config as fallback
                    _enhancedLogger?.LogWarning("ConfigurationService.LoadConfiguration", "Config file not found, creating fallback");
                    Debug.WriteLine("[ConfigurationService] Warning: Bundled config file not found, creating minimal fallback.");
                    CreateDefaultConfiguration();
                }
                else
                {
                    _enhancedLogger?.LogDebug("ConfigurationService.LoadConfiguration", "Config file found, attempting to load");
                    // Load existing config with safe file reading
                    using var fileStream = new FileStream(_configFilePath, 
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(fileStream);
                    
                    string fileContent = reader.ReadToEnd();
                    _config = _parser.Parser.Parse(fileContent);
                    
                    _enhancedLogger?.LogDebug("ConfigurationService.LoadConfiguration", "Configuration parsed successfully");
                    Debug.WriteLine("[ConfigurationService] Configuration loaded successfully.");
                    
                    // Test reading Steam API key immediately
                    var testApiKey = GetSetting("Steam Settings", "ApiKey", "");
                    _enhancedLogger?.LogDebug("ConfigurationService.LoadConfiguration", "Steam API Key check", new 
                    { 
                        ApiKeyStatus = string.IsNullOrEmpty(testApiKey) ? "EMPTY" : "SET" 
                    });
                    
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
                _enhancedLogger?.LogError("ConfigurationService.LoadConfiguration", "Exception during configuration loading", ex, new
                {
                    ConfigFilePath = _configFilePath
                });
                Debug.WriteLine($"[ConfigurationService] Error loading configuration: {ex.Message}");
                Debug.WriteLine("[ConfigurationService] Creating minimal in-memory config to preserve existing file.");
                
                // Create minimal in-memory configuration as fallback
                // NEVER call CreateDefaultConfiguration() here as it might overwrite the file
                _config = new IniData();
                
                // Add minimal required sections to prevent crashes
                _config[ConfigurationConstants.DEBUG_SETTINGS_SECTION]["EnableDebugLogging"] = ConfigurationConstants.DEFAULT_BOOL_VALUE.ToString().ToLower();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["ApiKey"] = ConfigurationConstants.API_KEY_PLACEHOLDER;
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["SteamId64"] = ConfigurationConstants.STEAM_ID_PLACEHOLDER;
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["UpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["FastUpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_FAST_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["MediumUpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_MEDIUM_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["SlowUpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_SLOW_UPDATE_INTERVAL.ToString();
                
                // Add Friends Activity Settings with defaults
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["ShowAllFriends"] = "true";
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["MaxFriendsToDisplay"] = ConfigurationConstants.DEFAULT_MAX_FRIENDS_DISPLAY.ToString();
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendsFilter"] = ConfigurationConstants.DEFAULT_FRIENDS_FILTER;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendsSortBy"] = ConfigurationConstants.DEFAULT_FRIENDS_SORT_BY;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["SortDescending"] = "true";
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendsTableColumns"] = ConfigurationConstants.DEFAULT_FRIENDS_COLUMNS;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["LastSeenFormat"] = ConfigurationConstants.DEFAULT_LAST_SEEN_FORMAT;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["HiddenStatuses"] = ConfigurationConstants.DEFAULT_STRING_VALUE;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendNameDisplay"] = ConfigurationConstants.DEFAULT_FRIEND_NAME_DISPLAY;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["MaxFriendNameLength"] = ConfigurationConstants.DEFAULT_MAX_FRIEND_NAME_LENGTH.ToString();
                
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
                _config[ConfigurationConstants.DEBUG_SETTINGS_SECTION]["EnableDebugLogging"] = ConfigurationConstants.DEFAULT_BOOL_VALUE.ToString().ToLower();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["ApiKey"] = ConfigurationConstants.API_KEY_PLACEHOLDER;
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["SteamId64"] = ConfigurationConstants.STEAM_ID_PLACEHOLDER;
                
                Debug.WriteLine("[ConfigurationService] Created minimal in-memory config as fallback. Original file preserved.");
                return;
            }

            try
            {
                _config = new IniData();
                
                // Debug Settings
                _config[ConfigurationConstants.DEBUG_SETTINGS_SECTION]["EnableDebugLogging"] = ConfigurationConstants.DEFAULT_BOOL_VALUE.ToString().ToLower();
                
                // Monitoring Settings
                _config[ConfigurationConstants.MONITORING_SETTINGS_SECTION]["MonitoringIntervalMs"] = ConfigurationConstants.DEFAULT_MONITORING_INTERVAL_MS.ToString();
                _config[ConfigurationConstants.MONITORING_SETTINGS_SECTION]["EnableAutoReconnect"] = "true";
                _config[ConfigurationConstants.MONITORING_SETTINGS_SECTION]["ConnectionTimeoutMs"] = ConfigurationConstants.DEFAULT_CONNECTION_TIMEOUT_MS.ToString();
                
                // Display Settings
                _config[ConfigurationConstants.DISPLAY_SETTINGS_SECTION]["ShowStatusMessages"] = "true";
                _config[ConfigurationConstants.DISPLAY_SETTINGS_SECTION]["ShowDetailedMetrics"] = "true";
                _config[ConfigurationConstants.DISPLAY_SETTINGS_SECTION]["UseMetricSystem"] = "true";
                
                // Steam Settings
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["ApiKey"] = ConfigurationConstants.API_KEY_PLACEHOLDER;
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["SteamId64"] = ConfigurationConstants.STEAM_ID_PLACEHOLDER;
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["UpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["FastUpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_FAST_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["MediumUpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_MEDIUM_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["SlowUpdateIntervalSeconds"] = ConfigurationConstants.DEFAULT_SLOW_UPDATE_INTERVAL.ToString();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["EnableProfileMonitoring"] = "true";
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["EnableLibraryMonitoring"] = "true";
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["EnableCurrentGameMonitoring"] = "true";
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["EnableAchievementMonitoring"] = ConfigurationConstants.DEFAULT_BOOL_VALUE.ToString().ToLower();
                _config[ConfigurationConstants.STEAM_SETTINGS_SECTION]["MaxRecentGames"] = ConfigurationConstants.DEFAULT_MAX_RECENT_GAMES.ToString();
                
                // Friends Activity Settings
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["ShowAllFriends"] = "true";
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["MaxFriendsToDisplay"] = ConfigurationConstants.DEFAULT_MAX_FRIENDS_DISPLAY.ToString();
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendsFilter"] = ConfigurationConstants.DEFAULT_FRIENDS_FILTER;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendsSortBy"] = ConfigurationConstants.DEFAULT_FRIENDS_SORT_BY;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["SortDescending"] = "true";
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendsTableColumns"] = ConfigurationConstants.DEFAULT_FRIENDS_COLUMNS;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["LastSeenFormat"] = ConfigurationConstants.DEFAULT_LAST_SEEN_FORMAT;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["HiddenStatuses"] = ConfigurationConstants.DEFAULT_STRING_VALUE;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["FriendNameDisplay"] = ConfigurationConstants.DEFAULT_FRIEND_NAME_DISPLAY;
                _config[ConfigurationConstants.FRIENDS_ACTIVITY_SECTION]["MaxFriendNameLength"] = ConfigurationConstants.DEFAULT_MAX_FRIEND_NAME_LENGTH.ToString();
                
                // Enhanced Logging Settings
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["EnableDeltaLogging"] = "true";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["EnableStructuredLogging"] = "true";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["FlushIntervalMs"] = "1000";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["MinimumLevel"] = "Info";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["EnablePerformanceLogging"] = "true";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["EnableOperationPairing"] = "true";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["LogRotationSizeMB"] = "5";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["MaxArchivedLogs"] = "5";
                _config[ConfigurationConstants.ENHANCED_LOGGING_SECTION]["EnableSensitiveDataRedaction"] = "true";
                
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
                ["Friends Activity Settings"] = new[] { "ShowAllFriends", "MaxFriendsToDisplay", "FriendsFilter", "FriendsSortBy", "SortDescending", "FriendsTableColumns", "LastSeenFormat", "HiddenStatuses", "FriendNameDisplay", "MaxFriendNameLength" },
                ["Enhanced Logging"] = new[] { "EnableDeltaLogging", "EnableStructuredLogging", "FlushIntervalMs", "MinimumLevel", "EnablePerformanceLogging", "EnableOperationPairing", "LogRotationSizeMB", "MaxArchivedLogs", "EnableSensitiveDataRedaction" }
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
        public string GetSetting(string section, string key, string defaultValue = ConfigurationConstants.DEFAULT_STRING_VALUE)
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
        public int GetIntSetting(string section, string key, int defaultValue = ConfigurationConstants.DEFAULT_INT_VALUE)
        {
            var value = GetSetting(section, key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Gets a double setting value
        /// </summary>
        public double GetDoubleSetting(string section, string key, double defaultValue = ConfigurationConstants.DEFAULT_DOUBLE_VALUE)
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
            GetIntSetting(ConfigurationConstants.MONITORING_SETTINGS_SECTION, "MonitoringIntervalMs", ConfigurationConstants.DEFAULT_MONITORING_INTERVAL_MS);
        
        /// <summary>
        /// Gets whether auto-reconnect is enabled
        /// </summary>
        public bool EnableAutoReconnect => 
            GetBoolSetting(ConfigurationConstants.MONITORING_SETTINGS_SECTION, "EnableAutoReconnect", true);
        
        /// <summary>
        /// Gets the connection timeout in milliseconds
        /// </summary>
        public int ConnectionTimeoutMs => 
            GetIntSetting(ConfigurationConstants.MONITORING_SETTINGS_SECTION, "ConnectionTimeoutMs", ConfigurationConstants.DEFAULT_CONNECTION_TIMEOUT_MS);
        
        /// <summary>
        /// Gets whether to show status messages
        /// </summary>
        public bool ShowStatusMessages => 
            GetBoolSetting(ConfigurationConstants.DISPLAY_SETTINGS_SECTION, "ShowStatusMessages", true);
        
        /// <summary>
        /// Gets whether to show detailed metrics
        /// </summary>
        public bool ShowDetailedMetrics => 
            GetBoolSetting(ConfigurationConstants.DISPLAY_SETTINGS_SECTION, "ShowDetailedMetrics", true);
        
        /// <summary>
        /// Gets whether to use metric system for units
        /// </summary>
        public bool UseMetricSystem => 
            GetBoolSetting(ConfigurationConstants.DISPLAY_SETTINGS_SECTION, "UseMetricSystem", true);
        
        #endregion

        #region Steam Settings Properties
        
        /// <summary>
        /// Gets the Steam Web API key
        /// </summary>
        public string SteamApiKey => 
            GetSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "ApiKey", ConfigurationConstants.DEFAULT_STRING_VALUE);
        
        /// <summary>
        /// Gets the Steam ID64 to monitor (64-bit format, 17 digits starting with 7656119)
        /// </summary>
        public string SteamId64 => 
            GetSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "SteamId64", ConfigurationConstants.DEFAULT_STRING_VALUE);
        
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
            return steamId64.Length == ConfigurationConstants.STEAM_ID64_LENGTH && 
                   steamId64.All(char.IsDigit) && 
                   steamId64.StartsWith(ConfigurationConstants.STEAM_ID64_PREFIX);
        }
        
        /// <summary>
        /// Gets whether the current SteamId64 configuration is valid
        /// </summary>
        public bool HasValidSteamId64 => IsValidSteamId64(SteamId64);
        
        /// <summary>
        /// Gets the update interval for Steam data in seconds
        /// </summary>
        public int UpdateIntervalSeconds => 
            GetIntSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "UpdateIntervalSeconds", ConfigurationConstants.DEFAULT_UPDATE_INTERVAL);
            
        /// <summary>
        /// Gets the fast update interval for critical real-time data (game state, session time) in seconds
        /// </summary>
        public int FastUpdateIntervalSeconds => 
            GetIntSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "FastUpdateIntervalSeconds", ConfigurationConstants.DEFAULT_FAST_UPDATE_INTERVAL);
            
        /// <summary>
        /// Gets the medium update interval for social data (friends status) in seconds  
        /// </summary>
        public int MediumUpdateIntervalSeconds => 
            GetIntSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "MediumUpdateIntervalSeconds", ConfigurationConstants.DEFAULT_MEDIUM_UPDATE_INTERVAL);
            
        /// <summary>
        /// Gets the slow update interval for static data (library stats, achievements) in seconds
        /// </summary>
        public int SlowUpdateIntervalSeconds => 
            GetIntSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "SlowUpdateIntervalSeconds", ConfigurationConstants.DEFAULT_SLOW_UPDATE_INTERVAL);
        
        /// <summary>
        /// Gets whether profile monitoring is enabled
        /// </summary>
        public bool EnableProfileMonitoring => 
            GetBoolSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "EnableProfileMonitoring", true);
        
        /// <summary>
        /// Gets whether library monitoring is enabled
        /// </summary>
        public bool EnableLibraryMonitoring => 
            GetBoolSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "EnableLibraryMonitoring", true);
        
        /// <summary>
        /// Gets whether current game monitoring is enabled
        /// </summary>
        public bool EnableCurrentGameMonitoring => 
            GetBoolSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "EnableCurrentGameMonitoring", true);
        
        /// <summary>
        /// Gets whether achievement monitoring is enabled
        /// </summary>
        public bool EnableAchievementMonitoring => 
            GetBoolSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "EnableAchievementMonitoring", ConfigurationConstants.DEFAULT_BOOL_VALUE);
        
        /// <summary>
        /// Gets the maximum number of recent games to track
        /// </summary>
        public int MaxRecentGames => 
            GetIntSetting(ConfigurationConstants.STEAM_SETTINGS_SECTION, "MaxRecentGames", ConfigurationConstants.DEFAULT_MAX_RECENT_GAMES);
        
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
            GetSetting("Friends Activity Settings", "FriendsSortBy", "PlayingFirst");
        
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

        #region Enhanced Logging Properties
        
        /// <summary>
        /// Gets whether delta logging is enabled (only logs changes)
        /// </summary>
        public bool EnableDeltaLogging => 
            GetBoolSetting("Enhanced Logging", "EnableDeltaLogging", true);
        
        /// <summary>
        /// Gets whether structured logging is enabled (JSON format)
        /// </summary>
        public bool EnableStructuredLogging => 
            GetBoolSetting("Enhanced Logging", "EnableStructuredLogging", true);
        
        /// <summary>
        /// Gets the log flush interval in milliseconds
        /// </summary>
        public int LogFlushInterval => 
            GetIntSetting("Enhanced Logging", "FlushIntervalMs", 1000);
        
        /// <summary>
        /// Gets the minimum log level (Trace, Debug, Info, Warning, Error, Critical)
        /// </summary>
        public string MinimumLogLevel => 
            GetSetting("Enhanced Logging", "MinimumLevel", "Info");
        
        /// <summary>
        /// Gets whether performance logging is enabled
        /// </summary>
        public bool EnablePerformanceLogging => 
            GetBoolSetting("Enhanced Logging", "EnablePerformanceLogging", true);
        
        /// <summary>
        /// Gets whether operation pairing is enabled (start/end correlation)
        /// </summary>
        public bool EnableOperationPairing => 
            GetBoolSetting("Enhanced Logging", "EnableOperationPairing", true);
        
        /// <summary>
        /// Gets the log rotation size in MB
        /// </summary>
        public int LogRotationSizeMB => 
            GetIntSetting("Enhanced Logging", "LogRotationSizeMB", 5);
        
        /// <summary>
        /// Gets the maximum number of archived logs to keep
        /// </summary>
        public int MaxArchivedLogs => 
            GetIntSetting("Enhanced Logging", "MaxArchivedLogs", 5);
        
        /// <summary>
        /// Gets whether sensitive data redaction is enabled
        /// </summary>
        public bool EnableSensitiveDataRedaction => 
            GetBoolSetting("Enhanced Logging", "EnableSensitiveDataRedaction", true);
        
        #endregion
    }
}