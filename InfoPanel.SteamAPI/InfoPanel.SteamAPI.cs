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
        
        // Steam Profile Sensors
        private readonly PluginText _playerNameSensor = new("player-name", "Player Name", "Unknown");
        private readonly PluginText _onlineStatusSensor = new("online-status", "Status", "Offline");
        private readonly PluginSensor _steamLevelSensor = new("steam-level", "Steam Level", 0, "");
        
        // Current Game Sensors
        private readonly PluginText _currentGameSensor = new("current-game", "Current Game", "Not Playing");
        private readonly PluginSensor _currentGamePlaytimeSensor = new("current-game-playtime", "Game Playtime", 0, "hrs");
        
        // Library Statistics Sensors
        private readonly PluginSensor _totalGamesSensor = new("total-games", "Games Owned", 0, "");
        private readonly PluginSensor _totalPlaytimeSensor = new("total-playtime", "Total Playtime", 0, "hrs");
        private readonly PluginSensor _recentPlaytimeSensor = new("recent-playtime", "Recent Playtime", 0, "hrs");
        
        // Status and Details
        private readonly PluginText _statusSensor = new("status", "Plugin Status", "Initializing...");
        private readonly PluginText _detailsSensor = new("details", "Details", "Loading Steam data...");
        
        // Phase 2: Enhanced Gaming Metrics
        // Recent Gaming Activity (2-week stats)
        private readonly PluginSensor _recentGamesCountSensor = new("recent-games-count", "Games Played (2w)", 0, "games");
        private readonly PluginText _mostPlayedRecentSensor = new("most-played-recent", "Top Recent Game", "None");
        private readonly PluginSensor _recentSessionsSensor = new("recent-sessions", "Gaming Sessions (2w)", 0, "sessions");
        
        // Session Time Tracking
        private readonly PluginSensor _currentSessionTimeSensor = new("current-session-time", "Current Session", 0, "min");
        private readonly PluginText _sessionStartTimeSensor = new("session-start-time", "Session Started", "Not in game");
        private readonly PluginSensor _averageSessionTimeSensor = new("avg-session-time", "Avg Session Length", 0, "min");
        
        // Friends Online Monitoring  
        private readonly PluginSensor _friendsOnlineSensor = new("friends-online", "Friends Online", 0, "friends");
        private readonly PluginSensor _friendsInGameSensor = new("friends-in-game", "Friends Gaming", 0, "friends");
        private readonly PluginText _friendsCurrentGameSensor = new("friends-current-game", "Popular Game", "None");
        
        // Achievement Tracking (for current game)
        private readonly PluginSensor _currentGameAchievementsSensor = new("current-achievements", "Achievements", 0, "%");
        private readonly PluginSensor _currentGameAchievementsUnlockedSensor = new("achievements-unlocked", "Unlocked", 0, "");
        private readonly PluginSensor _currentGameAchievementsTotalSensor = new("achievements-total", "Total", 0, "");
        private readonly PluginText _latestAchievementSensor = new("latest-achievement", "Latest Achievement", "None");

        // Recent Games Table
        private static readonly string _recentGamesTableFormat = "0:200|1:80|2:100";
        private readonly PluginTable _recentGamesTable;

        // Phase 3: Advanced Features
        // Detailed Game-Specific Statistics
        private readonly PluginText _primaryGameStatsSensor = new("primary-game-stats", "Primary Game Stats", "No data");
        private readonly PluginText _secondaryGameStatsSensor = new("secondary-game-stats", "Secondary Game Stats", "No data");
        private readonly PluginText _tertiaryGameStatsSensor = new("tertiary-game-stats", "Tertiary Game Stats", "No data");
        
        // Multiple Game Monitoring
        private readonly PluginSensor _monitoredGamesCountSensor = new("monitored-games-count", "Monitored Games", 0, "games");
        private readonly PluginSensor _monitoredGamesTotalHoursSensor = new("monitored-total-hours", "Monitored Total Hours", 0, "hrs");
        private readonly PluginSensor _monitoredGamesAvgRatingSensor = new("monitored-avg-rating", "Avg Game Rating", 0, "★");
        
        // Achievement Completion Tracking
        private readonly PluginSensor _overallAchievementCompletionSensor = new("overall-achievement-completion", "Overall Achievement %", 0, "%");
        private readonly PluginSensor _perfectGamesCountSensor = new("perfect-games-count", "Perfect Games", 0, "games");
        private readonly PluginSensor _totalAchievementsUnlockedSensor = new("total-achievements-unlocked", "Total Achievements", 0, "");
        private readonly PluginSensor _achievementCompletionRankSensor = new("achievement-completion-rank", "Achievement Rank", 0, "%ile");
        
        // News and Update Monitoring
        private readonly PluginText _latestGameNewsSensor = new("latest-game-news", "Latest Game News", "No news");
        private readonly PluginSensor _unreadNewsCountSensor = new("unread-news-count", "Unread News", 0, "items");
        private readonly PluginText _mostActiveNewsGameSensor = new("most-active-news-game", "Most Active News Game", "None");

        // Game Statistics Table
        private static readonly string _gameStatsTableFormat = "0:150|1:80|2:80|3:60|4:100";
        private readonly PluginTable _gameStatsTable;

        // Phase 4: Social & Community Features
        // Friends Activity Monitoring
        private readonly PluginSensor _totalFriendsCountSensor = new("total-friends-count", "Total Friends", 0, "friends");
        private readonly PluginSensor _recentlyActiveFriendsCountSensor = new("recently-active-friends", "Recently Active", 0, "friends");
        private readonly PluginText _friendActivityStatusSensor = new("friend-activity-status", "Friend Activity", "No recent activity");
        private readonly PluginText _mostActiveFriendSensor = new("most-active-friend", "Most Active Friend", "None");

        // Popular Games in Friend Network
        private readonly PluginText _trendingFriendGameSensor = new("trending-friend-game", "Trending Among Friends", "None");
        private readonly PluginSensor _friendNetworkGameCountSensor = new("friend-network-games", "Popular Friend Games", 0, "games");
        private readonly PluginText _topFriendGameSensor = new("top-friend-game", "Top Friend Game", "None");

        // Community Badge Tracking
        private readonly PluginSensor _totalBadgesEarnedSensor = new("total-badges-earned", "Badges Earned", 0, "badges");
        private readonly PluginSensor _totalBadgeXPSensor = new("total-badge-xp", "Badge XP", 0, "XP");
        private readonly PluginText _latestBadgeSensor = new("latest-badge", "Latest Badge", "None");
        private readonly PluginSensor _badgeCompletionRateSensor = new("badge-completion-rate", "Badge Completion", 0, "%");

        // Global Statistics Comparison
        private readonly PluginSensor _globalPlaytimePercentileSensor = new("global-playtime-percentile", "Playtime Percentile", 0, "%ile");
        private readonly PluginText _globalUserCategorySensor = new("global-user-category", "User Category", "Unknown");

        // Friends Activity Table
        private static readonly string _friendsActivityTableFormat = "0:150|1:100|2:80|3:120";
        private readonly PluginTable _friendsActivityTable;
        
        #endregion

        #region Services
        
        private MonitoringService? _monitoringService;
        private SensorManagementService? _sensorService;
        private ConfigurationService? _configService;
        private FileLoggingService? _loggingService;
        private CancellationTokenSource? _cancellationTokenSource;
        
        #endregion

        #region Constructor & Initialization
        
        public SteamAPIMain() : base("InfoPanel.SteamAPI", "Steam Data", "Get data from SteamAPI")
        {
            try
            {
                // Initialize the Recent Games table
                _recentGamesTable = new PluginTable("Recent Games", new DataTable(), _recentGamesTableFormat);
                
                // Initialize the Game Statistics table
                _gameStatsTable = new PluginTable("Game Statistics", new DataTable(), _gameStatsTableFormat);
                
                // Initialize the Friends Activity table
                _friendsActivityTable = new PluginTable("Friends Activity", new DataTable(), _friendsActivityTableFormat);
                
                // Note: _configFilePath will be set in Initialize()
                // ConfigurationService will be initialized after we have the path
                
                // TODO: Add any additional initialization logic here that doesn't require configuration
                
            }
            catch (Exception ex)
            {
                // Log initialization errors
                Console.WriteLine($"[SteamAPI] Error during initialization: {ex.Message}");
                throw;
            }
        }

        public override void Initialize()
        {
            // This method may be called by InfoPanel framework
            // Our main initialization is in Load() method
        }

        public override void Load(List<IPluginContainer> containers)
        {
            try
            {
                // Set up configuration file path for InfoPanel integration
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string basePath = assembly.ManifestModule.FullyQualifiedName;
                _configFilePath = $"{basePath}.ini";
                
                Console.WriteLine($"[SteamAPI] Config file path: {_configFilePath}");
                
                // Initialize services now that we have the config path
                _configService = new ConfigurationService(_configFilePath);
                _loggingService = new FileLoggingService(_configService);
                _sensorService = new SensorManagementService(_configService, _loggingService);
                _monitoringService = new MonitoringService(_configService, _loggingService);
                
                // Log initialization
                _loggingService.LogInfo("=== SteamAPI Plugin Initialization Started ===");
                _loggingService.LogInfo($"Config file path: {_configFilePath}");
                _loggingService.LogDebug("Services initialized successfully");
                
                // Subscribe to events
                _monitoringService.DataUpdated += OnDataUpdated;
                
                // Create sensor container
                var container = new PluginContainer("SteamAPI", "Basic Steam Data");
                
                // Add Steam sensors to container using the Entries collection
                container.Entries.Add(_playerNameSensor);
                container.Entries.Add(_onlineStatusSensor);
                container.Entries.Add(_steamLevelSensor);
                container.Entries.Add(_currentGameSensor);
                container.Entries.Add(_currentGamePlaytimeSensor);
                container.Entries.Add(_totalGamesSensor);
                container.Entries.Add(_totalPlaytimeSensor);
                container.Entries.Add(_recentPlaytimeSensor);
                container.Entries.Add(_statusSensor);
                container.Entries.Add(_detailsSensor);
                
                Console.WriteLine($"[SteamAPI] Added {container.Entries.Count} sensors to container '{container.Name}'");
                _loggingService.LogInfo($"Created Basic Steam Data container with {container.Entries.Count} sensors");
                
                // Register basic container with InfoPanel
                containers.Add(container);
                
                // Create Phase 2: Enhanced Gaming Data container
                var enhancedContainer = new PluginContainer("SteamAPI-Enhanced", "Enhanced Gaming Data");
                
                // Add Phase 2 sensors: Recent Gaming Activity
                enhancedContainer.Entries.Add(_recentGamesCountSensor);
                enhancedContainer.Entries.Add(_mostPlayedRecentSensor);
                enhancedContainer.Entries.Add(_recentSessionsSensor);
                
                // Add Phase 2 sensors: Session Time Tracking
                enhancedContainer.Entries.Add(_currentSessionTimeSensor);
                enhancedContainer.Entries.Add(_sessionStartTimeSensor);
                enhancedContainer.Entries.Add(_averageSessionTimeSensor);
                
                // Add Phase 2 sensors: Friends Online Monitoring
                enhancedContainer.Entries.Add(_friendsOnlineSensor);
                enhancedContainer.Entries.Add(_friendsInGameSensor);
                enhancedContainer.Entries.Add(_friendsCurrentGameSensor);
                
                // Add Phase 2 sensors: Achievement Tracking
                enhancedContainer.Entries.Add(_currentGameAchievementsSensor);
                enhancedContainer.Entries.Add(_currentGameAchievementsUnlockedSensor);
                enhancedContainer.Entries.Add(_currentGameAchievementsTotalSensor);
                enhancedContainer.Entries.Add(_latestAchievementSensor);
                
                // Add Recent Games Table
                enhancedContainer.Entries.Add(_recentGamesTable);
                
                Console.WriteLine($"[SteamAPI] Added {enhancedContainer.Entries.Count} items to container '{enhancedContainer.Name}' (13 sensors + 1 table)");
                _loggingService.LogInfo($"Created Enhanced Gaming Data container with {enhancedContainer.Entries.Count} items (13 sensors + 1 table)");
                
                // Register enhanced container with InfoPanel
                containers.Add(enhancedContainer);
                
                // Create Phase 3: Advanced Features container
                var advancedContainer = new PluginContainer("SteamAPI-Advanced", "Advanced Steam Features");
                
                // Add Phase 3 sensors: Detailed Game-Specific Statistics
                advancedContainer.Entries.Add(_primaryGameStatsSensor);
                advancedContainer.Entries.Add(_secondaryGameStatsSensor);
                advancedContainer.Entries.Add(_tertiaryGameStatsSensor);
                
                // Add Phase 3 sensors: Multiple Game Monitoring
                advancedContainer.Entries.Add(_monitoredGamesCountSensor);
                advancedContainer.Entries.Add(_monitoredGamesTotalHoursSensor);
                advancedContainer.Entries.Add(_monitoredGamesAvgRatingSensor);
                
                // Add Phase 3 sensors: Achievement Completion Tracking
                advancedContainer.Entries.Add(_overallAchievementCompletionSensor);
                advancedContainer.Entries.Add(_perfectGamesCountSensor);
                advancedContainer.Entries.Add(_totalAchievementsUnlockedSensor);
                advancedContainer.Entries.Add(_achievementCompletionRankSensor);
                
                // Add Phase 3 sensors: News and Update Monitoring
                advancedContainer.Entries.Add(_latestGameNewsSensor);
                advancedContainer.Entries.Add(_unreadNewsCountSensor);
                advancedContainer.Entries.Add(_mostActiveNewsGameSensor);
                
                // Add Game Statistics Table
                advancedContainer.Entries.Add(_gameStatsTable);
                
                Console.WriteLine($"[SteamAPI] Added {advancedContainer.Entries.Count} items to container '{advancedContainer.Name}' (12 sensors + 1 table)");
                _loggingService.LogInfo($"Created Advanced Steam Features container with {advancedContainer.Entries.Count} items (12 sensors + 1 table)");
                
                // Register advanced container with InfoPanel
                containers.Add(advancedContainer);
                
                // Create Phase 4: Social & Community Features container
                var socialContainer = new PluginContainer("SteamAPI-Social", "Social & Community Features");
                
                // Add Phase 4 sensors: Friends Activity Monitoring
                socialContainer.Entries.Add(_totalFriendsCountSensor);
                socialContainer.Entries.Add(_recentlyActiveFriendsCountSensor);
                socialContainer.Entries.Add(_friendActivityStatusSensor);
                socialContainer.Entries.Add(_mostActiveFriendSensor);
                
                // Add Phase 4 sensors: Popular Games in Friend Network
                socialContainer.Entries.Add(_trendingFriendGameSensor);
                socialContainer.Entries.Add(_friendNetworkGameCountSensor);
                socialContainer.Entries.Add(_topFriendGameSensor);
                
                // Add Phase 4 sensors: Community Badge Tracking
                socialContainer.Entries.Add(_totalBadgesEarnedSensor);
                socialContainer.Entries.Add(_totalBadgeXPSensor);
                socialContainer.Entries.Add(_latestBadgeSensor);
                socialContainer.Entries.Add(_badgeCompletionRateSensor);
                
                // Add Phase 4 sensors: Global Statistics Comparison
                socialContainer.Entries.Add(_globalPlaytimePercentileSensor);
                socialContainer.Entries.Add(_globalUserCategorySensor);
                
                // Add Friends Activity Table
                socialContainer.Entries.Add(_friendsActivityTable);
                
                Console.WriteLine($"[SteamAPI] Added {socialContainer.Entries.Count} items to container '{socialContainer.Name}' (13 sensors + 1 table)");
                _loggingService.LogInfo($"Created Social & Community Features container with {socialContainer.Entries.Count} items (13 sensors + 1 table)");
                
                // Register social container with InfoPanel
                containers.Add(socialContainer);
                
                // Start monitoring
                _cancellationTokenSource = new CancellationTokenSource();
                _ = StartMonitoringAsync(_cancellationTokenSource.Token);
                
                Console.WriteLine("[SteamAPI] Steam Data plugin loaded successfully - all 4 containers created (Basic + Enhanced + Advanced + Social & Community)");
                _loggingService.LogInfo("SteamAPI plugin loaded successfully - all 4 containers created, monitoring started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error during plugin initialization: {ex.Message}");
                throw;
            }
        }

        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(_configService?.UpdateIntervalSeconds ?? 30);

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
                
                // Unsubscribe from events
                if (_monitoringService != null)
                {
                    _monitoringService.DataUpdated -= OnDataUpdated;
                }
                
                // Dispose services
                _monitoringService?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Console.WriteLine("[SteamAPI] Plugin closed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error during close: {ex.Message}");
            }
        }
        
        #endregion

        #region Monitoring
        
        private async Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            try
            {
                // TODO: Implement your monitoring logic here
                // This is where you start your data collection process
                
                if (_monitoringService != null)
                {
                    await _monitoringService.StartMonitoringAsync(cancellationToken);
                }
                
                // Example: You might also start additional monitoring tasks
                // _ = MonitorSystemResourcesAsync(cancellationToken);
                // _ = MonitorNetworkConnectivityAsync(cancellationToken);
                
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Console.WriteLine("[SteamAPI] Monitoring cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error in monitoring: {ex.Message}");
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void OnDataUpdated(object? sender, DataUpdatedEventArgs e)
        {
            try
            {
                _loggingService?.LogDebug($"Data update received from {sender?.GetType().Name}");
                
                // Update Steam sensors with data from monitoring service
                if (_sensorService != null && e.Data != null)
                {
                    _loggingService?.LogDebug("Updating basic Steam sensors...");
                    _sensorService.UpdateSteamSensors(
                        _playerNameSensor,
                        _onlineStatusSensor,
                        _steamLevelSensor,
                        _currentGameSensor,
                        _currentGamePlaytimeSensor,
                        _totalGamesSensor,
                        _totalPlaytimeSensor,
                        _recentPlaytimeSensor,
                        _statusSensor,
                        _detailsSensor,
                        e.Data
                    );
                    
                    _loggingService?.LogDebug("Updating enhanced gaming sensors...");
                    // Update Phase 2: Enhanced Gaming sensors
                    _sensorService.UpdateEnhancedGamingSensors(
                        // Recent Gaming Activity
                        _recentGamesCountSensor,
                        _mostPlayedRecentSensor,
                        _recentSessionsSensor,
                        // Session Time Tracking
                        _currentSessionTimeSensor,
                        _sessionStartTimeSensor,
                        _averageSessionTimeSensor,
                        // Friends Online Monitoring
                        _friendsOnlineSensor,
                        _friendsInGameSensor,
                        _friendsCurrentGameSensor,
                        // Achievement Tracking
                        _currentGameAchievementsSensor,
                        _currentGameAchievementsUnlockedSensor,
                        _currentGameAchievementsTotalSensor,
                        _latestAchievementSensor,
                        e.Data
                    );
                    
                    _loggingService?.LogDebug("Updating advanced features sensors...");
                    // Update Phase 3: Advanced Features sensors
                    _sensorService.UpdateAdvancedFeaturesSensors(
                        // Detailed Game-Specific Statistics
                        _primaryGameStatsSensor,
                        _secondaryGameStatsSensor,
                        _tertiaryGameStatsSensor,
                        // Multiple Game Monitoring
                        _monitoredGamesCountSensor,
                        _monitoredGamesTotalHoursSensor,
                        _monitoredGamesAvgRatingSensor,
                        // Achievement Completion Tracking
                        _overallAchievementCompletionSensor,
                        _perfectGamesCountSensor,
                        _totalAchievementsUnlockedSensor,
                        _achievementCompletionRankSensor,
                        // News and Update Monitoring
                        _latestGameNewsSensor,
                        _unreadNewsCountSensor,
                        _mostActiveNewsGameSensor,
                        e.Data
                    );
                    
                    _loggingService?.LogDebug("Updating social & community features sensors...");
                    // Update Phase 4: Social & Community Features sensors
                    _sensorService.UpdateSocialFeaturesSensors(
                        // Friends Activity sensors
                        _totalFriendsCountSensor,
                        _recentlyActiveFriendsCountSensor,
                        _friendActivityStatusSensor,
                        _mostActiveFriendSensor,
                        // Friend Network Games sensors
                        _trendingFriendGameSensor,
                        _friendNetworkGameCountSensor,
                        _topFriendGameSensor,
                        // Community Badge sensors
                        _totalBadgesEarnedSensor,
                        _totalBadgeXPSensor,
                        _latestBadgeSensor,
                        _badgeCompletionRateSensor,
                        // Global Statistics sensors
                        _globalPlaytimePercentileSensor,
                        _globalUserCategorySensor,
                        e.Data
                    );
                    
                    // Update Recent Games Table
                    _loggingService?.LogDebug("Updating recent games table...");
                    _recentGamesTable.Value = BuildRecentGamesTable(e.Data);
                    
                    // Update Game Statistics Table
                    _loggingService?.LogDebug("Updating game statistics table...");
                    _gameStatsTable.Value = BuildGameStatisticsTable(e.Data);
                    
                    // Update Friends Activity Table
                    _loggingService?.LogDebug("Updating friends activity table...");
                    _friendsActivityTable.Value = BuildFriendsActivityTable(e.Data);
                    
                    _loggingService?.LogDebug("Sensors updated successfully");
                }
                else
                {
                    _loggingService?.LogWarning("SensorService or Data is null - cannot update sensors");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error updating sensors: {ex.Message}");
                _loggingService?.LogError("Error updating sensors", ex);
                _statusSensor.Value = "Error updating data";
            }
        }
        
        #endregion

        #region TODO: Add Your Custom Methods Here
        
        // TODO: Add any plugin-specific methods you need
        // Examples:
        
        // private async Task MonitorSystemResourcesAsync(CancellationToken cancellationToken)
        // {
        //     // Monitor CPU, memory, disk, etc.
        // }
        
        // private async Task MonitorNetworkConnectivityAsync(CancellationToken cancellationToken)
        // {
        //     // Monitor network connectivity, bandwidth, etc.
        // }
        
        // private void ProcessSpecialEvents(SpecialEventData eventData)
        // {
        //     // Handle special events or conditions
        // }
        
        #endregion

        #region Table Building Methods
        
        /// <summary>
        /// Builds the Recent Games table from Steam data
        /// </summary>
        private DataTable BuildRecentGamesTable(SteamData data)
        {
            var dataTable = new DataTable();
            try
            {
                InitializeRecentGamesTableColumns(dataTable);
                
                if (data.RecentGames != null && data.RecentGames.Count > 0)
                {
                    foreach (var game in data.RecentGames.OrderByDescending(g => g.Playtime2Weeks))
                    {
                        AddGameToRecentGamesTable(dataTable, game);
                    }
                    
                    _loggingService?.LogDebug($"Built Recent Games table with {dataTable.Rows.Count} games");
                }
                else
                {
                    _loggingService?.LogDebug("No recent games data available for table");
                }
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Error building Recent Games table", ex);
                Console.WriteLine($"[SteamAPI] Error building Recent Games table: {ex.Message}");
            }
            return dataTable;
        }
        
        /// <summary>
        /// Initializes columns for the Recent Games table
        /// </summary>
        private void InitializeRecentGamesTableColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("Game", typeof(PluginText));
            dataTable.Columns.Add("2w Hours", typeof(PluginText));
            dataTable.Columns.Add("Total Hours", typeof(PluginText));
        }
        
        /// <summary>
        /// Adds a game row to the Recent Games table
        /// </summary>
        private void AddGameToRecentGamesTable(DataTable dataTable, InfoPanel.SteamAPI.Services.RecentlyPlayedGame game)
        {
            var row = dataTable.NewRow();
            
            // Game name column
            row["Game"] = new PluginText($"recent-game_{game.AppId}", game.Name ?? "Unknown Game");
            
            // Recent playtime (2 weeks) in hours
            var recentHours = game.Playtime2Weeks / 60.0;
            row["2w Hours"] = new PluginText($"recent-hours_{game.AppId}", $"{recentHours:F1}h");
            
            // Total playtime in hours  
            var totalHours = game.PlaytimeForever / 60.0;
            row["Total Hours"] = new PluginText($"total-hours_{game.AppId}", $"{totalHours:F1}h");
            
            dataTable.Rows.Add(row);
        }

        /// <summary>
        /// Builds the Game Statistics table from Steam data
        /// </summary>
        private DataTable BuildGameStatisticsTable(SteamData data)
        {
            try
            {
                var dataTable = new DataTable();
                
                // Initialize table columns
                InitializeGameStatisticsTableColumns(dataTable);
                
                // Add monitored games data if available
                if (data.MonitoredGamesStats?.Any() == true)
                {
                    foreach (var gameStats in data.MonitoredGamesStats.Take(5))  // Show top 5 monitored games
                    {
                        AddGameToStatisticsTable(dataTable, gameStats);
                    }
                    
                    _loggingService?.LogDebug($"Built Game Statistics table with {dataTable.Rows.Count} games");
                }
                else
                {
                    _loggingService?.LogDebug("No monitored games data available for statistics table");
                }
                
                return dataTable;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Error building Game Statistics table", ex);
                Console.WriteLine($"[SteamAPI] Error building Game Statistics table: {ex.Message}");
                return new DataTable();
            }
        }

        /// <summary>
        /// Initializes columns for the Game Statistics table
        /// </summary>
        private void InitializeGameStatisticsTableColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("Game", typeof(PluginText));
            dataTable.Columns.Add("Total Hours", typeof(PluginText));
            dataTable.Columns.Add("Recent Hours", typeof(PluginText));
            dataTable.Columns.Add("Achievements", typeof(PluginText));
            dataTable.Columns.Add("Status", typeof(PluginText));
        }

        /// <summary>
        /// Adds a monitored game row to the Game Statistics table
        /// </summary>
        private void AddGameToStatisticsTable(DataTable dataTable, MonitoredGameStats gameStats)
        {
            var row = dataTable.NewRow();
            
            // Game name column with playing indicator
            var gameDisplayName = gameStats.IsCurrentlyPlaying ? $"▶ {gameStats.GameName}" : gameStats.GameName ?? "Unknown Game";
            row["Game"] = new PluginText($"stats-game_{gameStats.AppId}", gameDisplayName);
            
            // Total playtime
            row["Total Hours"] = new PluginText($"stats-total_{gameStats.AppId}", $"{gameStats.TotalHours:F1}h");
            
            // Recent playtime
            row["Recent Hours"] = new PluginText($"stats-recent_{gameStats.AppId}", $"{gameStats.RecentHours:F1}h");
            
            // Achievement progress
            var achievementText = gameStats.AchievementsTotal > 0 ? 
                $"{gameStats.AchievementCompletion:F0}% ({gameStats.AchievementsUnlocked}/{gameStats.AchievementsTotal})" : "N/A";
            row["Achievements"] = new PluginText($"stats-achievements_{gameStats.AppId}", achievementText);
            
            // Game status (currently playing, last played, rating)
            var statusText = gameStats.IsCurrentlyPlaying ? "Playing" : 
                gameStats.LastPlayed?.ToString("MMM dd") ?? "Unknown";
            if (gameStats.UserRating.HasValue)
            {
                statusText += $" ({gameStats.UserRating:F1}★)";
            }
            row["Status"] = new PluginText($"stats-status_{gameStats.AppId}", statusText);
            
            dataTable.Rows.Add(row);
        }

        /// <summary>
        /// Builds the Friends Activity table from Steam data
        /// </summary>
        private DataTable BuildFriendsActivityTable(SteamData data)
        {
            var dataTable = new DataTable();
            try
            {
                InitializeFriendsActivityTableColumns(dataTable);
                
                if (data.FriendsList != null && data.FriendsList.Count > 0)
                {
                    // Sort friends by most recent activity first
                    var sortedFriends = data.FriendsList
                        .OrderBy(f => f.PersonaState == "Offline" ? 1 : 0) // Online friends first
                        .ThenByDescending(f => f.LastOnline ?? DateTime.MinValue)
                        .Take(10); // Limit to top 10 for display
                    
                    foreach (var friend in sortedFriends)
                    {
                        AddFriendToActivityTable(dataTable, friend);
                    }
                    
                    _loggingService?.LogDebug($"Built Friends Activity table with {dataTable.Rows.Count} friends");
                }
                else
                {
                    _loggingService?.LogDebug("No friends activity data available for table");
                }
                
                return dataTable;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Error building Friends Activity table", ex);
                Console.WriteLine($"[SteamAPI] Error building Friends Activity table: {ex.Message}");
                return new DataTable();
            }
        }

        /// <summary>
        /// Initializes columns for the Friends Activity table
        /// </summary>
        private void InitializeFriendsActivityTableColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("Friend", typeof(PluginText));
            dataTable.Columns.Add("Status", typeof(PluginText));
            dataTable.Columns.Add("Playing", typeof(PluginText));
            dataTable.Columns.Add("Last Online", typeof(PluginText));
        }

        /// <summary>
        /// Adds a friend row to the Friends Activity table
        /// </summary>
        private void AddFriendToActivityTable(DataTable dataTable, SteamFriend friend)
        {
            var row = dataTable.NewRow();
            
            // Friend name column with status indicator
            var statusIndicator = friend.PersonaState switch
            {
                "Online" => "🟢",
                "In-Game" => "🎮",
                "Away" => "🟡",
                "Busy" => "🔴",
                _ => "⚫"
            };
            var friendDisplayName = $"{statusIndicator} {friend.PersonaName ?? "Unknown"}";
            row["Friend"] = new PluginText($"friend_{friend.SteamId64}", friendDisplayName);
            
            // Status column
            row["Status"] = new PluginText($"friend_status_{friend.SteamId64}", friend.PersonaState ?? "Unknown");
            
            // Currently playing game
            var currentGame = !string.IsNullOrEmpty(friend.CurrentGame) ? friend.CurrentGame : "Not in game";
            row["Playing"] = new PluginText($"friend_game_{friend.SteamId64}", currentGame);
            
            // Last online time
            var lastOnlineText = friend.LastOnline?.ToString("MMM dd HH:mm") ?? "Unknown";
            row["Last Online"] = new PluginText($"friend_lastonline_{friend.SteamId64}", lastOnlineText);
            
            dataTable.Rows.Add(row);
        }
        
        #endregion

        #region Cleanup
        
        public void Dispose()
        {
            try
            {
                _loggingService?.LogInfo("SteamAPI plugin disposing...");
                
                // Cancel monitoring
                _cancellationTokenSource?.Cancel();
                
                // Unsubscribe from events
                if (_monitoringService != null)
                {
                    _monitoringService.DataUpdated -= OnDataUpdated;
                }
                
                // Dispose services
                _monitoringService?.Dispose();
                _loggingService?.LogInfo("SteamAPI plugin disposed successfully");
                _loggingService?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Console.WriteLine("[SteamAPI] Plugin disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error during disposal: {ex.Message}");
            }
        }
        
        #endregion
    }
}