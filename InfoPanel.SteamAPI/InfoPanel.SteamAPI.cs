// InfoPanel.SteamAPI v1.0.0 - Steam API Plugin for InfoPanel
using InfoPanel.Plugins;
using InfoPanel.SteamAPI.Services;
using InfoPanel.SteamAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI
{
    /// <summary>
    /// Constants for the main SteamAPI plugin configuration and operation
    /// </summary>
    public static class SteamAPIConstants
    {
        #region Table Configuration
        /// <summary>Table format string for recent games table columns</summary>
        public const string RECENT_GAMES_TABLE_FORMAT = "0:200|1:80|2:100";
        
        /// <summary>Table format string for game statistics table columns</summary>  
        public const string GAME_STATS_TABLE_FORMAT = "0:150|1:80|2:80|3:60|4:100";
        
        /// <summary>Table format string for friends activity table columns</summary>
        public const string FRIENDS_ACTIVITY_TABLE_FORMAT = "0:150|1:100|2:80|3:120";
        
        /// <summary>Maximum number of monitored games to show in statistics table</summary>
        public const int MAX_MONITORED_GAMES_IN_TABLE = 5;
        #endregion
        
        #region Time Conversion
        /// <summary>Minutes per hour conversion factor</summary>
        public const double MINUTES_PER_HOUR = 60.0;
        
        /// <summary>Hours per day for time calculations</summary>
        public const int HOURS_PER_DAY = 24;
        
        /// <summary>Default update interval in seconds when configuration is not available</summary>
        public const int DEFAULT_UPDATE_INTERVAL_SECONDS = 30;
        #endregion
        
        #region Activity Filtering
        /// <summary>Activity filter for friends active within 3 days</summary>
        public const int ACTIVITY_FILTER_3_DAYS = 3;
        
        /// <summary>Activity filter for friends active within 5 days</summary>
        public const int ACTIVITY_FILTER_5_DAYS = 5;
        
        /// <summary>Activity filter for friends active within 7 days</summary>
        public const int ACTIVITY_FILTER_7_DAYS = 7;
        
        /// <summary>Days threshold for smart last seen format</summary>
        public const int SMART_FORMAT_DAYS_THRESHOLD = 7;
        
        /// <summary>Days in month approximation for relative time formatting</summary>
        public const int DAYS_PER_MONTH = 30;
        #endregion
        
        #region Status Sorting
        /// <summary>Sort order for online status (highest priority)</summary>
        public const int ONLINE_STATUS_SORT_ORDER = 3;
        
        /// <summary>Sort order for away/busy status (medium priority)</summary>
        public const int AWAY_STATUS_SORT_ORDER = 2;
        
        /// <summary>Sort order for offline status (lowest priority)</summary>
        public const int OFFLINE_STATUS_SORT_ORDER = 1;
        
        /// <summary>Playing game priority for sorting (highest priority)</summary>
        public const int PLAYING_GAME_SORT_PRIORITY = 3;
        #endregion
        
        #region Default Values and Limits
        /// <summary>Maximum friend name length default</summary>
        public const int DEFAULT_MAX_FRIEND_NAME_LENGTH = 20;
        
        /// <summary>Name truncation suffix</summary>
        public const string NAME_TRUNCATION_SUFFIX = "...";
        
        /// <summary>Length of truncation suffix</summary>
        public const int TRUNCATION_SUFFIX_LENGTH = 3;
        
        /// <summary>Default maximum friends to display (0 = no limit)</summary>
        public const int DEFAULT_MAX_FRIENDS_TO_DISPLAY = 0;
        #endregion
        
        #region Logging and Status Messages
        /// <summary>Plugin name for logging</summary>
        public const string PLUGIN_NAME = "SteamAPI";
        
        /// <summary>Configuration section name for InfoPanel</summary>
        public const string CONFIG_SECTION_NAME = "InfoPanel.SteamAPI";
        
        /// <summary>Plugin description for InfoPanel</summary>
        public const string PLUGIN_DESCRIPTION = "Steam Data";
        
        /// <summary>Plugin subtitle for InfoPanel</summary>
        public const string PLUGIN_SUBTITLE = "Get data from SteamAPI";
        #endregion
        
        #region Status Indicators
        /// <summary>Online status indicator</summary>
        public const string STATUS_INDICATOR_ONLINE = "●";
        
        /// <summary>Away status indicator</summary>
        public const string STATUS_INDICATOR_AWAY = "◐";
        
        /// <summary>Busy status indicator</summary>
        public const string STATUS_INDICATOR_BUSY = "◒";
        
        /// <summary>Snooze status indicator</summary>
        public const string STATUS_INDICATOR_SNOOZE = "◑";
        
        /// <summary>Offline status indicator</summary>
        public const string STATUS_INDICATOR_OFFLINE = "○";
        #endregion
        
        #region String Literals
        /// <summary>Unknown game name fallback</summary>
        public const string UNKNOWN_GAME = "Unknown Game";
        
        /// <summary>Not playing status</summary>
        public const string NOT_PLAYING = "Not Playing";
        
        /// <summary>Online now status for last seen</summary>
        public const string ONLINE_NOW = "Online Now";
        
        /// <summary>Unknown status fallback</summary>
        public const string UNKNOWN_STATUS = "Unknown";
        
        /// <summary>Playing indicator for currently playing games</summary>
        public const string PLAYING_INDICATOR = "▶ ";
        
        /// <summary>Achievement not available text</summary>
        public const string ACHIEVEMENT_NOT_AVAILABLE = "N/A";
        #endregion
    }

    /// <summary>
    /// InfoPanel Steam API Plugin - Monitor Steam profile and gaming activity
    /// 
    /// Comprehensive Steam monitoring plugin that provides:
    /// - Steam profile data (player name, status, level)
    /// - Current game tracking and playtime statistics
    /// - Library statistics and recent gaming activity
    /// - Advanced features like news, achievements, and recommendations
    /// - Social features including friends activity and community badges
    /// - Global statistics comparison and performance metrics
    /// 
    /// Features 48 sensors + 3 tables across 4 monitoring containers with
    /// complete Steam Web API integration and SteamID64 validation.
    /// </summary>
    public class SteamAPIMain : BasePlugin, IDisposable
    {
        #region Configuration
        
        // Configuration file path - exposed to InfoPanel for direct file access
        private string? _configFilePath;
        
        /// <summary>
        /// Exposes the configuration file path to InfoPanel for the "Open Config" button
        /// </summary>
        public override string? ConfigFilePath => _configFilePath;
        
        #endregion

        #region Sensors
        
        // User Profile and Status
        private readonly PluginText _playerNameSensor = new("player-name", "Player Name", "Unknown");
        private readonly PluginText _onlineStatusSensor = new("online-status", "Status", "Offline");
        private readonly PluginSensor _steamLevelSensor = new("steam-level", "Steam Level", 0, "");
        private readonly PluginText _statusSensor = new("status", "Plugin Status", "Initializing...");
        private readonly PluginText _detailsSensor = new("details", "Details", "Loading Steam data...");
        
        // User Profile and Game Images
        private readonly PluginText _profileImageUrlSensor = new("profile_image_url", "Profile Image URL", "-");
        private readonly PluginText _currentGameBannerUrlSensor = new("current_game_banner_url", "Current Game Banner URL", "-");
        private readonly PluginText _gameStatusTextSensor = new("game-status-text", "Game Status", "Not Playing");
        
        // Current Game and Session Tracking
        private readonly PluginText _currentGameSensor = new("current-game", "Current Game", "Not Playing");
        private readonly PluginSensor _currentGamePlaytimeSensor = new("current-game-playtime", "Current Game Total Hours", 0, "hrs");
        private readonly PluginText _currentSessionTimeSensor = new("current-session-time", "Current Session Duration", "0m");
        private readonly PluginText _sessionStartTimeSensor = new("session-start-time", "Session Started At", "Not in game");
        private readonly PluginText _averageSessionTimeSensor = new("avg-session-time", "Avg Session Duration", "0m");
        
        // Library and Overall Playtime Statistics
        private readonly PluginSensor _totalGamesSensor = new("total-games", "Total Games Owned", 0, "");
        private readonly PluginSensor _totalPlaytimeSensor = new("total-playtime", "All Games Total Hours", 0, "hrs");
        private readonly PluginSensor _recentPlaytimeSensor = new("recent-playtime", "Recent Play Hours (2w)", 0, "hrs");
        private readonly PluginSensor _recentGamesCountSensor = new("recent-games-count", "Games Played Last 2w", 0, "games");
        private readonly PluginText _mostPlayedRecentSensor = new("most-played-recent", "Most Played Game (2w)", "None");
        private readonly PluginSensor _recentSessionsSensor = new("recent-sessions", "Gaming Sessions (2w)", 0, "sessions");
        private readonly PluginSensor _monitoredGamesCountSensor = new("monitored-games-count", "Top Games Being Tracked", 0, "games");
        private readonly PluginSensor _monitoredGamesTotalHoursSensor = new("monitored-total-hours", "Top Games Total Hours", 0, "hrs");
        
        // Achievements and Badges
        private readonly PluginSensor _currentGameAchievementsSensor = new("current-achievements", "Current Game Achievements", 0, "%");
        private readonly PluginSensor _currentGameAchievementsUnlockedSensor = new("achievements-unlocked", "Achievements Unlocked", 0, "");
        private readonly PluginSensor _currentGameAchievementsTotalSensor = new("achievements-total", "Total Achievements", 0, "");
        private readonly PluginText _latestAchievementSensor = new("latest-achievement", "Latest Achievement", "None");
        // Removed artificial achievement sensors (overall completion, total unlocked, percentile rank)
        // These would require analyzing achievement data across all owned games, not available via Steam Web API
        private readonly PluginSensor _totalBadgesEarnedSensor = new("total-badges-earned", "Steam Badges Earned", 0, "badges");
        private readonly PluginSensor _totalBadgeXPSensor = new("total-badge-xp", "Total Badge XP", 0, "XP");
        private readonly PluginText _latestBadgeSensor = new("latest-badge", "Latest Badge Earned", "None");
        
        // Friends and Social Activity
        private readonly PluginSensor _friendsOnlineSensor = new("friends-online", "Friends Online", 0, "friends");
        private readonly PluginSensor _friendsInGameSensor = new("friends-in-game", "Friends Gaming", 0, "friends");
        private readonly PluginSensor _totalFriendsCountSensor = new("total-friends-count", "Total Friends", 0, "friends");
        private readonly PluginText _friendActivityStatusSensor = new("friend-activity-status", "Friend Activity", "No recent activity");
        
        // Game-Specific Details and News
        private readonly PluginText _primaryGameStatsSensor = new("primary-game-stats", "Primary Game Stats", "No data");
        private readonly PluginText _secondaryGameStatsSensor = new("secondary-game-stats", "Secondary Game Stats", "No data");
        private readonly PluginText _tertiaryGameStatsSensor = new("tertiary-game-stats", "Tertiary Game Stats", "No data");
        private readonly PluginText _latestGameNewsSensor = new("latest-game-news", "Latest Game News", "No news");
        private readonly PluginSensor _unreadNewsCountSensor = new("unread-news-count", "Unread News", 0, "items");
        private readonly PluginText _mostActiveNewsGameSensor = new("most-active-news-game", "Most Active News Game", "None");
        
        // Tables
        private readonly PluginTable _recentGamesTable;
        private readonly PluginTable _gameStatsTable;
        private readonly PluginTable _friendsActivityTable;
        
        #endregion

        #region Services
        
        // NEW domain services
        private InfoPanel.SteamAPI.Services.Monitoring.PlayerMonitoringService? _playerMonitoring;
        private InfoPanel.SteamAPI.Services.Monitoring.SocialMonitoringService? _socialMonitoring;
        private InfoPanel.SteamAPI.Services.Monitoring.LibraryMonitoringService? _libraryMonitoring;
        
        private InfoPanel.SteamAPI.Services.Sensors.PlayerSensorService? _playerSensors;
        private InfoPanel.SteamAPI.Services.Sensors.SocialSensorService? _socialSensors;
        private InfoPanel.SteamAPI.Services.Sensors.LibrarySensorService? _librarySensors;
        
        // Shared infrastructure
        private ConfigurationService? _configService;
        private FileLoggingService? _loggingService;
        private EnhancedLoggingService? _enhancedLoggingService;
        private System.Threading.SemaphoreSlim? _apiSemaphore;
        
        // Data services (used by monitoring services)
        private PlayerDataService? _playerDataService;
        private SocialDataService? _socialDataService;
        private LibraryDataService? _libraryDataService;
        private SessionTrackingService? _sessionTrackingService;
        
        private CancellationTokenSource? _cancellationTokenSource;
        
        #endregion

        #region Constructor & Initialization
        
        public SteamAPIMain() : base(SteamAPIConstants.CONFIG_SECTION_NAME, SteamAPIConstants.PLUGIN_DESCRIPTION, SteamAPIConstants.PLUGIN_SUBTITLE)
        {
            try
            {
                // Initialize the Recent Games table
                _recentGamesTable = new PluginTable("Recent Games", new DataTable(), SteamAPIConstants.RECENT_GAMES_TABLE_FORMAT);
                
                // Initialize the Game Statistics table
                _gameStatsTable = new PluginTable("Game Statistics", new DataTable(), SteamAPIConstants.GAME_STATS_TABLE_FORMAT);
                
                // Initialize the Friends Activity table
                _friendsActivityTable = new PluginTable("Friends Activity", new DataTable(), SteamAPIConstants.FRIENDS_ACTIVITY_TABLE_FORMAT);
                
                // Note: _configFilePath will be set in Initialize()
                // ConfigurationService will be initialized after we have the path
                
            }
            catch (Exception ex)
            {
                // Log initialization errors
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Error during initialization: {ex.Message}");
                throw;
            }
        }

        public override void Initialize()
        {
            // This method may be called by InfoPanel framework
            // Our main initialization is in Load() method
        }

        /// <summary>
        /// Archives an orphaned enhanced log file from a previous session when logging is now disabled
        /// </summary>
        private void ArchiveOrphanedEnhancedLog(string logFilePath)
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    Console.WriteLine("[SteamAPI] No orphaned enhanced log file found");
                    return;
                }

                var fileInfo = new FileInfo(logFilePath);
                var directory = fileInfo.DirectoryName ?? string.Empty;
                var extension = fileInfo.Extension;
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var archivedPath = Path.Combine(directory, $"debug-{timestamp}{extension}");
                
                File.Move(logFilePath, archivedPath);
                Console.WriteLine($"[SteamAPI] Archived orphaned enhanced log: {Path.GetFileName(archivedPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Failed to archive orphaned log: {ex.Message}");
            }
        }

        public override void Load(List<IPluginContainer> containers)
        {
            try
            {
                // Set up configuration file path for InfoPanel integration
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string basePath = assembly.ManifestModule.FullyQualifiedName;
                _configFilePath = $"{basePath}.ini";
                
                // Initialize services now that we have the config path
                _configService = new ConfigurationService(_configFilePath);
                
                // Create EnhancedLoggingService only if enabled in configuration
                string enhancedLogPath = _configFilePath.Replace(".ini", "_enhanced.json");
                if (_configService.EnableEnhancedLogging)
                {
                    _enhancedLoggingService = new EnhancedLoggingService(enhancedLogPath, _configService);
                    Console.WriteLine("[SteamAPI] Enhanced logging enabled - service initialized");
                }
                else
                {
                    // Archive any orphaned log file from previous session when logging was enabled
                    ArchiveOrphanedEnhancedLog(enhancedLogPath);
                    Console.WriteLine("[SteamAPI] Enhanced logging disabled - service not created");
                }
                
                // Keep FileLoggingService temporarily for backward compatibility during transition
                _loggingService = new FileLoggingService(_configService);
                
                // Initialize shared API semaphore for rate limiting (one API call at a time across all domains)
                _apiSemaphore = new System.Threading.SemaphoreSlim(1, 1);
                
                // Initialize Steam API service
                var steamApiService = new SteamApiService(_configService.SteamApiKey, _configService.SteamId64, _loggingService, _enhancedLoggingService);
                
                // Initialize session tracking service (session file path is optional, uses default if not provided)
                var sessionFilePath = _configFilePath?.Replace(".ini", "_session.json");
                _sessionTrackingService = new SessionTrackingService(_loggingService, _enhancedLoggingService, sessionFilePath);
                
                // Initialize data collection services
                _playerDataService = new PlayerDataService(_configService, steamApiService, _sessionTrackingService, _loggingService, _enhancedLoggingService);
                _socialDataService = new SocialDataService(_configService, steamApiService, _loggingService, _enhancedLoggingService);
                _libraryDataService = new LibraryDataService(_configService, steamApiService, _loggingService, _enhancedLoggingService);
                
                // Initialize domain monitoring services
                _playerMonitoring = new InfoPanel.SteamAPI.Services.Monitoring.PlayerMonitoringService(
                    _configService,
                    _playerDataService,
                    _sessionTrackingService,
                    _apiSemaphore,
                    _enhancedLoggingService);
                
                _socialMonitoring = new InfoPanel.SteamAPI.Services.Monitoring.SocialMonitoringService(
                    _configService,
                    _socialDataService,
                    _apiSemaphore,
                    _enhancedLoggingService);
                
                _libraryMonitoring = new InfoPanel.SteamAPI.Services.Monitoring.LibraryMonitoringService(
                    _configService,
                    _libraryDataService,
                    _apiSemaphore,
                    _enhancedLoggingService);
                
                // Initialize domain sensor services
                _playerSensors = new InfoPanel.SteamAPI.Services.Sensors.PlayerSensorService(
                    _configService,
                    _playerNameSensor,
                    _onlineStatusSensor,
                    _steamLevelSensor,
                    _currentGameSensor,
                    _currentGamePlaytimeSensor,
                    _statusSensor,
                    _detailsSensor,
                    _currentSessionTimeSensor,
                    _sessionStartTimeSensor,
                    _averageSessionTimeSensor,
                    _profileImageUrlSensor,
                    _currentGameBannerUrlSensor,
                    _gameStatusTextSensor,
                    _enhancedLoggingService);
                
                _socialSensors = new InfoPanel.SteamAPI.Services.Sensors.SocialSensorService(
                    _configService,
                    _friendsOnlineSensor,
                    _friendsInGameSensor,
                    _totalFriendsCountSensor,
                    _friendsActivityTable,
                    _friendActivityStatusSensor,
                    _enhancedLoggingService);
                
                _librarySensors = new InfoPanel.SteamAPI.Services.Sensors.LibrarySensorService(
                    _configService,
                    _totalGamesSensor,
                    _totalPlaytimeSensor,
                    _recentPlaytimeSensor,
                    _recentGamesCountSensor,
                    _mostPlayedRecentSensor,
                    _recentGamesTable,
                    _enhancedLoggingService);
                
                // Subscribe sensor services to monitoring events
                _playerSensors.SubscribeToMonitoring(_playerMonitoring);
                _socialSensors.SubscribeToMonitoring(_socialMonitoring);
                _librarySensors.SubscribeToMonitoring(_libraryMonitoring);
                
                // Set session cache reference for social and library domains
                var sessionCache = _playerMonitoring.GetSessionCache();
                _socialMonitoring.SetSessionCache(sessionCache);
                _libraryMonitoring.SetSessionCache(sessionCache);
                
                // Get version from assembly
                var assemblyVersion = assembly.GetName().Version?.ToString() ?? "Unknown";
                
                // Log initialization with enhanced logging (if enabled)
                _enhancedLoggingService?.LogInfo("PLUGIN", "SteamAPI Plugin Initialization Started", new
                {
                    ConfigFilePath = _configFilePath,
                    EnhancedLogPath = enhancedLogPath,
                    Version = assemblyVersion
                });
                _loggingService.LogInfo("=== SteamAPI Plugin Initialization Started ===");
                _loggingService.LogInfo($"Config file path: {_configFilePath}");
                _loggingService.LogDebug("Services initialized successfully");
                
                // OLD: Subscribe to monolithic monitoring service events
                // _monitoringService.DataUpdated += OnDataUpdated;
                // NEW: Event subscriptions handled in sensor service initialization above
                
                // Create User Profile & Status container
                var profileContainer = new PluginContainer("SteamAPI-Profile", "User Profile & Status");
                profileContainer.Entries.Add(_playerNameSensor);
                profileContainer.Entries.Add(_onlineStatusSensor);
                profileContainer.Entries.Add(_steamLevelSensor);
                profileContainer.Entries.Add(_profileImageUrlSensor);
                profileContainer.Entries.Add(_currentGameBannerUrlSensor);
                profileContainer.Entries.Add(_gameStatusTextSensor);
                profileContainer.Entries.Add(_statusSensor);
                profileContainer.Entries.Add(_detailsSensor);
                _loggingService.LogInfo($"Created User Profile & Status container with {profileContainer.Entries.Count} sensors");
                containers.Add(profileContainer);
                
                // Create Current Game & Session container
                var sessionContainer = new PluginContainer("SteamAPI-Session", "Current Game & Session");
                sessionContainer.Entries.Add(_currentGameSensor);
                sessionContainer.Entries.Add(_currentGamePlaytimeSensor);
                sessionContainer.Entries.Add(_currentSessionTimeSensor);
                sessionContainer.Entries.Add(_sessionStartTimeSensor);
                sessionContainer.Entries.Add(_averageSessionTimeSensor);
                _loggingService.LogInfo($"Created Current Game & Session container with {sessionContainer.Entries.Count} sensors");
                containers.Add(sessionContainer);
                
                // Create Library & Playtime container
                var libraryContainer = new PluginContainer("SteamAPI-Library", "Library & Playtime Statistics");
                libraryContainer.Entries.Add(_totalGamesSensor);
                libraryContainer.Entries.Add(_totalPlaytimeSensor);
                libraryContainer.Entries.Add(_recentPlaytimeSensor);
                libraryContainer.Entries.Add(_recentGamesCountSensor);
                libraryContainer.Entries.Add(_mostPlayedRecentSensor);
                libraryContainer.Entries.Add(_recentSessionsSensor);
                libraryContainer.Entries.Add(_monitoredGamesCountSensor);
                libraryContainer.Entries.Add(_monitoredGamesTotalHoursSensor);
                libraryContainer.Entries.Add(_recentGamesTable);
                _loggingService.LogInfo($"Created Library & Playtime Statistics container with {libraryContainer.Entries.Count} items (8 sensors + 1 table)");
                containers.Add(libraryContainer);
                
                // Create Achievements & Badges container
                var achievementsContainer = new PluginContainer("SteamAPI-Achievements", "Achievements & Badges");
                achievementsContainer.Entries.Add(_currentGameAchievementsSensor);
                achievementsContainer.Entries.Add(_currentGameAchievementsUnlockedSensor);
                achievementsContainer.Entries.Add(_currentGameAchievementsTotalSensor);
                achievementsContainer.Entries.Add(_latestAchievementSensor);
                // Removed artificial achievement sensors - Steam API doesn't provide overall achievement statistics
                achievementsContainer.Entries.Add(_totalBadgesEarnedSensor);
                achievementsContainer.Entries.Add(_totalBadgeXPSensor);
                achievementsContainer.Entries.Add(_latestBadgeSensor);
                _loggingService.LogInfo($"Created Achievements & Badges container with {achievementsContainer.Entries.Count} sensors");
                containers.Add(achievementsContainer);
                
                // Create Friends & Social container
                var socialContainer = new PluginContainer("SteamAPI-Social", "Friends & Social Activity");
                socialContainer.Entries.Add(_friendsOnlineSensor);
                socialContainer.Entries.Add(_friendsInGameSensor);
                socialContainer.Entries.Add(_totalFriendsCountSensor);
                socialContainer.Entries.Add(_friendActivityStatusSensor);
                socialContainer.Entries.Add(_friendsActivityTable);
                _loggingService.LogInfo($"Created Friends & Social Activity container with {socialContainer.Entries.Count} items (4 sensors + 1 table)");
                containers.Add(socialContainer);
                
                // Create Game Details & News container
                var gameDetailsContainer = new PluginContainer("SteamAPI-GameDetails", "Game Details & News");
                gameDetailsContainer.Entries.Add(_primaryGameStatsSensor);
                gameDetailsContainer.Entries.Add(_secondaryGameStatsSensor);
                gameDetailsContainer.Entries.Add(_tertiaryGameStatsSensor);
                gameDetailsContainer.Entries.Add(_latestGameNewsSensor);
                gameDetailsContainer.Entries.Add(_unreadNewsCountSensor);
                gameDetailsContainer.Entries.Add(_mostActiveNewsGameSensor);
                gameDetailsContainer.Entries.Add(_gameStatsTable);
                _loggingService.LogInfo($"Created Game Details & News container with {gameDetailsContainer.Entries.Count} items (6 sensors + 1 table)");
                containers.Add(gameDetailsContainer);
                
                // Start monitoring
                _cancellationTokenSource = new CancellationTokenSource();
                _ = StartMonitoringAsync(_cancellationTokenSource.Token);
                
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Plugin initialized successfully - 6 containers created");
                _loggingService.LogInfo("SteamAPI plugin loaded successfully - all 6 containers created, monitoring started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Error during plugin initialization: {ex.Message}");
                throw;
            }
        }

        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(_configService?.UpdateIntervalSeconds ?? SteamAPIConstants.DEFAULT_UPDATE_INTERVAL_SECONDS);

        public override void Update()
        {
            // For synchronous updates if needed
            // Most of our work is done asynchronously in StartMonitoringAsync
        }

        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            // For async updates - the monitoring service handles timing automatically
            await Task.CompletedTask;
        }

        public override void Close()
        {
            try
            {
                // Cancel monitoring
                _cancellationTokenSource?.Cancel();
                
                // Unsubscribe sensor services from monitoring events
                if (_playerSensors != null && _playerMonitoring != null)
                {
                    _playerSensors.UnsubscribeFromMonitoring(_playerMonitoring);
                }
                
                if (_socialSensors != null && _socialMonitoring != null)
                {
                    _socialSensors.UnsubscribeFromMonitoring(_socialMonitoring);
                }
                
                if (_librarySensors != null && _libraryMonitoring != null)
                {
                    _librarySensors.UnsubscribeFromMonitoring(_libraryMonitoring);
                }
                
                // Dispose domain services
                _playerMonitoring?.Dispose();
                _socialMonitoring?.Dispose();
                _libraryMonitoring?.Dispose();
                
                _playerSensors?.Dispose();
                _socialSensors?.Dispose();
                _librarySensors?.Dispose();
                
                // Dispose shared services
                _apiSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Plugin closed successfully - all domain services disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Error during close: {ex.Message}");
            }
        }
        
        #endregion

        #region Monitoring
        
        private async Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Start all domain monitoring services
                _playerMonitoring?.StartMonitoring();
                _socialMonitoring?.StartMonitoring();
                _libraryMonitoring?.StartMonitoring();
                
                Console.WriteLine("[SteamAPI] All domain monitoring services started");
                _loggingService?.LogInfo("Domain monitoring services started: Player (1s), Social (15s), Library (45s)");
                
                // Keep task alive until cancellation
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _loggingService?.LogDebug("Monitoring cancelled");
                
                // Stop all monitoring services
                _playerMonitoring?.StopMonitoring();
                _socialMonitoring?.StopMonitoring();
                _libraryMonitoring?.StopMonitoring();
                
                Console.WriteLine("[SteamAPI] All domain monitoring services stopped");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Error in monitoring: {ex.Message}");
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Critical monitoring error: {ex.Message}");
            }
        }
        
        #endregion

        #region Event Handlers
        // Domain-driven architecture: Sensor updates handled automatically by domain sensor services
        // - PlayerSensorService subscribes to PlayerMonitoringService.PlayerDataUpdated
        // - SocialSensorService subscribes to SocialMonitoringService.SocialDataUpdated  
        // - LibrarySensorService subscribes to LibraryMonitoringService.LibraryDataUpdated
        
        #endregion

        #region Cleanup
        
        public void Dispose()
        {
            try
            {
                _loggingService?.LogInfo("SteamAPI plugin disposing...");
                
                // Cancel monitoring
                _cancellationTokenSource?.Cancel();
                
                // Unsubscribe sensor services from monitoring events
                if (_playerSensors != null && _playerMonitoring != null)
                {
                    _playerSensors.UnsubscribeFromMonitoring(_playerMonitoring);
                }
                
                if (_socialSensors != null && _socialMonitoring != null)
                {
                    _socialSensors.UnsubscribeFromMonitoring(_socialMonitoring);
                }
                
                if (_librarySensors != null && _libraryMonitoring != null)
                {
                    _librarySensors.UnsubscribeFromMonitoring(_libraryMonitoring);
                }
                
                // Dispose domain monitoring services
                _playerMonitoring?.Dispose();
                _socialMonitoring?.Dispose();
                _libraryMonitoring?.Dispose();
                
                // Dispose domain sensor services
                _playerSensors?.Dispose();
                _socialSensors?.Dispose();
                _librarySensors?.Dispose();
                
                // Dispose shared services
                _apiSemaphore?.Dispose();
                
                _loggingService?.LogInfo("SteamAPI plugin disposed successfully - all domain services cleaned up");
                _enhancedLoggingService?.Dispose(); // Dispose enhanced logging service
                _loggingService?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Plugin disposed - all domain services cleaned up");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{SteamAPIConstants.PLUGIN_NAME}] Error during disposal: {ex.Message}");
            }
        }
        
        #endregion
    }
}
