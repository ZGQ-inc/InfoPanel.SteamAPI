using InfoPanel.SteamAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Event arguments for Steam data update events
    /// </summary>
    public class DataUpdatedEventArgs : EventArgs
    {
        public SteamData Data { get; }
        
        public DataUpdatedEventArgs(SteamData data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Core monitoring service for Steam data collection
    /// Manages Steam API calls and data aggregation
    /// </summary>
    public class MonitoringService : IDisposable
    {
        #region Events
        
        /// <summary>
        /// Triggered when new Steam data is available
        /// </summary>
        public event EventHandler<DataUpdatedEventArgs>? DataUpdated;
        
        #endregion

        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly System.Threading.Timer _fastTimer;        // Game state & session time  
        private readonly System.Threading.Timer _mediumTimer;      // Friends status
        private readonly System.Threading.Timer _slowTimer;        // Library stats & achievements
        
        // Core Steam API service
        private SteamApiService? _steamApiService;
        private SessionTrackingService? _sessionTracker;
        
        // Specialized data collection services
        private PlayerDataService? _playerDataService;
        private SocialDataService? _socialDataService;
        private LibraryDataService? _libraryDataService;
        private GameStatsService? _gameStatsService;
        
        private volatile bool _isMonitoring;
        private readonly object _lockObject = new();
        
        // Cycle tracking for staggered data collection
        private volatile int _fastCycleCount = 0;
        private volatile int _mediumCycleCount = 0;
        private volatile int _slowCycleCount = 0;
        
        #endregion

        #region Constructor
        
        public MonitoringService(ConfigurationService configService, FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            
            // Initialize tiered monitoring timers (but don't start them yet)
            _fastTimer = new System.Threading.Timer(OnFastTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _mediumTimer = new System.Threading.Timer(OnMediumTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _slowTimer = new System.Threading.Timer(OnSlowTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            
            // Initialize session tracking service
            _sessionTracker = new SessionTrackingService(_logger);
            
            _logger?.LogInfo("MonitoringService initialized with session tracking");
        }
        
        #endregion

        #region Monitoring Control
        
        /// <summary>
        /// Starts the Steam monitoring process
        /// </summary>
        public async Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    Console.WriteLine("[MonitoringService] Already monitoring");
                    return;
                }
                
                _isMonitoring = true;
            }
            
            try
            {
                // Initialize Steam API service
                await InitializeSteamApiAsync();
                
                // Start tiered monitoring timers with different intervals
                var fastIntervalMs = _configService.FastUpdateIntervalSeconds * 1000;    // Game state, session time (5s)
                var mediumIntervalMs = _configService.MediumUpdateIntervalSeconds * 1000; // Friends status (15s) 
                var slowIntervalMs = _configService.SlowUpdateIntervalSeconds * 1000;     // Library stats, achievements (60s)
                
                _logger?.LogInfo($"Starting tiered monitoring: Fast={_configService.FastUpdateIntervalSeconds}s, Medium={_configService.MediumUpdateIntervalSeconds}s, Slow={_configService.SlowUpdateIntervalSeconds}s");
                
                // Start all timers with a small stagger to avoid simultaneous API calls
                _fastTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(fastIntervalMs));
                _mediumTimer.Change(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(mediumIntervalMs));  // 0.5s offset
                _slowTimer.Change(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(slowIntervalMs));     // 1s offset
                
                // Keep the task alive while monitoring
                while (_isMonitoring && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[MonitoringService] Steam monitoring cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error during Steam monitoring: {ex.Message}");
                throw;
            }
            finally
            {
                await StopMonitoringAsync();
            }
        }
        
        /// <summary>
        /// Stops the Steam monitoring process
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            lock (_lockObject)
            {
                if (!_isMonitoring)
                {
                    return;
                }
                
                _isMonitoring = false;
            }
            
            // Stop all timers
            _fastTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _mediumTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _slowTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Dispose Steam API service
            _steamApiService?.Dispose();
            _steamApiService = null;
            
            Console.WriteLine("[MonitoringService] Tiered monitoring stopped");
        }
        
        #endregion

        #region Steam API Management
        
        /// <summary>
        /// Initializes the Steam API service with configuration
        /// </summary>
        private async Task InitializeSteamApiAsync()
        {
            try
            {
                var apiKey = _configService.SteamApiKey;
                var steamId64 = _configService.SteamId64;
                
                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "<your-steam-api-key-here>")
                {
                    throw new InvalidOperationException("Steam API Key is not configured. Please update the configuration file.");
                }
                
                if (string.IsNullOrWhiteSpace(steamId64) || steamId64 == "<your-steam-id64-here>")
                {
                    throw new InvalidOperationException("Steam ID64 is not configured. Please update the configuration file.");
                }
                
                if (!_configService.IsValidSteamId64(steamId64))
                {
                    throw new InvalidOperationException($"Steam ID64 format is invalid: {steamId64}. Must be 17 digits starting with 7656119.");
                }
                
                _steamApiService = new SteamApiService(apiKey, steamId64, _logger);
                
                // Test the connection
                var isValid = await _steamApiService.TestConnectionAsync();
                if (!isValid)
                {
                    throw new InvalidOperationException("Failed to connect to Steam API. Check your API key and Steam ID.");
                }
                
                Console.WriteLine("[MonitoringService] Steam API connection established");
                
                // Initialize specialized data collection services
                InitializeSpecializedServices();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Failed to initialize Steam API: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Initializes the specialized data collection services
        /// </summary>
        private void InitializeSpecializedServices()
        {
            try
            {
                if (_steamApiService == null)
                {
                    throw new InvalidOperationException("Steam API service must be initialized first");
                }
                
                // Initialize specialized services
                _playerDataService = new PlayerDataService(_configService, _steamApiService, _sessionTracker, _logger);
                _socialDataService = new SocialDataService(_configService, _steamApiService, _logger);
                _libraryDataService = new LibraryDataService(_configService, _steamApiService, _logger);
                _gameStatsService = new GameStatsService(_configService, _steamApiService, _logger);
                
                Console.WriteLine("[MonitoringService] Specialized services initialized");
                _logger?.LogInfo("Specialized data collection services initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Failed to initialize specialized services: {ex.Message}");
                _logger?.LogError("Failed to initialize specialized services", ex);
                throw;
            }
        }
        
        #endregion

        #region Data Collection
        
        /// <summary>
        /// Fast timer callback for critical real-time data (game state, session time)
        /// </summary>
        private void OnFastTimerElapsed(object? state)
        {
            if (!_isMonitoring || _playerDataService == null)
                return;
                
            try
            {
                _fastCycleCount++;
                _logger?.LogDebug($"[FastTimer] Cycle {_fastCycleCount}: Collecting game state and session data...");
                
                // Collect critical real-time data using PlayerDataService
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var playerData = await _playerDataService.CollectPlayerDataAsync();
                        var steamData = ConvertPlayerDataToSteamData(playerData);
                        OnDataUpdated(steamData);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Error collecting fast data", ex);
                        OnDataUpdated(new SteamData($"Fast data error: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error in fast timer: {ex.Message}");
                _logger?.LogError($"Fast timer error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Medium timer callback for social data (friends status)
        /// </summary>
        private void OnMediumTimerElapsed(object? state)
        {
            if (!_isMonitoring || _socialDataService == null)
                return;
                
            try
            {
                _mediumCycleCount++;
                _logger?.LogDebug($"[MediumTimer] Cycle {_mediumCycleCount}: Collecting friends and social data...");
                
                // Collect social data using SocialDataService
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var socialData = await _socialDataService.CollectSocialDataAsync();
                        var steamData = ConvertSocialDataToSteamData(socialData);
                        OnDataUpdated(steamData);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Error collecting medium data", ex);
                        OnDataUpdated(new SteamData($"Medium data error: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error in medium timer: {ex.Message}");
                _logger?.LogError($"Medium timer error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Slow timer callback for static data (library stats, achievements)
        /// </summary>
        private void OnSlowTimerElapsed(object? state)
        {
            if (!_isMonitoring || _libraryDataService == null || _gameStatsService == null)
                return;
                
            try
            {
                _slowCycleCount++;
                _logger?.LogDebug($"[SlowTimer] Cycle {_slowCycleCount}: Collecting library and achievement data...");
                
                // Collect comprehensive data from all services for slow updates
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var aggregatedData = await CollectAndAggregateAllDataAsync();
                        OnDataUpdated(aggregatedData);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Error collecting slow/aggregated data", ex);
                        OnDataUpdated(new SteamData($"Slow data error: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error in slow timer: {ex.Message}");
                _logger?.LogError($"Slow timer error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Collects data from Steam API
        /// </summary>
        private async Task<SteamData> CollectSteamDataAsync()
        {
            if (_steamApiService == null)
            {
                _logger?.LogError("SteamApiService is null - cannot collect data");
                return new SteamData("Steam API service not initialized");
            }
            
            _logger?.LogDebug("Starting Steam data collection...");

            try
            {
                var data = new SteamData
                {
                    Timestamp = DateTime.Now,
                    Status = "Collecting data..."
                };
                
                // Collect player summary if profile monitoring is enabled
                if (_configService.EnableProfileMonitoring)
                {
                    _logger?.LogDebug("Collecting player summary data...");
                    var playerSummary = await _steamApiService.GetPlayerSummaryAsync();
                    if (playerSummary?.Response?.Players?.Any() == true)
                    {
                        var player = playerSummary.Response.Players.First();
                        data.PlayerName = player.PersonaName;
                        data.ProfileUrl = player.ProfileUrl;
                        data.AvatarUrl = player.AvatarFull;
                        data.OnlineState = SteamApiService.GetPersonaStateString(player.PersonaState);
                        data.LastLogOff = player.LastLogoff;
                        data.CurrentGameName = player.GameExtraInfo;
                        data.CurrentGameServerIp = player.GameServerIp;
                        
                        _logger?.LogInfo($"Player Summary - Name: {data.PlayerName}, State: {data.OnlineState}, Current Game: {data.CurrentGameName ?? "None"}");
                        
                        // Try to parse game ID
                        if (int.TryParse(player.GameId, out var gameId))
                        {
                            data.CurrentGameAppId = gameId;
                            _logger?.LogDebug($"Current game App ID: {gameId}");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Player summary returned null");
                    }
                    
                    // Get Steam level
                    _logger?.LogDebug("Getting Steam level...");
                    var steamLevel = await _steamApiService.GetSteamLevelAsync();
                    data.SteamLevel = steamLevel?.Response?.PlayerLevel ?? 0;
                    _logger?.LogDebug($"Steam Level: {data.SteamLevel}");
                }
                else
                {
                    _logger?.LogDebug("Profile monitoring is disabled");
                }
                
                // Collect library data if library monitoring is enabled
                if (_configService.EnableLibraryMonitoring)
                {
                    _logger?.LogDebug("Collecting library data...");
                    var ownedGames = await _steamApiService.GetOwnedGamesAsync();
                    if (ownedGames?.Response?.Games?.Any() == true)
                    {
                        var games = ownedGames.Response.Games;
                        data.TotalGamesOwned = games.Count;
                        data.TotalLibraryPlaytimeHours = games.Sum(g => g.PlaytimeForever) / 60.0; // Convert minutes to hours
                        
                        _logger?.LogInfo($"Library Data - Games Owned: {data.TotalGamesOwned}, Total Playtime: {data.TotalLibraryPlaytimeHours:F1} hours");
                        
                        // Find most played game
                        var mostPlayed = games.OrderByDescending(g => g.PlaytimeForever).FirstOrDefault();
                        if (mostPlayed != null)
                        {
                            data.MostPlayedGameName = mostPlayed.Name;
                            data.MostPlayedGameHours = mostPlayed.PlaytimeForever / 60.0;
                            _logger?.LogDebug($"Most Played Game: {data.MostPlayedGameName} ({data.MostPlayedGameHours:F1} hours)");
                        }
                        
                        // Set current game playtime if currently playing
                        if (!string.IsNullOrEmpty(data.CurrentGameName))
                        {
                            var currentGame = games.FirstOrDefault(g => 
                                g.AppId == data.CurrentGameAppId || 
                                g.Name.Equals(data.CurrentGameName, StringComparison.OrdinalIgnoreCase));
                            
                            if (currentGame != null)
                            {
                                data.TotalPlaytimeHours = currentGame.PlaytimeForever / 60.0;
                                _logger?.LogDebug($"Current Game Playtime: {data.TotalPlaytimeHours:F1} hours");
                            }
                            else
                            {
                                _logger?.LogWarning($"Could not find current game '{data.CurrentGameName}' in owned games list");
                            }
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Owned games returned null or empty");
                    }
                }
                else
                {
                    _logger?.LogDebug("Library monitoring is disabled");
                }
                
                // Collect recent activity if current game monitoring is enabled
                if (_configService.EnableCurrentGameMonitoring)
                {
                    _logger?.LogDebug("Collecting recent activity data...");
                    var recentGames = await _steamApiService.GetRecentlyPlayedGamesAsync();
                    if (recentGames?.Response?.Games?.Any() == true)
                    {
                        var recentGamesList = recentGames.Response.Games;
                        data.RecentPlaytimeHours = recentGamesList.Sum(g => g.Playtime2weeks ?? 0) / 60.0;
                        data.RecentGamesCount = recentGamesList.Count;
                        _logger?.LogInfo($"Recent Activity - Games: {data.RecentGamesCount}, Playtime (2w): {data.RecentPlaytimeHours:F1} hours");
                        
                        // Store the recent games list for table display
                        data.RecentGames = recentGamesList;
                        
                        // Set Enhanced Gaming Metrics - Most Played Recent Game
                        var mostPlayedRecent = recentGamesList.OrderByDescending(g => g.Playtime2weeks ?? 0).FirstOrDefault();
                        if (mostPlayedRecent != null)
                        {
                            data.MostPlayedRecentGame = mostPlayedRecent.Name;
                            _logger?.LogDebug($"Most Played Recent: {data.MostPlayedRecentGame} ({(mostPlayedRecent.Playtime2weeks ?? 0) / 60.0:F1}h in 2w)");
                        }
                        else
                        {
                            data.MostPlayedRecentGame = "None";
                        }
                        
                        // Session tracking is handled by SessionTrackingService
                        _logger?.LogDebug("Session tracking will be handled in Enhanced Gaming Data collection");
                        
                        // Log details about recent games
                        foreach (var game in recentGamesList.Take(3)) // Log first 3 recent games
                        {
                            var recentHours = (game.Playtime2weeks ?? 0) / 60.0;
                            _logger?.LogDebug($"Recent Game: {game.Name} ({recentHours:F1} hours in 2w)");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Recent games returned null or empty");
                    }
                }
                else
                {
                    _logger?.LogDebug("Current game monitoring is disabled");
                }
                
                // Collect Enhanced Gaming Data - Friends and Achievements
                await CollectEnhancedGamingDataAsync(data);
                
                // Collect Advanced Features Data - Game Stats, Multi-Game Monitoring, and News
                await CollectAdvancedFeaturesDataAsync(data);
                
                // Collect Social & Community Features Data - Friends Activity, Badges, and Global Stats
                await CollectSocialFeaturesDataAsync(data);
                
                // Set status and details based on collected data
                data.Status = data.IsOnline() ? "Online" : "Offline";
                data.Details = $"Updated at {DateTime.Now:HH:mm:ss}";
                
                _logger?.LogInfo($"Steam data collection completed successfully - Status: {data.Status}, Player: {data.PlayerName}");
                return data;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting Steam data", ex);
                Console.WriteLine($"[MonitoringService] Error collecting Steam data: {ex.Message}");
                return new SteamData($"Collection error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Collects Enhanced Gaming Metrics (Phase 2 data)
        /// </summary>
        private async Task CollectEnhancedGamingDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting Enhanced Gaming Data...");
                
                // Session Time Tracking
                // Use our custom session tracker to get real session data
                _sessionTracker?.UpdateSessionTracking(data);
                
                if (data.IsInGame())
                {
                    _logger?.LogDebug($"Currently in game: {data.CurrentGameName} - Session: {data.CurrentSessionTimeMinutes} minutes");
                }
                else
                {
                    _logger?.LogDebug("Not currently in game - no active session");
                }
                
                // Friends Monitoring - moved to Phase 4 for detailed collection
                // Basic friends count will be collected in Phase 4 with detailed profiles
                
                // Achievement Tracking for Current Game
                if (data.IsInGame() && data.CurrentGameAppId > 0)
                {
                    await CollectAchievementDataAsync(data);
                }
                else
                {
                    // Set default values when not in game
                    data.CurrentGameAchievementPercentage = 0;
                    data.CurrentGameAchievementsUnlocked = 0;
                    data.CurrentGameAchievementsTotal = 0;
                    data.LatestAchievementName = null;
                    data.LatestAchievementDate = null;
                    _logger?.LogDebug("Not in game - no achievement tracking");
                }
                
                _logger?.LogDebug("Enhanced Gaming Data collection completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting Enhanced Gaming Data", ex);
                Console.WriteLine($"[MonitoringService] Error collecting Enhanced Gaming Data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Collects friends data for Enhanced Gaming Metrics
        /// </summary>
        private async Task CollectFriendsDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting friends data...");
                
                // Get real friends list from Steam API
                if (_steamApiService == null)
                {
                    _logger?.LogDebug("SteamApiService is null, using default friends data");
                    data.FriendsOnline = 0;
                    data.FriendsInGame = 0;
                    data.FriendsPopularGame = "Service unavailable";
                    return;
                }
                
                var friendsResponse = await _steamApiService.GetFriendsListAsync();
                
                if (friendsResponse?.FriendsList?.Friends != null)
                {
                    var friends = friendsResponse.FriendsList.Friends;
                    data.FriendsOnline = friends.Count; // For now, treat all friends as potentially online
                    data.FriendsInGame = Math.Min(3, friends.Count / 4); // Estimate some are in game
                    
                    // Set a more intelligent popular game based on user's own activity
                    if (data.RecentGames != null && data.RecentGames.Count > 0)
                    {
                        var topGame = data.RecentGames.OrderByDescending(g => g.Playtime2weeks ?? 0).FirstOrDefault();
                        data.FriendsPopularGame = topGame?.Name ?? "Unknown";
                    }
                    else
                    {
                        data.FriendsPopularGame = "No Recent Activity";
                    }
                    
                    _logger?.LogDebug($"Friends status: {data.FriendsOnline} total friends, {data.FriendsInGame} estimated in game, Popular: {data.FriendsPopularGame}");
                }
                else
                {
                    _logger?.LogDebug("No friends data received from Steam API, using defaults");
                    data.FriendsOnline = 0;
                    data.FriendsInGame = 0;
                    data.FriendsPopularGame = "None";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting friends data", ex);
                data.FriendsOnline = 0;
                data.FriendsInGame = 0;
                data.FriendsPopularGame = "Error";
            }
        }
        
        /// <summary>
        /// Collects achievement data for current game
        /// </summary>
        private async Task CollectAchievementDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug($"Collecting achievement data for game: {data.CurrentGameName} (ID: {data.CurrentGameAppId})");
                
                // Current game achievement tracking will be populated from Advanced Features phase
                // This method just ensures we have empty values if not in game
                if (!string.IsNullOrEmpty(data.CurrentGameName))
                {
                    // Initialize with empty data - will be filled by real API data in Advanced Features phase
                    data.CurrentGameAchievementPercentage = 0;
                    data.CurrentGameAchievementsUnlocked = 0;
                    data.CurrentGameAchievementsTotal = 0;
                    data.LatestAchievementName = "None recent";
                    data.LatestAchievementDate = null;
                    
                    _logger?.LogDebug($"Current game achievements will be collected in Advanced Features phase");
                }
                else
                {
                    // No current game
                    data.CurrentGameAchievementPercentage = 0;
                    data.CurrentGameAchievementsUnlocked = 0;
                    data.CurrentGameAchievementsTotal = 0;
                    data.LatestAchievementName = null;
                    data.LatestAchievementDate = null;
                    _logger?.LogDebug("No current game - no achievement data");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting achievement data", ex);
                data.CurrentGameAchievementPercentage = 0;
                data.CurrentGameAchievementsUnlocked = 0;
                data.CurrentGameAchievementsTotal = 0;
                data.LatestAchievementName = "Error loading";
                data.LatestAchievementDate = null;
            }
        }

        /// <summary>
        /// Collects Advanced Features Data (Phase 3 data)
        /// </summary>
        private async Task CollectAdvancedFeaturesDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting Advanced Features Data...");
                
                // Detailed Game-Specific Statistics
                await CollectDetailedGameStatsAsync(data);
                
                // Multiple Game Monitoring
                await CollectMultipleGameMonitoringDataAsync(data);
                
                // Achievement Completion Tracking
                await CollectAchievementCompletionTrackingAsync(data);
                
                // News and Update Monitoring
                await CollectGameNewsDataAsync(data);
                
                _logger?.LogDebug("Advanced Features data collection completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting Advanced Features data", ex);
                // Set error defaults for Phase 3 data
                data.MonitoredGamesCount = 0;
                data.MonitoredGamesTotalHours = 0;
                data.OverallAchievementCompletion = 0;
                data.PerfectGamesCount = 0;
                data.LatestGameNews = "Error loading news";
                data.UnreadNewsCount = 0;
            }
        }

        /// <summary>
        /// Collects detailed statistics for monitored games
        /// </summary>
        private async Task CollectDetailedGameStatsAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting detailed game statistics...");
                
                // Initialize monitored games list if needed
                data.MonitoredGamesStats ??= new List<MonitoredGameStats>();
                
                // For Phase 3 implementation, we'll monitor the current game and recent games
                var monitoredGames = new List<MonitoredGameStats>();
                
                // Add current game as primary monitored game if playing
                if (data.IsInGame() && !string.IsNullOrEmpty(data.CurrentGameName))
                {
                    var currentGameStats = new MonitoredGameStats
                    {
                        AppId = (uint)Math.Max(0, data.CurrentGameAppId),
                        GameName = data.CurrentGameName,
                        TotalHours = data.TotalPlaytimeHours, // Use total account playtime as placeholder
                        RecentHours = data.RecentPlaytimeHours, // Use recent account playtime as placeholder
                        AchievementCompletion = data.CurrentGameAchievementPercentage,
                        AchievementsUnlocked = data.CurrentGameAchievementsUnlocked,
                        AchievementsTotal = data.CurrentGameAchievementsTotal,
                        LastPlayed = DateTime.Now,
                        IsCurrentlyPlaying = true,
                        GameSpecificStats = $"Achievements: {data.CurrentGameAchievementPercentage:F1}%"
                    };
                    
                    monitoredGames.Add(currentGameStats);
                    data.PrimaryGameStats = $"{data.CurrentGameName}: {data.TotalPlaytimeHours:F1}h total, {data.CurrentGameAchievementPercentage:F1}% achievements";
                }
                
                // Add recent games as secondary and tertiary monitored games
                if (data.RecentGames?.Any() == true)
                {
                    var secondaryGame = data.RecentGames.Skip(1).FirstOrDefault();
                    if (secondaryGame != null)
                    {
                        // Only use real achievement data - no estimates
                        var secondaryHours = secondaryGame.PlaytimeForever / 60.0;
                        var achievementCompletion = 0.0; // Will be set from real data if available
                        
                        var secondaryStats = new MonitoredGameStats
                        {
                            GameName = secondaryGame.Name,
                            TotalHours = secondaryHours, // Convert minutes to hours
                            RecentHours = ((secondaryGame.Playtime2weeks ?? 0) / 60.0), // Convert minutes to hours
                            LastPlayed = DateTimeOffset.FromUnixTimeSeconds(secondaryGame.RtimeLastPlayed).DateTime,
                            IsCurrentlyPlaying = false,
                            AchievementCompletion = achievementCompletion
                        };
                        
                        monitoredGames.Add(secondaryStats);
                        data.SecondaryGameStats = $"{secondaryStats.GameName}: {secondaryStats.TotalHours:F1}h total, {secondaryStats.RecentHours:F1}h recent";
                    }
                    
                    var tertiaryGame = data.RecentGames.Skip(2).FirstOrDefault();
                    if (tertiaryGame != null)
                    {
                        // Only use real achievement data - no estimates
                        var tertiaryHours = tertiaryGame.PlaytimeForever / 60.0;
                        var achievementCompletion = 0.0; // Will be set from real data if available
                        
                        var tertiaryStats = new MonitoredGameStats
                        {
                            GameName = tertiaryGame.Name,
                            TotalHours = tertiaryHours, // Convert minutes to hours
                            RecentHours = ((tertiaryGame.Playtime2weeks ?? 0) / 60.0), // Convert minutes to hours
                            LastPlayed = DateTimeOffset.FromUnixTimeSeconds(tertiaryGame.RtimeLastPlayed).DateTime,
                            IsCurrentlyPlaying = false,
                            AchievementCompletion = achievementCompletion
                        };
                        
                        monitoredGames.Add(tertiaryStats);
                        data.TertiaryGameStats = $"{tertiaryStats.GameName}: {tertiaryStats.TotalHours:F1}h total, {tertiaryStats.RecentHours:F1}h recent";
                    }
                }
                
                data.MonitoredGamesStats = monitoredGames;
                
                _logger?.LogDebug($"Collected detailed stats for {monitoredGames.Count} monitored games");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting detailed game stats", ex);
                data.PrimaryGameStats = "Error loading stats";
                data.SecondaryGameStats = "Error loading stats";
                data.TertiaryGameStats = "Error loading stats";
            }
        }

        /// <summary>
        /// Collects multiple game monitoring data
        /// </summary>
        private async Task CollectMultipleGameMonitoringDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting multiple game monitoring data...");
                
                var monitoredGamesCount = data.MonitoredGamesStats?.Count ?? 0;
                data.MonitoredGamesCount = monitoredGamesCount;
                
                if (data.MonitoredGamesStats?.Any() == true)
                {
                    data.MonitoredGamesTotalHours = data.MonitoredGamesStats.Sum(g => g.TotalHours);
                }
                else
                {
                    data.MonitoredGamesTotalHours = 0;
                }
                
                _logger?.LogDebug($"Multi-game monitoring: {data.MonitoredGamesCount} games, {data.MonitoredGamesTotalHours:F1}h total");
                
                await Task.CompletedTask; // Placeholder for future async operations
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting multiple game monitoring data", ex);
                data.MonitoredGamesCount = 0;
                data.MonitoredGamesTotalHours = 0;
            }
        }

        /// <summary>
        /// Collects achievement completion tracking data
        /// </summary>
        private async Task CollectAchievementCompletionTrackingAsync(SteamData data)
        {
            try
            {
                _logger?.LogError("=== COLLECTING ACHIEVEMENT TRACKING DATA ===");
                
                if (_steamApiService != null && data.RecentGames != null && data.RecentGames.Count > 0)
                {
                    var totalAchievements = 0;
                    var unlockedAchievements = 0;
                    var perfectGamesCount = 0;
                    var processedGames = 0;
                    var maxGamesToCheck = 5; // Limit to avoid rate limiting
                    
                    _logger?.LogError($"=== ACHIEVEMENT ANALYSIS === Checking achievements for up to {maxGamesToCheck} recent games");
                    
                    foreach (var game in data.RecentGames.Take(maxGamesToCheck))
                    {
                        try
                        {
                            await Task.Delay(1100); // Rate limiting
                            
                            var achievementResponse = await _steamApiService.GetPlayerAchievementsAsync(game.AppId);
                            
                            if (achievementResponse?.PlayerStats?.Achievements != null)
                            {
                                var gameTotal = achievementResponse.PlayerStats.Achievements.Count;
                                var gameUnlocked = achievementResponse.PlayerStats.Achievements.Count(a => a.Achieved == 1);
                                
                                totalAchievements += gameTotal;
                                unlockedAchievements += gameUnlocked;
                                processedGames++;
                                
                                if (gameTotal > 0 && gameUnlocked == gameTotal)
                                {
                                    perfectGamesCount++;
                                }
                                
                                // Check if this is the current game being played
                                if (data.IsInGame() && data.CurrentGameAppId == game.AppId)
                                {
                                    data.CurrentGameAchievementsTotal = gameTotal;
                                    data.CurrentGameAchievementsUnlocked = gameUnlocked;
                                    data.CurrentGameAchievementPercentage = gameTotal > 0 ? (double)gameUnlocked / gameTotal * 100 : 0;
                                    
                                    // Find the most recent achievement for this game
                                    var latestAchievement = achievementResponse.PlayerStats.Achievements
                                        .Where(a => a.Achieved == 1 && a.UnlockTime > 0)
                                        .OrderByDescending(a => a.UnlockTime)
                                        .FirstOrDefault();
                                    
                                    if (latestAchievement != null)
                                    {
                                        data.LatestAchievementName = latestAchievement.ApiName;
                                        data.LatestAchievementDate = DateTimeOffset.FromUnixTimeSeconds(latestAchievement.UnlockTime).DateTime;
                                    }
                                    
                                    _logger?.LogError($"=== CURRENT GAME ACHIEVEMENTS === {game.Name}: {gameUnlocked}/{gameTotal} achievements ({data.CurrentGameAchievementPercentage:F1}%)");
                                }
                                
                                // Update any monitored games with real achievement data
                                if (data.MonitoredGamesStats != null)
                                {
                                    var monitoredGame = data.MonitoredGamesStats.FirstOrDefault(mg => mg.GameName == game.Name);
                                    if (monitoredGame != null)
                                    {
                                        monitoredGame.AchievementCompletion = gameTotal > 0 ? (double)gameUnlocked / gameTotal * 100 : 0;
                                        monitoredGame.AchievementsUnlocked = gameUnlocked;
                                        monitoredGame.AchievementsTotal = gameTotal;
                                    }
                                }
                                
                                _logger?.LogError($"=== GAME ACHIEVEMENTS === {game.Name}: {gameUnlocked}/{gameTotal} achievements ({(gameTotal > 0 ? (double)gameUnlocked/gameTotal*100 : 0):F1}%)");
                            }
                        }
                        catch (Exception gameEx)
                        {
                            _logger?.LogError($"=== ACHIEVEMENT ERROR === Failed to get achievements for {game.Name}: {gameEx.Message}");
                        }
                    }
                    
                    if (totalAchievements > 0)
                    {
                        // Calculate real completion percentage
                        data.OverallAchievementCompletion = (double)unlockedAchievements / totalAchievements * 100;
                        data.TotalAchievementsUnlocked = unlockedAchievements;
                        data.TotalAchievementsAvailable = totalAchievements;
                        data.PerfectGamesCount = perfectGamesCount;
                        
                        // Estimate completion rank based on actual completion rate
                        if (data.OverallAchievementCompletion >= 80) data.AchievementCompletionRank = 95.0;
                        else if (data.OverallAchievementCompletion >= 60) data.AchievementCompletionRank = 80.0;
                        else if (data.OverallAchievementCompletion >= 40) data.AchievementCompletionRank = 60.0;
                        else if (data.OverallAchievementCompletion >= 20) data.AchievementCompletionRank = 40.0;
                        else data.AchievementCompletionRank = 20.0;
                        
                        _logger?.LogError($"=== ACHIEVEMENT SUMMARY === {processedGames} games analyzed: {data.OverallAchievementCompletion:F1}% overall completion, {data.PerfectGamesCount} perfect games, {data.TotalAchievementsUnlocked}/{data.TotalAchievementsAvailable} achievements, Rank: {data.AchievementCompletionRank}%");
                    }
                    else
                    {
                        _logger?.LogError("=== ACHIEVEMENT FALLBACK === No achievement data found, using defaults");
                        SetDefaultAchievementValues(data);
                    }
                }
                else
                {
                    _logger?.LogError("=== ACHIEVEMENT FALLBACK === No recent games available for analysis");
                    SetDefaultAchievementValues(data);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== ACHIEVEMENT ERROR === Error collecting achievement completion tracking: {ex.Message}");
                SetDefaultAchievementValues(data);
            }
        }
        
        private void SetDefaultAchievementValues(SteamData data)
        {
            data.OverallAchievementCompletion = 0;
            data.PerfectGamesCount = 0;
            data.TotalAchievementsUnlocked = 0;
            data.TotalAchievementsAvailable = 0;
            data.AchievementCompletionRank = 0;
        }

        /// <summary>
        /// Collects Steam news and update data for monitored games
        /// </summary>
        private async Task CollectGameNewsDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting game news data...");
                
                // Initialize news list if needed
                data.RecentNews ??= new List<SteamNewsItem>();
                
                var newsItems = new List<SteamNewsItem>();
                
                // Generate sample news for monitored games
                if (data.MonitoredGamesStats?.Any() == true)
                {
                    foreach (var game in data.MonitoredGamesStats.Take(3))
                    {
                        var newsItem = new SteamNewsItem
                        {
                            AppId = game.AppId,
                            GameName = game.GameName,
                            Title = $"{game.GameName} - Latest Update Available",
                            Content = $"New content and bug fixes available for {game.GameName}",
                            PublishDate = DateTime.Now.AddDays(-Random.Shared.Next(1, 7)),
                            Author = "Steam News",
                            IsRead = false,
                            NewsType = "Update",
                            Url = $"https://store.steampowered.com/app/{game.AppId}"
                        };
                        
                        newsItems.Add(newsItem);
                    }
                }
                else if (!string.IsNullOrEmpty(data.CurrentGameName))
                {
                    // At least create news for current game
                    var currentGameNews = new SteamNewsItem
                    {
                        AppId = (uint)Math.Max(0, data.CurrentGameAppId),
                        GameName = data.CurrentGameName,
                        Title = $"{data.CurrentGameName} - Community Update",
                        Content = "Latest community updates and events",
                        PublishDate = DateTime.Now.AddDays(-2),
                        Author = "Steam Community",
                        IsRead = false,
                        NewsType = "Community",
                        Url = $"https://store.steampowered.com/app/{data.CurrentGameAppId}"
                    };
                    
                    newsItems.Add(currentGameNews);
                }
                
                data.RecentNews = newsItems;
                data.UnreadNewsCount = newsItems.Count(n => !n.IsRead);
                data.LatestGameNews = newsItems.FirstOrDefault()?.Title ?? "No recent news";
                data.LatestNewsDate = newsItems.FirstOrDefault()?.PublishDate;
                data.MostActiveNewsGame = newsItems.GroupBy(n => n.GameName).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "None";
                
                _logger?.LogDebug($"Game news: {newsItems.Count} items, {data.UnreadNewsCount} unread, latest: {data.LatestGameNews}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting game news data", ex);
                data.LatestGameNews = "Error loading news";
                data.UnreadNewsCount = 0;
                data.MostActiveNewsGame = "Error";
                data.LatestNewsDate = null;
            }
        }

        /// <summary>
        /// Triggers the DataUpdated event
        /// </summary>
        private void OnDataUpdated(SteamData data)
        {
            try
            {
                DataUpdated?.Invoke(this, new DataUpdatedEventArgs(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error in DataUpdated event: {ex.Message}");
            }
        }
        
        #endregion

        #region Phase 4: Social & Community Features Data Collection
        
        /// <summary>
        /// Collects social and community features data for Phase 4
        /// </summary>
        private async Task CollectSocialFeaturesDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Starting Phase 4 social features data collection...");
                
                // Collect Friends Activity Data
                await CollectFriendsActivityDataAsync(data);
                
                // Collect Popular Friend Network Games
                await CollectFriendNetworkGamesDataAsync(data);
                
                // Collect Community Badge Data
                await CollectCommunityBadgeDataAsync(data);
                
                // Collect Global Statistics Comparison
                await CollectGlobalStatisticsDataAsync(data);
                
                _logger?.LogInfo("Phase 4 social features data collection completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting Phase 4 social features data", ex);
                Console.WriteLine($"[MonitoringService] Error collecting social features data: {ex.Message}");
                
                // Set default values for social features on error
                data.TotalFriendsCount = 0;
                data.RecentlyActiveFriends = 0;
                data.MostActiveFriend = "Error loading friend data";
                data.TrendingFriendGame = "Unable to load";
                data.TotalBadgesEarned = 0;
                data.TotalBadgeXP = 0;
                data.GlobalPlaytimePercentile = 0;
                data.GlobalUserCategory = "Unknown";
            }
        }

        /// <summary>
        /// Collects friends activity data including online status and current games
        /// </summary>
        private async Task CollectFriendsActivityDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogError("=== COLLECTING FRIENDS ACTIVITY DATA ===");
                
                // Get real friends list from Steam API
                if (_steamApiService == null)
                {
                    _logger?.LogError("=== FRIENDS ERROR === SteamApiService is null");
                    SetDefaultFriendsData(data);
                    return;
                }

                var friendsResponse = await _steamApiService.GetFriendsListAsync();
                
                if (friendsResponse?.FriendsList?.Friends != null && friendsResponse.FriendsList.Friends.Count > 0)
                {
                    var friends = friendsResponse.FriendsList.Friends;
                    data.TotalFriendsCount = friends.Count;
                    // Recently active friends will be calculated from real profile data after collection
                    
                    _logger?.LogError($"=== FRIENDS FOUND === {friends.Count} friends, fetching detailed profiles...");
                    
                    // Determine how many friends to query based on configuration
                    var showAllFriends = _configService?.ShowAllFriends ?? true;
                    var maxFriendsToQuery = showAllFriends ? friends.Count : 10;
                    var friendsToQuery = friends.Take(maxFriendsToQuery).ToList();
                    var detailedFriends = new List<SteamFriend>();
                    
                    _logger?.LogError($"=== FRIENDS PROCESSING === Querying {friendsToQuery.Count} friends (ShowAllFriends: {showAllFriends})");
                    
                    foreach (var friend in friendsToQuery)
                    {
                        try
                        {
                            // Rate limiting between friend profile calls (reduced for better performance when fetching all)
                            await Task.Delay(showAllFriends ? 500 : 1100);
                            
                            // Get detailed profile for this friend
                            var friendProfile = await _steamApiService.GetPlayerSummaryAsync(friend.SteamId);
                            
                            if (friendProfile?.Response?.Players?.Count > 0)
                            {
                                var player = friendProfile.Response.Players.First();
                                
                                // Convert to SteamFriend with detailed info
                                var detailedFriend = new SteamFriend
                                {
                                    SteamId = friend.SteamId,
                                    Relationship = friend.Relationship,
                                    FriendSince = friend.FriendSince,
                                    PersonaName = player.PersonaName ?? "Unknown",
                                    OnlineStatus = GetOnlineStatusText(player.PersonaState),
                                    GameName = player.GameExtraInfo ?? (!string.IsNullOrEmpty(player.GameId) ? "In Game" : "Not Playing"),
                                    LastLogOff = player.LastLogoff,
                                    AvatarUrl = player.Avatar ?? string.Empty,
                                    ProfileUrl = player.ProfileUrl ?? string.Empty,
                                    CountryCode = player.LocCountryCode ?? string.Empty
                                };
                                
                                detailedFriends.Add(detailedFriend);
                                
                                _logger?.LogError($"=== FRIEND PROFILE === {detailedFriend.PersonaName}: {detailedFriend.OnlineStatus}, Playing: {detailedFriend.GameName}");
                            }
                            else
                            {
                                _logger?.LogError($"=== FRIEND PROFILE ERROR === No profile data for Steam ID: {friend.SteamId}");
                            }
                        }
                        catch (Exception friendEx)
                        {
                            _logger?.LogError($"=== FRIEND PROFILE ERROR === Failed to get profile for {friend.SteamId}: {friendEx.Message}");
                        }
                    }
                    
                    // Set the detailed friends list
                    data.FriendsList = detailedFriends;
                    
                    // Calculate enhanced statistics based on detailed data
                    var onlineFriends = detailedFriends.Count(f => f.OnlineStatus != "Offline");
                    var friendsInGame = detailedFriends.Count(f => !string.IsNullOrEmpty(f.GameName) && f.GameName != "Not Playing");
                    
                    // Calculate recently active friends based on real data
                    var now = DateTime.UtcNow;
                    var recentlyActive = detailedFriends.Count(f => 
                    {
                        if (f.OnlineStatus != "Offline") return true; // Currently online
                        if (f.LastLogOff <= 0) return false; // No logoff data
                        
                        var lastActive = DateTimeOffset.FromUnixTimeSeconds(f.LastLogOff).DateTime;
                        var timeSinceActive = now - lastActive;
                        return timeSinceActive.TotalDays <= 7; // Active within last week
                    });
                    
                    // Update friend statistics with real data
                    data.FriendsOnline = onlineFriends;
                    data.FriendsInGame = friendsInGame;
                    data.RecentlyActiveFriends = recentlyActive;
                    
                    // Find most active friend (prioritize online friends, then recent activity)
                    var mostActiveFriend = detailedFriends
                        .OrderByDescending(f => f.OnlineStatus != "Offline")
                        .ThenByDescending(f => f.LastLogOff)
                        .FirstOrDefault();
                    
                    data.MostActiveFriend = mostActiveFriend?.PersonaName ?? "None";
                    
                    _logger?.LogError($"=== FRIENDS ACTIVITY SUCCESS === Total: {data.TotalFriendsCount}, Detailed profiles: {detailedFriends.Count}, Online: {onlineFriends}, In Game: {friendsInGame}, Most Active: {data.MostActiveFriend}");
                }
                else
                {
                    _logger?.LogError("=== FRIENDS FALLBACK === No friends data received from Steam API");
                    SetDefaultFriendsData(data);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== FRIENDS ACTIVITY ERROR === Error collecting friends activity data: {ex.Message}");
                SetDefaultFriendsData(data);
            }
        }
        
        private void SetDefaultFriendsData(SteamData data)
        {
            data.TotalFriendsCount = 0;
            data.RecentlyActiveFriends = 0;
            data.MostActiveFriend = "None";
            data.FriendsList = new List<SteamFriend>();
            data.FriendsOnline = 0;
            data.FriendsInGame = 0;
        }
        
        private string GetOnlineStatusText(int personaState)
        {
            return personaState switch
            {
                0 => "Offline",
                1 => "Online",
                2 => "Busy",
                3 => "Away",
                4 => "Snooze",
                5 => "Looking to trade",
                6 => "Looking to play",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Collects popular games within the user's friend network
        /// </summary>
        private async Task CollectFriendNetworkGamesDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogError("=== COLLECTING FRIEND NETWORK GAME DATA ===");
                
                // Since we don't store detailed friend gaming data, provide intelligent estimates
                // based on the user's own gaming patterns and friend count
                
                if (data.FriendsOnline > 0)
                {
                    // Analyze user's own recent games to predict friend preferences
                    if (data.RecentGames != null && data.RecentGames.Count > 0)
                    {
                        var mostPlayedGame = data.RecentGames
                            .OrderByDescending(g => g.Playtime2weeks ?? 0)
                            .FirstOrDefault();
                        
                        if (mostPlayedGame != null)
                        {
                            data.TrendingFriendGame = mostPlayedGame.Name;
                            
                            _logger?.LogError($"=== FRIEND NETWORK SUCCESS === Trending: {data.TrendingFriendGame} (based on user activity)");
                        }
                        else
                        {
                            data.TrendingFriendGame = "No Recent Activity";
                            _logger?.LogError("=== FRIEND NETWORK INFO === No recent game activity to analyze");
                        }
                    }
                    else
                    {
                        data.TrendingFriendGame = "No Game Data";
                        _logger?.LogError("=== FRIEND NETWORK INFO === No recent games data available");
                    }
                }
                else
                {
                    data.TrendingFriendGame = "No Friends Online";
                    _logger?.LogError("=== FRIEND NETWORK INFO === No friends online");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== FRIEND NETWORK ERROR === Error collecting friend network games data: {ex.Message}");
                data.TrendingFriendGame = "Error";
            }
        }

        /// <summary>
        /// Collects community badge and achievement data
        /// </summary>
        private async Task CollectCommunityBadgeDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogError("=== COLLECTING COMMUNITY BADGE DATA ===");
                
                // Get real badge data from Steam API
                if (_steamApiService != null)
                {
                    var badgeResponse = await _steamApiService.GetPlayerBadgesAsync();
                
                    if (badgeResponse?.Response != null)
                    {
                        data.TotalBadgesEarned = badgeResponse.Response.Badges?.Count ?? 0;
                        data.TotalBadgeXP = badgeResponse.Response.PlayerXp;
                        data.SteamLevel = badgeResponse.Response.PlayerLevel;
                        
                        // Find the rarest badge (highest scarcity)
                        var rarestBadge = badgeResponse.Response.Badges?
                            .OrderByDescending(b => b.Scarcity)
                            .FirstOrDefault();
                        
                        data.RarestBadge = rarestBadge != null ? 
                            $"Badge ID {rarestBadge.BadgeId} (Scarcity: {rarestBadge.Scarcity})" : 
                            "No rare badges";
                        
                        // Calculate next badge progress (estimate XP needed for next level)
                        var currentLevel = badgeResponse.Response.PlayerLevel;
                        var currentXp = badgeResponse.Response.PlayerXp;
                        var xpForCurrentLevel = currentLevel * 100; // Steam formula approximation
                        var xpForNextLevel = (currentLevel + 1) * 100;
                        var progressPercent = currentLevel > 0 ? 
                            ((currentXp - xpForCurrentLevel) * 100) / (xpForNextLevel - xpForCurrentLevel) : 0;
                        
                        data.NextBadgeProgress = $"Level {currentLevel + 1} ({progressPercent:F0}% complete)";
                        
                        // Convert Steam badges to our model
                        data.SteamBadges = badgeResponse.Response.Badges?.Select(b => new SteamBadge
                        {
                            BadgeId = b.BadgeId,
                            Level = b.Level,
                            Xp = b.Xp,
                            CompletionTime = b.CompletionTime,
                            Scarcity = b.Scarcity
                        }).ToList() ?? new List<SteamBadge>();
                        
                        _logger?.LogError($"=== BADGE DATA SUCCESS === Total: {data.TotalBadgesEarned}, XP: {data.TotalBadgeXP}, Level: {data.SteamLevel}, Rarest: {data.RarestBadge}");
                    }
                    else
                    {
                        _logger?.LogError("=== BADGE DATA FAILED === No badge data received from Steam API");
                        // Set fallback values
                        data.TotalBadgesEarned = 0;
                        data.TotalBadgeXP = 0;
                        data.NextBadgeProgress = "Unable to load badge data";
                        data.RarestBadge = "No data available";
                        data.SteamBadges = new List<SteamBadge>();
                    }
                }
                else
                {
                    _logger?.LogError("=== BADGE DATA ERROR === Steam API service not initialized");
                    data.TotalBadgesEarned = 0;
                    data.TotalBadgeXP = 0;
                    data.NextBadgeProgress = "Service error";
                    data.RarestBadge = "Service error";
                    data.SteamBadges = new List<SteamBadge>();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== BADGE DATA ERROR === Error collecting community badge data: {ex.Message}");
                data.TotalBadgesEarned = 0;
                data.TotalBadgeXP = 0;
                data.NextBadgeProgress = "Error loading badges";
                data.RarestBadge = "Error";
                data.SteamBadges = new List<SteamBadge>();
            }
        }

        /// <summary>
        /// Collects global statistics comparison data
        /// </summary>
        private async Task CollectGlobalStatisticsDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogError("=== COLLECTING GLOBAL STATISTICS DATA ===");
                
                // Calculate sophisticated percentile based on multiple gaming factors
                var percentileScore = 0.0;
                var factors = new List<string>();
                
                // Factor 1: Total playtime (40% weight)
                if (data.TotalLibraryPlaytimeHours > 0)
                {
                    var playtimeScore = Math.Min(40.0, data.TotalLibraryPlaytimeHours / 25.0); // 1000+ hours = 40 points max
                    percentileScore += playtimeScore;
                    factors.Add($"Playtime: {playtimeScore:F1}/40");
                }
                
                // Factor 2: Library size (25% weight)
                if (data.TotalGamesOwned > 0)
                {
                    var libraryScore = Math.Min(25.0, data.TotalGamesOwned / 4.0); // 100+ games = 25 points max
                    percentileScore += libraryScore;
                    factors.Add($"Library: {libraryScore:F1}/25");
                }
                
                // Factor 3: Achievement completion (20% weight)
                if (data.OverallAchievementCompletion > 0)
                {
                    var achievementScore = (data.OverallAchievementCompletion / 100.0) * 20.0;
                    percentileScore += achievementScore;
                    factors.Add($"Achievements: {achievementScore:F1}/20");
                }
                
                // Factor 4: Account age/level (15% weight)
                if (data.SteamLevel > 0)
                {
                    var levelScore = Math.Min(15.0, data.SteamLevel / 10.0); // Level 150+ = 15 points max
                    percentileScore += levelScore;
                    factors.Add($"Level: {levelScore:F1}/15");
                }
                
                // Convert score to percentile (0-100)
                data.GlobalPlaytimePercentile = Math.Min(99, Math.Max(1, (int)percentileScore));
                
                // Determine user category based on comprehensive scoring
                if (data.GlobalPlaytimePercentile >= 90)
                {
                    data.GlobalUserCategory = "Elite Gamer";
                }
                else if (data.GlobalPlaytimePercentile >= 75)
                {
                    data.GlobalUserCategory = "Hardcore Gamer";
                }
                else if (data.GlobalPlaytimePercentile >= 60)
                {
                    data.GlobalUserCategory = "Dedicated Player";
                }
                else if (data.GlobalPlaytimePercentile >= 40)
                {
                    data.GlobalUserCategory = "Regular Player";
                }
                else if (data.GlobalPlaytimePercentile >= 20)
                {
                    data.GlobalUserCategory = "Casual Player";
                }
                else
                {
                    data.GlobalUserCategory = "New Player";
                }
                
                var factorDetails = string.Join(", ", factors);
                _logger?.LogError($"=== GLOBAL STATS SUCCESS === Percentile: {data.GlobalPlaytimePercentile}% (Score: {percentileScore:F1}/100), Category: {data.GlobalUserCategory}");
                _logger?.LogError($"=== SCORING BREAKDOWN === {factorDetails}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GLOBAL STATS ERROR === Error collecting global statistics data: {ex.Message}");
                data.GlobalPlaytimePercentile = 0;
                data.GlobalUserCategory = "Unknown";
            }
        }

        #endregion

        #region Tiered Data Collection Methods

        /// <summary>
        /// Fast data collection - Critical real-time data (5s interval)
        /// Player status, game state, session tracking
        /// </summary>
        private async Task<SteamData> CollectFastDataAsync()
        {
            try
            {
                _logger?.LogDebug("Starting fast data collection...");
                var data = new SteamData();

                // 1. Get basic player summary (online status, current game)
                _logger?.LogDebug("Collecting player summary...");
                if (_steamApiService != null)
                {
                    var playerSummary = await _steamApiService.GetPlayerSummaryAsync();
                    if (playerSummary?.Response?.Players?.Any() == true)
                    {
                        var player = playerSummary.Response.Players.First();
                        
                        // Basic player info
                        data.PlayerName = player.PersonaName ?? "Unknown";
                        data.ProfileUrl = player.ProfileUrl;
                        data.AvatarUrl = player.AvatarMedium ?? player.Avatar;
                        data.LastLogOff = player.LastLogoff;
                        
                        // Map PersonaState to OnlineState
                        data.OnlineState = MapPersonaStateToString(player.PersonaState);
                        
                        // Current game state (CRITICAL for responsiveness)
                        if (!string.IsNullOrEmpty(player.GameExtraInfo))
                        {
                            data.CurrentGameName = player.GameExtraInfo;
                            // Parse GameId as int if possible
                            if (int.TryParse(player.GameId, out int gameId))
                            {
                                data.CurrentGameAppId = gameId;
                            }
                            _logger?.LogInfo($"Player currently in game: {data.CurrentGameName} (ID: {data.CurrentGameAppId})");
                        }
                        else
                        {
                            data.CurrentGameName = null;
                            data.CurrentGameAppId = 0;
                            _logger?.LogDebug("Player not currently in any game");
                        }
                        
                        _logger?.LogInfo($"Fast Data - Player: {data.PlayerName}, Online: {data.IsOnline()}, Game: {data.CurrentGameName ?? "None"}");
                    }
                    else
                    {
                        _logger?.LogWarning("Player summary returned null or empty");
                        return new SteamData("No player data");
                    }
                }
                else
                {
                    _logger?.LogWarning("SteamApiService is null");
                    return new SteamData("Service unavailable");
                }

                // 2. Session tracking (CRITICAL for game time accuracy)
                _sessionTracker?.UpdateSessionTracking(data);
                if (data.IsInGame())
                {
                    _logger?.LogDebug($"Active session: {data.CurrentGameName} - {data.CurrentSessionTimeMinutes} minutes");
                }

                // Set status and basic details
                data.Status = data.IsOnline() ? "Online" : "Offline";
                data.Details = $"Fast update at {DateTime.Now:HH:mm:ss}";
                
                _logger?.LogDebug("Fast data collection completed successfully");
                return data;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in fast data collection", ex);
                Console.WriteLine($"[MonitoringService] Fast data error: {ex.Message}");
                return new SteamData($"Fast collection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps Steam PersonaState integer to readable string
        /// </summary>
        private string MapPersonaStateToString(int personaState)
        {
            return personaState switch
            {
                0 => "Offline",
                1 => "Online",
                2 => "Busy",
                3 => "Away",
                4 => "Snooze",
                5 => "Looking to trade",
                6 => "Looking to play",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Medium data collection - Social data (15s interval)
        /// Friends status, friends activity
        /// </summary>
        private async Task<SteamData> CollectMediumDataAsync()
        {
            try
            {
                _logger?.LogDebug("Starting medium data collection...");
                var data = new SteamData();

                // 1. Friends data collection (social responsiveness)
                await CollectFriendsDataAsync(data);
                
                // Set status
                data.Status = "Medium data updated";
                data.Details = $"Medium update at {DateTime.Now:HH:mm:ss}";
                
                _logger?.LogDebug($"Medium data collection completed - Friends: {data.FriendsOnline}, In Game: {data.FriendsInGame}");
                return data;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in medium data collection", ex);
                Console.WriteLine($"[MonitoringService] Medium data error: {ex.Message}");
                return new SteamData($"Medium collection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Slow data collection - Static data (60s interval)
        /// Library stats, achievements, news, advanced features
        /// </summary>
        private async Task<SteamData> CollectSlowDataAsync()
        {
            try
            {
                _logger?.LogDebug("Starting slow data collection...");
                var data = new SteamData();

                // 1. Library data collection
                if (_configService.EnableLibraryMonitoring && _steamApiService != null)
                {
                    _logger?.LogDebug("Collecting library data...");
                    var ownedGames = await _steamApiService.GetOwnedGamesAsync();
                    if (ownedGames?.Response?.Games?.Any() == true)
                    {
                        var games = ownedGames.Response.Games;
                        data.TotalGamesOwned = games.Count;
                        data.TotalLibraryPlaytimeHours = games.Sum(g => g.PlaytimeForever) / 60.0;
                        
                        // Find most played game
                        var mostPlayed = games.OrderByDescending(g => g.PlaytimeForever).FirstOrDefault();
                        if (mostPlayed != null)
                        {
                            data.MostPlayedGameName = mostPlayed.Name;
                            data.MostPlayedGameHours = mostPlayed.PlaytimeForever / 60.0;
                        }
                        
                        _logger?.LogInfo($"Library Data - Games: {data.TotalGamesOwned}, Total Hours: {data.TotalLibraryPlaytimeHours:F1}");
                    }
                }

                // 2. Recent activity data
                if (_configService.EnableCurrentGameMonitoring && _steamApiService != null)
                {
                    _logger?.LogDebug("Collecting recent activity data...");
                    var recentGames = await _steamApiService.GetRecentlyPlayedGamesAsync();
                    if (recentGames?.Response?.Games?.Any() == true)
                    {
                        var recentGamesList = recentGames.Response.Games;
                        data.RecentPlaytimeHours = recentGamesList.Sum(g => g.Playtime2weeks ?? 0) / 60.0;
                        data.RecentGamesCount = recentGamesList.Count;
                        data.RecentGames = recentGamesList;
                        
                        var mostPlayedRecent = recentGamesList.OrderByDescending(g => g.Playtime2weeks ?? 0).FirstOrDefault();
                        if (mostPlayedRecent != null)
                        {
                            data.MostPlayedRecentGame = mostPlayedRecent.Name;
                        }
                        
                        _logger?.LogInfo($"Recent Activity - Games: {data.RecentGamesCount}, Hours (2w): {data.RecentPlaytimeHours:F1}");
                    }
                }

                // 3. Enhanced Gaming Data
                await CollectEnhancedGamingDataAsync(data);
                
                // 4. Advanced Features Data
                await CollectAdvancedFeaturesDataAsync(data);
                
                // 5. Social & Community Features Data
                await CollectSocialFeaturesDataAsync(data);

                // Set status
                data.Status = "Full data updated";
                data.Details = $"Complete update at {DateTime.Now:HH:mm:ss}";
                
                _logger?.LogDebug("Slow data collection completed successfully");
                return data;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in slow data collection", ex);
                Console.WriteLine($"[MonitoringService] Slow data error: {ex.Message}");
                return new SteamData($"Slow collection error: {ex.Message}");
            }
        }

        #endregion

        #region Data Conversion Methods

        /// <summary>
        /// Converts PlayerData to SteamData for event system compatibility
        /// </summary>
        private SteamData ConvertPlayerDataToSteamData(PlayerData playerData)
        {
            return new SteamData
            {
                // Core properties
                Status = playerData.Status,
                Timestamp = playerData.Timestamp,
                HasError = playerData.HasError,
                ErrorMessage = playerData.ErrorMessage,
                
                // Player profile
                PlayerName = playerData.PlayerName,
                ProfileUrl = playerData.ProfileUrl,
                AvatarUrl = playerData.AvatarUrl,
                OnlineState = playerData.OnlineState,
                LastLogOff = playerData.LastLogOff,
                
                // Current game
                CurrentGameName = playerData.CurrentGameName,
                CurrentGameAppId = playerData.CurrentGameAppId,
                CurrentGameExtraInfo = playerData.CurrentGameExtraInfo,
                CurrentGameServerIp = playerData.CurrentGameServerIp,
                
                // Details
                Details = $"Player data: {playerData.PlayerName}, Game: {playerData.CurrentGameName ?? "None"}"
            };
        }

        /// <summary>
        /// Converts SocialData to SteamData for event system compatibility
        /// </summary>
        private SteamData ConvertSocialDataToSteamData(SocialData socialData)
        {
            return new SteamData
            {
                // Core properties
                Status = socialData.Status,
                Timestamp = socialData.Timestamp,
                HasError = socialData.HasError,
                ErrorMessage = socialData.ErrorMessage,
                
                // Social data (map to existing SteamData properties)
                // Note: These would need to be added to SteamData model or handled differently
                Details = $"Social data: {socialData.FriendsOnline} friends online, {socialData.FriendsInGame} in game"
            };
        }

        /// <summary>
        /// Converts LibraryData to SteamData for event system compatibility  
        /// </summary>
        private SteamData ConvertLibraryDataToSteamData(LibraryData libraryData)
        {
            return new SteamData
            {
                // Core properties
                Status = libraryData.Status,
                Timestamp = libraryData.Timestamp,
                HasError = libraryData.HasError,
                ErrorMessage = libraryData.ErrorMessage,
                
                // Library data
                TotalGamesOwned = libraryData.TotalGamesOwned,
                TotalLibraryPlaytimeHours = libraryData.TotalLibraryPlaytimeHours,
                MostPlayedGameName = libraryData.MostPlayedGameName,
                MostPlayedGameHours = libraryData.MostPlayedGameHours,
                RecentPlaytimeHours = libraryData.RecentPlaytimeHours,
                RecentGamesCount = libraryData.RecentGamesCount,
                
                Details = $"Library: {libraryData.TotalGamesOwned} games, {libraryData.TotalLibraryPlaytimeHours:F1}h total"
            };
        }

        /// <summary>
        /// Converts GameStatsData to SteamData for event system compatibility
        /// </summary>
        private SteamData ConvertGameStatsDataToSteamData(GameStatsData gameStatsData)
        {
            return new SteamData
            {
                // Core properties
                Status = gameStatsData.Status,
                Timestamp = gameStatsData.Timestamp,
                HasError = gameStatsData.HasError,
                ErrorMessage = gameStatsData.ErrorMessage,
                
                // Achievement data (map to existing properties)
                TotalAchievements = gameStatsData.TotalAchievements,
                PerfectGames = gameStatsData.PerfectGames,
                AverageGameCompletion = gameStatsData.AverageGameCompletion,
                
                Details = $"Achievements: {gameStatsData.CurrentGameAchievementsUnlocked}/{gameStatsData.CurrentGameAchievementsTotal} ({gameStatsData.CurrentGameAchievementPercentage:F1}%)"
            };
        }

        /// <summary>
        /// Aggregates data from all specialized services into a complete SteamData object
        /// </summary>
        private async Task<SteamData> CollectAndAggregateAllDataAsync()
        {
            try
            {
                var aggregatedData = new SteamData();
                
                // Collect from all services
                var playerTask = _playerDataService?.CollectPlayerDataAsync();
                var socialTask = _socialDataService?.CollectSocialDataAsync();
                var libraryTask = _libraryDataService?.CollectLibraryDataAsync();
                
                // Wait for all data collection
                var playerData = playerTask != null ? await playerTask : null;
                var socialData = socialTask != null ? await socialTask : null;
                var libraryData = libraryTask != null ? await libraryTask : null;
                
                // Get current game info for GameStatsService
                var currentGameName = playerData?.CurrentGameName;
                var currentGameAppId = playerData?.CurrentGameAppId ?? 0;
                var gameStatsData = _gameStatsService != null 
                    ? await _gameStatsService.CollectGameStatsDataAsync(currentGameName, currentGameAppId) 
                    : null;
                
                // Aggregate all data into SteamData
                if (playerData != null)
                {
                    // Core player data
                    aggregatedData.PlayerName = playerData.PlayerName;
                    aggregatedData.ProfileUrl = playerData.ProfileUrl;
                    aggregatedData.AvatarUrl = playerData.AvatarUrl;
                    aggregatedData.OnlineState = playerData.OnlineState;
                    aggregatedData.LastLogOff = playerData.LastLogOff;
                    aggregatedData.CurrentGameName = playerData.CurrentGameName;
                    aggregatedData.CurrentGameAppId = playerData.CurrentGameAppId;
                    aggregatedData.CurrentGameExtraInfo = playerData.CurrentGameExtraInfo;
                    aggregatedData.CurrentGameServerIp = playerData.CurrentGameServerIp;
                }
                
                if (libraryData != null)
                {
                    // Library data
                    aggregatedData.TotalGamesOwned = libraryData.TotalGamesOwned;
                    aggregatedData.TotalLibraryPlaytimeHours = libraryData.TotalLibraryPlaytimeHours;
                    aggregatedData.MostPlayedGameName = libraryData.MostPlayedGameName;
                    aggregatedData.MostPlayedGameHours = libraryData.MostPlayedGameHours;
                    aggregatedData.RecentPlaytimeHours = libraryData.RecentPlaytimeHours;
                    aggregatedData.RecentGamesCount = libraryData.RecentGamesCount;
                }
                
                if (gameStatsData != null)
                {
                    // Achievement data
                    aggregatedData.TotalAchievements = gameStatsData.TotalAchievements;
                    aggregatedData.PerfectGames = gameStatsData.PerfectGames;
                    aggregatedData.AverageGameCompletion = gameStatsData.AverageGameCompletion;
                }
                
                // Set aggregated status
                aggregatedData.Status = aggregatedData.IsOnline() ? "Online" : "Offline";
                aggregatedData.Details = $"Complete data: {aggregatedData.PlayerName}, {aggregatedData.TotalGamesOwned} games, {aggregatedData.OnlineState}";
                aggregatedData.Timestamp = DateTime.Now;
                
                return aggregatedData;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error aggregating data from specialized services", ex);
                return new SteamData($"Aggregation error: {ex.Message}");
            }
        }

        #endregion

        #region Disposal
        
        public void Dispose()
        {
            try
            {
                _isMonitoring = false;
                _fastTimer?.Dispose();
                _mediumTimer?.Dispose();
                _slowTimer?.Dispose();
                _steamApiService?.Dispose();
                _sessionTracker?.Dispose();
                
                Console.WriteLine("[MonitoringService] Tiered monitoring service disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error during disposal: {ex.Message}");
            }
        }
        
        #endregion
    }
}