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
        
        // User Profile and Status
        private readonly PluginText _playerNameSensor = new("player-name", "Player Name", "Unknown");
        private readonly PluginText _onlineStatusSensor = new("online-status", "Status", "Offline");
        private readonly PluginSensor _steamLevelSensor = new("steam-level", "Steam Level", 0, "");
        private readonly PluginText _statusSensor = new("status", "Plugin Status", "Initializing...");
        private readonly PluginText _detailsSensor = new("details", "Details", "Loading Steam data...");
        private readonly PluginText _globalUserCategorySensor = new("global-user-category", "User Category", "Unknown");
        
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
        private readonly PluginSensor _overallAchievementCompletionSensor = new("overall-achievement-completion", "All Games Achievement %", 0, "%");
        private readonly PluginSensor _totalAchievementsUnlockedSensor = new("total-achievements-unlocked", "All Achievements Unlocked", 0, "");
        private readonly PluginSensor _achievementCompletionRankSensor = new("achievement-completion-rank", "Achievement Percentile Rank", 0, "%ile");
        private readonly PluginSensor _totalBadgesEarnedSensor = new("total-badges-earned", "Steam Badges Earned", 0, "badges");
        private readonly PluginSensor _totalBadgeXPSensor = new("total-badge-xp", "Total Badge XP", 0, "XP");
        private readonly PluginText _latestBadgeSensor = new("latest-badge", "Latest Badge Earned", "None");
        private readonly PluginSensor _badgeCompletionRateSensor = new("badge-completion-rate", "Badge Collection %", 0, "%");
        
        // Friends and Social Activity
        private readonly PluginSensor _friendsOnlineSensor = new("friends-online", "Friends Online", 0, "friends");
        private readonly PluginSensor _friendsInGameSensor = new("friends-in-game", "Friends Gaming", 0, "friends");
        private readonly PluginSensor _totalFriendsCountSensor = new("total-friends-count", "Total Friends", 0, "friends");
        private readonly PluginSensor _recentlyActiveFriendsCountSensor = new("recently-active-friends", "Recently Active", 0, "friends");
        private readonly PluginText _friendActivityStatusSensor = new("friend-activity-status", "Friend Activity", "No recent activity");
        private readonly PluginText _mostActiveFriendSensor = new("most-active-friend", "Most Active Friend", "None");
        private readonly PluginText _trendingFriendGameSensor = new("trending-friend-game", "Trending Among Friends", "None");
        
        // Game-Specific Details and News
        private readonly PluginText _primaryGameStatsSensor = new("primary-game-stats", "Primary Game Stats", "No data");
        private readonly PluginText _secondaryGameStatsSensor = new("secondary-game-stats", "Secondary Game Stats", "No data");
        private readonly PluginText _tertiaryGameStatsSensor = new("tertiary-game-stats", "Tertiary Game Stats", "No data");
        private readonly PluginText _latestGameNewsSensor = new("latest-game-news", "Latest Game News", "No news");
        private readonly PluginSensor _unreadNewsCountSensor = new("unread-news-count", "Unread News", 0, "items");
        private readonly PluginText _mostActiveNewsGameSensor = new("most-active-news-game", "Most Active News Game", "None");
        
        // Tables
        private static readonly string _recentGamesTableFormat = "0:200|1:80|2:100";
        private readonly PluginTable _recentGamesTable;
        private static readonly string _gameStatsTableFormat = "0:150|1:80|2:80|3:60|4:100";
        private readonly PluginTable _gameStatsTable;
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
                
                // Create User Profile & Status container
                var profileContainer = new PluginContainer("SteamAPI-Profile", "User Profile & Status");
                profileContainer.Entries.Add(_playerNameSensor);
                profileContainer.Entries.Add(_onlineStatusSensor);
                profileContainer.Entries.Add(_steamLevelSensor);
                profileContainer.Entries.Add(_statusSensor);
                profileContainer.Entries.Add(_detailsSensor);
                profileContainer.Entries.Add(_globalUserCategorySensor);
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
                achievementsContainer.Entries.Add(_overallAchievementCompletionSensor);
                achievementsContainer.Entries.Add(_totalAchievementsUnlockedSensor);
                achievementsContainer.Entries.Add(_achievementCompletionRankSensor);
                achievementsContainer.Entries.Add(_totalBadgesEarnedSensor);
                achievementsContainer.Entries.Add(_totalBadgeXPSensor);
                achievementsContainer.Entries.Add(_latestBadgeSensor);
                achievementsContainer.Entries.Add(_badgeCompletionRateSensor);
                _loggingService.LogInfo($"Created Achievements & Badges container with {achievementsContainer.Entries.Count} sensors");
                containers.Add(achievementsContainer);
                
                // Create Friends & Social container
                var socialContainer = new PluginContainer("SteamAPI-Social", "Friends & Social Activity");
                socialContainer.Entries.Add(_friendsOnlineSensor);
                socialContainer.Entries.Add(_friendsInGameSensor);
                socialContainer.Entries.Add(_totalFriendsCountSensor);
                socialContainer.Entries.Add(_recentlyActiveFriendsCountSensor);
                socialContainer.Entries.Add(_friendActivityStatusSensor);
                socialContainer.Entries.Add(_mostActiveFriendSensor);
                socialContainer.Entries.Add(_trendingFriendGameSensor);
                socialContainer.Entries.Add(_friendsActivityTable);
                _loggingService.LogInfo($"Created Friends & Social Activity container with {socialContainer.Entries.Count} items (7 sensors + 1 table)");
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
                
                Console.WriteLine("[SteamAPI] Plugin initialized successfully - 6 containers created");
                _loggingService.LogInfo("SteamAPI plugin loaded successfully - all 6 containers created, monitoring started");
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
                _loggingService?.LogDebug("Monitoring cancelled");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Error in monitoring: {ex.Message}");
                Console.WriteLine($"[SteamAPI] Critical monitoring error: {ex.Message}");
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
                    // Update Enhanced Gaming sensors
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
                        // Achievement Tracking
                        _currentGameAchievementsSensor,
                        _currentGameAchievementsUnlockedSensor,
                        _currentGameAchievementsTotalSensor,
                        _latestAchievementSensor,
                        e.Data
                    );
                    
                    _loggingService?.LogDebug("Updating advanced features sensors...");
                    // Update Advanced Features sensors
                    _sensorService.UpdateAdvancedFeaturesSensors(
                        // Detailed Game-Specific Statistics
                        _primaryGameStatsSensor,
                        _secondaryGameStatsSensor,
                        _tertiaryGameStatsSensor,
                        // Multiple Game Monitoring
                        _monitoredGamesCountSensor,
                        _monitoredGamesTotalHoursSensor,
                        // Achievement Completion Tracking
                        _overallAchievementCompletionSensor,
                        _totalAchievementsUnlockedSensor,
                        _achievementCompletionRankSensor,
                        // News and Update Monitoring
                        _latestGameNewsSensor,
                        _unreadNewsCountSensor,
                        _mostActiveNewsGameSensor,
                        e.Data
                    );
                    
                    _loggingService?.LogDebug("Updating social & community features sensors...");
                    // Update Social & Community Features sensors
                    _sensorService.UpdateSocialFeaturesSensors(
                        // Friends Activity sensors
                        _totalFriendsCountSensor,
                        _recentlyActiveFriendsCountSensor,
                        _friendActivityStatusSensor,
                        _mostActiveFriendSensor,
                        // Friend Network Games sensors
                        _trendingFriendGameSensor,
                        // Community Badge sensors
                        _totalBadgesEarnedSensor,
                        _totalBadgeXPSensor,
                        _latestBadgeSensor,
                        _badgeCompletionRateSensor,
                        // Global Statistics sensors
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
                _loggingService?.LogError("Error updating sensors", ex);
                Console.WriteLine($"[SteamAPI] Critical sensor update error: {ex.Message}");
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
                    foreach (var game in data.RecentGames.OrderByDescending(g => g.Playtime2weeks ?? 0))
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
        private void AddGameToRecentGamesTable(DataTable dataTable, SteamGame game)
        {
            var row = dataTable.NewRow();
            
            // Game name column
            row["Game"] = new PluginText($"recent-game_{game.AppId}", game.Name ?? "Unknown Game");
            
            // Recent playtime (2 weeks) in hours
            var recentHours = (game.Playtime2weeks ?? 0) / 60.0;
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
            
            // Game status (currently playing, last played)
            var statusText = gameStats.IsCurrentlyPlaying ? "Playing" : 
                gameStats.LastPlayed?.ToString("MMM dd") ?? "Unknown";
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
                    // Apply filtering based on configuration
                    var filteredFriends = FilterFriendsList(data.FriendsList);
                    
                    // Apply sorting based on configuration
                    var sortedFriends = SortFriendsList(filteredFriends);
                    
                    // Apply display limit if configured
                    var displayFriends = ApplyDisplayLimit(sortedFriends);
                    
                    // Display friends based on configuration
                    foreach (var friend in displayFriends)
                    {
                        AddFriendToActivityTable(dataTable, friend);
                    }
                    
                    var totalCount = sortedFriends.Count;
                    var displayCount = displayFriends.Count;
                    var limitInfo = totalCount != displayCount ? $" (showing {displayCount} of {totalCount})" : "";
                    
                    _loggingService?.LogDebug($"Built Friends Activity table with {dataTable.Rows.Count} friends{limitInfo} (Filter: {_configService?.FriendsFilter}, Sort: {_configService?.FriendsSortBy})");
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
                return new DataTable();
            }
        }

        /// <summary>
        /// Filters the friends list based on configuration settings
        /// </summary>
        private List<SteamFriend> FilterFriendsList(List<SteamFriend> friends)
        {
            var filter = (_configService?.FriendsFilter ?? "All").ToLowerInvariant();
            var now = DateTime.UtcNow;
            
            // Apply activity/time-based filter
            var filteredFriends = filter switch
            {
                "onlineonly" => friends.Where(f => f.OnlineStatus != "Offline").ToList(),
                "active3days" => friends.Where(f => IsFriendActiveWithinDays(f, 3, now)).ToList(),
                "active5days" => friends.Where(f => IsFriendActiveWithinDays(f, 5, now)).ToList(),
                "active7days" => friends.Where(f => IsFriendActiveWithinDays(f, 7, now)).ToList(),
                _ => friends // "all" or any other value
            };
            
            // Apply hidden statuses filter
            var hiddenStatuses = (_configService?.HiddenStatuses ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet();
            
            if (hiddenStatuses.Count > 0)
            {
                filteredFriends = filteredFriends
                    .Where(f => !hiddenStatuses.Contains(f.OnlineStatus.ToLowerInvariant()))
                    .ToList();
            }
            
            return filteredFriends;
        }
        
        /// <summary>
        /// Applies display limit to friends list if configured
        /// </summary>
        private List<SteamFriend> ApplyDisplayLimit(List<SteamFriend> friends)
        {
            var maxDisplay = _configService?.MaxFriendsToDisplay ?? 0;
            
            if (maxDisplay > 0 && friends.Count > maxDisplay)
            {
                return friends.Take(maxDisplay).ToList();
            }
            
            return friends;
        }
        
        /// <summary>
        /// Checks if a friend has been active within the specified number of days
        /// </summary>
        private bool IsFriendActiveWithinDays(SteamFriend friend, int days, DateTime now)
        {
            if (friend.OnlineStatus != "Offline") 
                return true; // Currently online counts as active
                
            if (friend.LastLogOff <= 0) 
                return false; // No logoff data available
                
            var lastActive = DateTimeOffset.FromUnixTimeSeconds(friend.LastLogOff).DateTime;
            var timeSinceActive = now - lastActive;
            return timeSinceActive.TotalDays <= days;
        }
        
        /// <summary>
        /// Sorts the friends list based on configuration settings
        /// </summary>
        private List<SteamFriend> SortFriendsList(List<SteamFriend> friends)
        {
            var sortBy = (_configService?.FriendsSortBy ?? "LastOnline").ToLowerInvariant();
            var descending = _configService?.SortDescending ?? true;
            
            _loggingService?.LogDebug($"[SortFriendsList] Raw config value: '{_configService?.FriendsSortBy}', Lowercase: '{sortBy}', Descending: {descending}");
            
            return sortBy switch
            {
                "playingfirst" => SortByPlayingFirst(friends, descending),
                
                "name" => descending 
                    ? friends.OrderByDescending(f => f.PersonaName).ToList()
                    : friends.OrderBy(f => f.PersonaName).ToList(),
                    
                "status" => descending
                    ? friends.OrderByDescending(f => GetStatusSortOrder(f.OnlineStatus)).ToList()
                    : friends.OrderBy(f => GetStatusSortOrder(f.OnlineStatus)).ToList(),
                    
                "lastonline" => descending
                    ? friends.OrderByDescending(f => GetLastOnlineSortKey(f)).ToList()
                    : friends.OrderBy(f => GetLastOnlineSortKey(f)).ToList(),
                    
                _ => friends // Default order
            };
        }
        
        /// <summary>
        /// Gets a sort order value for online status (Online = 3, Away/Busy = 2, Offline = 1)
        /// </summary>
        private int GetStatusSortOrder(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "online" => 3,
                "away" => 2,
                "busy" => 2,
                "snooze" => 2,
                _ => 1 // Offline or unknown
            };
        }
        
        /// <summary>
        /// Gets a sort key for last online time (online friends get current time)
        /// </summary>
        private long GetLastOnlineSortKey(SteamFriend friend)
        {
            if (friend.OnlineStatus != "Offline")
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // Currently online gets highest priority
                
            return friend.LastLogOff > 0 ? friend.LastLogOff : 0; // Use actual last logoff time
        }

        /// <summary>
        /// Sorts friends with those currently playing games first, then by online status and last online time
        /// </summary>
        private List<SteamFriend> SortByPlayingFirst(List<SteamFriend> friends, bool descending)
        {
            _loggingService?.LogDebug($"[SortByPlayingFirst] Sorting {friends.Count} friends with PlayingFirst mode (descending: {descending})");
            
            var playingFriends = friends.Where(f => IsCurrentlyPlaying(f)).ToList();
            var notPlayingFriends = friends.Where(f => !IsCurrentlyPlaying(f)).ToList();
            
            _loggingService?.LogDebug($"[SortByPlayingFirst] Found {playingFriends.Count} playing friends, {notPlayingFriends.Count} not playing");
            
            // Debug each friend's playing status
            foreach (var friend in friends.Take(5)) // Log first 5 friends for debugging
            {
                _loggingService?.LogDebug($"[SortByPlayingFirst] {friend.PersonaName}: GameName='{friend.GameName}', IsPlaying={IsCurrentlyPlaying(friend)}");
            }
            
            if (descending)
            {
                // Playing games first (descending priority)
                return friends
                    .OrderByDescending(f => IsCurrentlyPlaying(f) ? 3 : 0) // Playing = 3
                    .ThenByDescending(f => GetStatusSortOrder(f.OnlineStatus)) // Then by status
                    .ThenByDescending(f => GetLastOnlineSortKey(f)) // Then by last online
                    .ToList();
            }
            else
            {
                // Playing games first (ascending order still puts playing first)
                return friends
                    .OrderByDescending(f => IsCurrentlyPlaying(f) ? 3 : 0) // Playing always first
                    .ThenBy(f => GetStatusSortOrder(f.OnlineStatus)) // Then by status (ascending)
                    .ThenBy(f => GetLastOnlineSortKey(f)) // Then by last online (ascending)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if a friend is currently playing a game
        /// </summary>
        private bool IsCurrentlyPlaying(SteamFriend friend)
        {
            // Must be online and have a valid game name that's not "Not Playing"
            return friend.OnlineStatus != "Offline" && 
                   !string.IsNullOrWhiteSpace(friend.GameName) && 
                   !friend.GameName.Equals("Not Playing", StringComparison.OrdinalIgnoreCase);
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
        /// Adds a friend row to the Friends Activity table with detailed profile information
        /// </summary>
        private void AddFriendToActivityTable(DataTable dataTable, SteamFriend friend)
        {
            var row = dataTable.NewRow();
            
            // Format friend name according to configuration
            var friendName = FormatFriendName(friend);
            row["Friend"] = new PluginText($"friend_{friend.SteamId}", friendName);
            
            // Online status (use detailed status if available, fallback to relationship)
            var statusText = !string.IsNullOrEmpty(friend.OnlineStatus) ? friend.OnlineStatus : friend.Relationship;
            row["Status"] = new PluginText($"friend_status_{friend.SteamId}", statusText ?? "Unknown");
            
            // Currently playing game
            var gameText = !string.IsNullOrEmpty(friend.GameName) ? friend.GameName : "Not Playing";
            row["Playing"] = new PluginText($"friend_game_{friend.SteamId}", gameText);
            
            // Format last online time according to configuration
            var lastOnlineText = FormatLastSeenTime(friend);
            row["Last Online"] = new PluginText($"friend_since_{friend.SteamId}", lastOnlineText);
            
            dataTable.Rows.Add(row);
        }
        
        /// <summary>
        /// Formats friend name according to configuration settings
        /// </summary>
        private string FormatFriendName(SteamFriend friend)
        {
            var baseName = !string.IsNullOrEmpty(friend.PersonaName) ? friend.PersonaName : friend.SteamId;
            var nameDisplay = (_configService?.FriendNameDisplay ?? "DisplayName").ToLowerInvariant();
            
            return nameDisplay switch
            {
                "withstatus" => $"{GetStatusIndicator(friend.OnlineStatus)} {baseName}",
                "truncated" => TruncateName(baseName),
                _ => baseName // "displayname" or default
            };
        }
        
        /// <summary>
        /// Gets a status indicator character for the friend's online status
        /// </summary>
        private string GetStatusIndicator(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "online" => "●",
                "away" => "◐",
                "busy" => "◒",
                "snooze" => "◑",
                _ => "○" // Offline or unknown
            };
        }
        
        /// <summary>
        /// Truncates friend name if it exceeds the configured maximum length
        /// </summary>
        private string TruncateName(string name)
        {
            var maxLength = _configService?.MaxFriendNameLength ?? 20;
            
            if (name.Length <= maxLength)
                return name;
                
            return name.Substring(0, maxLength - 3) + "...";
        }
        
        /// <summary>
        /// Formats last seen time according to configuration settings
        /// </summary>
        private string FormatLastSeenTime(SteamFriend friend)
        {
            var format = (_configService?.LastSeenFormat ?? "Smart").ToLowerInvariant();
            
            if (friend.OnlineStatus != "Offline")
                return "Online Now";
                
            if (friend.LastLogOff <= 0)
            {
                // Fallback to friend since date if no last logoff data
                var friendSince = DateTimeOffset.FromUnixTimeSeconds(friend.FriendSince);
                return $"Since {friendSince:MMM dd, yyyy}";
            }
            
            var lastOnline = DateTimeOffset.FromUnixTimeSeconds(friend.LastLogOff);
            var timeSince = DateTime.UtcNow - lastOnline.DateTime;
            
            return format switch
            {
                "relative" => FormatRelativeTime(timeSince),
                "datetime" => lastOnline.ToString("MMM dd, h:mm tt"),
                "smart" => timeSince.TotalDays < 7 ? FormatRelativeTime(timeSince) : lastOnline.ToString("MMM dd"),
                _ => FormatRelativeTime(timeSince)
            };
        }
        
        /// <summary>
        /// Formats time span as relative time (e.g., "2 hours ago", "3 days ago")
        /// </summary>
        private string FormatRelativeTime(TimeSpan timeSince)
        {
            if (timeSince.TotalMinutes < 60)
                return $"{(int)timeSince.TotalMinutes}m ago";
            else if (timeSince.TotalHours < 24)
                return $"{(int)timeSince.TotalHours}h ago";
            else if (timeSince.TotalDays < 30)
                return $"{(int)timeSince.TotalDays}d ago";
            else
                return $"{(int)(timeSince.TotalDays / 30)}mo ago";
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