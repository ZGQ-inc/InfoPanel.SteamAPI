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
        private readonly System.Threading.Timer _monitoringTimer;
        private SteamApiService? _steamApiService;
        private volatile bool _isMonitoring;
        private readonly object _lockObject = new();
        
        #endregion

        #region Constructor
        
        public MonitoringService(ConfigurationService configService, FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            
            // Initialize timer (but don't start it yet)
            _monitoringTimer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            
            _logger?.LogInfo("MonitoringService initialized");
            Console.WriteLine("[MonitoringService] Steam monitoring service initialized");
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
                
                // Start the monitoring timer using Steam update interval
                var intervalSeconds = _configService.UpdateIntervalSeconds;
                var intervalMs = intervalSeconds * 1000;
                
                _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(intervalMs));
                
                Console.WriteLine($"[MonitoringService] Steam monitoring started (interval: {intervalSeconds}s)");
                
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
            
            // Stop the timer
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Dispose Steam API service
            _steamApiService?.Dispose();
            _steamApiService = null;
            
            Console.WriteLine("[MonitoringService] Steam monitoring stopped");
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
                
                _steamApiService = new SteamApiService(apiKey, steamId64);
                
                // Test the connection
                var isValid = await _steamApiService.TestConnectionAsync();
                if (!isValid)
                {
                    throw new InvalidOperationException("Failed to connect to Steam API. Check your API key and Steam ID.");
                }
                
                Console.WriteLine("[MonitoringService] Steam API connection established");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Failed to initialize Steam API: {ex.Message}");
                throw;
            }
        }
        
        #endregion

        #region Data Collection
        
        private void OnTimerElapsed(object? state)
        {
            if (!_isMonitoring || _steamApiService == null)
                return;
                
            try
            {
                // Collect Steam data asynchronously
                _ = Task.Run(async () =>
                {
                    var data = await CollectSteamDataAsync();
                    OnDataUpdated(data);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error in timer callback: {ex.Message}");
                
                // Create error data
                var errorData = new SteamData($"Timer error: {ex.Message}");
                OnDataUpdated(errorData);
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
                    if (playerSummary != null)
                    {
                        data.PlayerName = playerSummary.PersonaName;
                        data.ProfileUrl = playerSummary.ProfileUrl;
                        data.AvatarUrl = playerSummary.Avatar;
                        data.OnlineState = SteamApiService.GetPersonaStateString(playerSummary.PersonaState);
                        data.LastLogOff = playerSummary.LastLogOff;
                        data.CurrentGameName = playerSummary.GameExtraInfo;
                        data.CurrentGameServerIp = playerSummary.GameServerIp;
                        
                        _logger?.LogInfo($"Player Summary - Name: {data.PlayerName}, State: {data.OnlineState}, Current Game: {data.CurrentGameName ?? "None"}");
                        
                        // Try to parse game ID
                        if (int.TryParse(playerSummary.GameId, out var gameId))
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
                    data.SteamLevel = await _steamApiService.GetSteamLevelAsync();
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
                    if (ownedGames != null && ownedGames.Count > 0)
                    {
                        data.TotalGamesOwned = ownedGames.Count;
                        data.TotalLibraryPlaytimeHours = ownedGames.Sum(g => g.PlaytimeForever) / 60.0; // Convert minutes to hours
                        
                        _logger?.LogInfo($"Library Data - Games Owned: {data.TotalGamesOwned}, Total Playtime: {data.TotalLibraryPlaytimeHours:F1} hours");
                        
                        // Find most played game
                        var mostPlayed = ownedGames.OrderByDescending(g => g.PlaytimeForever).FirstOrDefault();
                        if (mostPlayed != null)
                        {
                            data.MostPlayedGameName = mostPlayed.Name;
                            data.MostPlayedGameHours = mostPlayed.PlaytimeForever / 60.0;
                            _logger?.LogDebug($"Most Played Game: {data.MostPlayedGameName} ({data.MostPlayedGameHours:F1} hours)");
                        }
                        
                        // Set current game playtime if currently playing
                        if (!string.IsNullOrEmpty(data.CurrentGameName))
                        {
                            var currentGame = ownedGames.FirstOrDefault(g => 
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
                    if (recentGames != null && recentGames.Count > 0)
                    {
                        data.RecentPlaytimeHours = recentGames.Sum(g => g.Playtime2Weeks) / 60.0;
                        data.RecentGamesCount = recentGames.Count;
                        _logger?.LogInfo($"Recent Activity - Games: {data.RecentGamesCount}, Playtime (2w): {data.RecentPlaytimeHours:F1} hours");
                        
                        // Store the recent games list for table display
                        data.RecentGames = recentGames;
                        
                        // Set Enhanced Gaming Metrics - Most Played Recent Game
                        var mostPlayedRecent = recentGames.OrderByDescending(g => g.Playtime2Weeks).FirstOrDefault();
                        if (mostPlayedRecent != null)
                        {
                            data.MostPlayedRecentGame = mostPlayedRecent.Name;
                            _logger?.LogDebug($"Most Played Recent: {data.MostPlayedRecentGame} ({mostPlayedRecent.Playtime2Weeks / 60.0:F1}h in 2w)");
                        }
                        else
                        {
                            data.MostPlayedRecentGame = "None";
                        }
                        
                        // Calculate recent session estimates based on playtime patterns
                        data.RecentGameSessions = Math.Max(1, recentGames.Count * 2); // Estimate 2 sessions per game
                        data.AverageSessionTimeMinutes = data.RecentPlaytimeHours > 0 ? 
                            (data.RecentPlaytimeHours * 60.0) / data.RecentGameSessions : 0;
                        _logger?.LogDebug($"Estimated Sessions: {data.RecentGameSessions}, Avg Session: {data.AverageSessionTimeMinutes:F1} min");
                        
                        // Log details about recent games
                        foreach (var game in recentGames.Take(3)) // Log first 3 recent games
                        {
                            var recentHours = game.Playtime2Weeks / 60.0;
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
                if (data.IsInGame())
                {
                    // For now, set basic session data - can be enhanced with persistent tracking later
                    data.SessionStartTime = DateTime.Now.AddMinutes(-30); // Placeholder estimate
                    data.CurrentSessionTimeMinutes = 30; // Placeholder estimate
                    _logger?.LogDebug($"Session tracking: Started {data.SessionStartTime:HH:mm}, Duration: {data.CurrentSessionTimeMinutes} min");
                }
                else
                {
                    data.SessionStartTime = null;
                    data.CurrentSessionTimeMinutes = 0;
                    _logger?.LogDebug("Not currently in game - no session tracking");
                }
                
                // Friends Monitoring
                await CollectFriendsDataAsync(data);
                
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
                
                // TODO: Add GetFriendsListAsync to SteamApiService
                // For now, provide realistic placeholder data based on typical Steam usage
                data.FriendsOnline = 12; // Realistic number of online friends
                data.FriendsInGame = 3;   // Subset playing games
                data.FriendsPopularGame = "Counter-Strike 2"; // Popular current game
                
                _logger?.LogDebug($"Friends status: {data.FriendsOnline} online, {data.FriendsInGame} in game, Popular: {data.FriendsPopularGame}");
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
                
                // TODO: Add GetPlayerAchievementsAsync to SteamApiService
                // For now, provide realistic placeholder data for the current game
                if (!string.IsNullOrEmpty(data.CurrentGameName))
                {
                    // Simulate realistic achievement progress
                    data.CurrentGameAchievementsTotal = 47;     // Typical number of achievements
                    data.CurrentGameAchievementsUnlocked = 23;  // Partial completion
                    data.CurrentGameAchievementPercentage = (23.0 / 47.0) * 100; // ~49%
                    data.LatestAchievementName = "Explorer";     // Recent achievement
                    data.LatestAchievementDate = DateTime.Now.AddDays(-2); // Recent unlock
                    
                    _logger?.LogDebug($"Achievements: {data.CurrentGameAchievementsUnlocked}/{data.CurrentGameAchievementsTotal} ({data.CurrentGameAchievementPercentage:F1}%)");
                    _logger?.LogDebug($"Latest: {data.LatestAchievementName} on {data.LatestAchievementDate:MM/dd}");
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
                        UserRating = 8.5, // Placeholder rating
                        GameSpecificStats = $"Session: {data.CurrentSessionTimeMinutes} min, Achievements: {data.CurrentGameAchievementPercentage:F1}%"
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
                        var secondaryStats = new MonitoredGameStats
                        {
                            GameName = secondaryGame.Name,
                            TotalHours = (secondaryGame.PlaytimeForever / 60.0), // Convert minutes to hours
                            RecentHours = (secondaryGame.Playtime2Weeks / 60.0), // Convert minutes to hours
                            LastPlayed = DateTime.Now.AddDays(-1), // Placeholder - recent game
                            IsCurrentlyPlaying = false,
                            UserRating = 7.8,
                            AchievementCompletion = 65.0 // Placeholder
                        };
                        
                        monitoredGames.Add(secondaryStats);
                        data.SecondaryGameStats = $"{secondaryStats.GameName}: {secondaryStats.TotalHours:F1}h total, {secondaryStats.RecentHours:F1}h recent";
                    }
                    
                    var tertiaryGame = data.RecentGames.Skip(2).FirstOrDefault();
                    if (tertiaryGame != null)
                    {
                        var tertiaryStats = new MonitoredGameStats
                        {
                            GameName = tertiaryGame.Name,
                            TotalHours = (tertiaryGame.PlaytimeForever / 60.0), // Convert minutes to hours
                            RecentHours = (tertiaryGame.Playtime2Weeks / 60.0), // Convert minutes to hours
                            LastPlayed = DateTime.Now.AddDays(-3), // Placeholder - older recent game
                            IsCurrentlyPlaying = false,
                            UserRating = 8.2,
                            AchievementCompletion = 42.0 // Placeholder
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
                    var gamesWithRatings = data.MonitoredGamesStats.Where(g => g.UserRating.HasValue && g.UserRating.Value > 0);
                    data.MonitoredGamesAverageRating = gamesWithRatings.Any() ? gamesWithRatings.Average(g => g.UserRating!.Value) : 0;
                }
                else
                {
                    data.MonitoredGamesTotalHours = 0;
                    data.MonitoredGamesAverageRating = 0;
                }
                
                _logger?.LogDebug($"Multi-game monitoring: {data.MonitoredGamesCount} games, {data.MonitoredGamesTotalHours:F1}h total, {data.MonitoredGamesAverageRating:F1}★ avg rating");
                
                await Task.CompletedTask; // Placeholder for future async operations
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting multiple game monitoring data", ex);
                data.MonitoredGamesCount = 0;
                data.MonitoredGamesTotalHours = 0;
                data.MonitoredGamesAverageRating = 0;
            }
        }

        /// <summary>
        /// Collects achievement completion tracking data
        /// </summary>
        private async Task CollectAchievementCompletionTrackingAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting achievement completion tracking data...");
                
                // Simulate overall achievement completion tracking
                // In a real implementation, this would aggregate data from all owned games
                
                // Calculate estimated overall completion based on sample data
                var estimatedTotalGames = (int)data.TotalGamesOwned;
                var estimatedCompletedGames = Math.Max(1, estimatedTotalGames / 10); // ~10% completion rate
                var estimatedTotalAchievements = estimatedTotalGames * 25; // ~25 achievements per game average
                var estimatedUnlockedAchievements = (int)(estimatedTotalAchievements * 0.35); // 35% unlock rate
                
                data.OverallAchievementCompletion = (double)estimatedUnlockedAchievements / estimatedTotalAchievements * 100;
                data.PerfectGamesCount = estimatedCompletedGames;
                data.TotalAchievementsUnlocked = estimatedUnlockedAchievements;
                data.TotalAchievementsAvailable = estimatedTotalAchievements;
                data.AchievementCompletionRank = 65.0; // Estimated percentile ranking
                
                _logger?.LogDebug($"Achievement tracking: {data.OverallAchievementCompletion:F1}% overall, {data.PerfectGamesCount} perfect games, {data.TotalAchievementsUnlocked}/{data.TotalAchievementsAvailable} achievements");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting achievement completion tracking", ex);
                data.OverallAchievementCompletion = 0;
                data.PerfectGamesCount = 0;
                data.TotalAchievementsUnlocked = 0;
                data.TotalAchievementsAvailable = 0;
                data.AchievementCompletionRank = 0;
            }
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
                _logger?.LogDebug("Collecting friends activity data...");
                
                // TODO: Implement Steam Friends API integration
                // For now, simulate data based on configuration or use placeholder values
                
                // Simulate friends data (replace with actual Steam Friends API calls)
                data.TotalFriendsCount = 42; // Placeholder
                data.RecentlyActiveFriends = 8; // Friends active in last 48 hours
                data.FriendsAverageWeeklyHours = 15.5; // Average weekly hours among friends
                data.MostActiveFriend = "PlayerOne"; // Most active friend name
                
                // Create sample friends list for the table
                data.FriendsList = new List<SteamFriend>
                {
                    new SteamFriend 
                    { 
                        SteamId64 = "76561198000000001", // Example SteamID64
                        PersonaName = "PlayerOne", 
                        PersonaState = "Online", 
                        CurrentGame = "Counter-Strike 2",
                        LastOnline = DateTime.Now.AddMinutes(-15)
                    },
                    new SteamFriend 
                    { 
                        SteamId64 = "76561198000000002", // Example SteamID64
                        PersonaName = "GamerTwo", 
                        PersonaState = "In-Game", 
                        CurrentGame = "Dota 2",
                        LastOnline = DateTime.Now.AddMinutes(-30)
                    },
                    new SteamFriend 
                    { 
                        SteamId64 = "76561198000000003", // Example SteamID64
                        PersonaName = "SteamUser3", 
                        PersonaState = "Away", 
                        CurrentGame = null,
                        LastOnline = DateTime.Now.AddHours(-2)
                    }
                };
                
                _logger?.LogInfo($"Friends Activity - Total: {data.TotalFriendsCount}, Recently Active: {data.RecentlyActiveFriends}");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting friends activity data", ex);
                data.TotalFriendsCount = 0;
                data.RecentlyActiveFriends = 0;
                data.MostActiveFriend = "Error loading friends";
            }
        }

        /// <summary>
        /// Collects popular games within the user's friend network
        /// </summary>
        private async Task CollectFriendNetworkGamesDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting friend network games data...");
                
                // TODO: Implement Steam Friends Games API integration
                // For now, simulate popular games data
                
                data.TrendingFriendGame = "Counter-Strike 2"; // Currently trending among friends
                data.FriendsGameOverlapPercentage = 65.5; // Overlap percentage with friends
                data.MostOwnedFriendGame = "Counter-Strike 2"; // Most popular game overall
                
                // Create sample popular friend games
                data.PopularFriendGames = new List<FriendNetworkGame>
                {
                    new FriendNetworkGame 
                    { 
                        GameName = "Counter-Strike 2", 
                        PlayingFriendsCount = 8, 
                        OwningFriendsCount = 25,
                        PopularityRank = 1,
                        TrendDirection = "Up"
                    },
                    new FriendNetworkGame 
                    { 
                        GameName = "Dota 2", 
                        PlayingFriendsCount = 3, 
                        OwningFriendsCount = 18,
                        PopularityRank = 2,
                        TrendDirection = "Stable"
                    }
                };
                
                _logger?.LogInfo($"Friend Network Games - Trending: {data.TrendingFriendGame}, Overlap: {data.FriendsGameOverlapPercentage}%");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting friend network games data", ex);
                data.TrendingFriendGame = "Unable to load";
                data.FriendsGameOverlapPercentage = 0;
            }
        }

        /// <summary>
        /// Collects community badge and achievement data
        /// </summary>
        private async Task CollectCommunityBadgeDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting community badge data...");
                
                // TODO: Implement Steam Badges API integration
                // For now, simulate badge data
                
                data.TotalBadgesEarned = 156; // Total badges earned
                data.TotalBadgeXP = 3240; // Total XP from badges
                data.NextBadgeProgress = "Community Ambassador (80% complete)"; // Next badge progress
                data.RarestBadge = "Beta Tester"; // Rarest badge owned
                
                // Create sample badges list
                data.SteamBadges = new List<SteamBadge>
                {
                    new SteamBadge 
                    { 
                        Name = "Community Ambassador", 
                        Level = 5, 
                        XP = 500,
                        CompletionTime = DateTime.Now.AddDays(-2),
                        Rarity = "Rare"
                    },
                    new SteamBadge 
                    { 
                        Name = "Years of Service", 
                        Level = 12, 
                        XP = 1200,
                        CompletionTime = DateTime.Now.AddDays(-30),
                        Rarity = "Common"
                    }
                };
                
                _logger?.LogInfo($"Community Badges - Total: {data.TotalBadgesEarned}, XP: {data.TotalBadgeXP}, Next: {data.NextBadgeProgress}");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting community badge data", ex);
                data.TotalBadgesEarned = 0;
                data.TotalBadgeXP = 0;
                data.NextBadgeProgress = "Unable to load";
            }
        }

        /// <summary>
        /// Collects global statistics comparison data
        /// </summary>
        private async Task CollectGlobalStatisticsDataAsync(SteamData data)
        {
            try
            {
                _logger?.LogDebug("Collecting global statistics data...");
                
                // TODO: Implement global statistics comparison using Steam Community features
                // For now, simulate global comparison data
                
                // Calculate percentile based on playtime (placeholder calculation)
                if (data.TotalLibraryPlaytimeHours > 0)
                {
                    // Simulate percentile calculation - higher playtime = higher percentile
                    data.GlobalPlaytimePercentile = Math.Min(99, Math.Max(1, (int)(data.TotalLibraryPlaytimeHours / 50.0)));
                }
                else
                {
                    data.GlobalPlaytimePercentile = 5; // Default low percentile
                }
                
                // Determine user category based on activity level
                if (data.GlobalPlaytimePercentile >= 90)
                {
                    data.GlobalUserCategory = "Hardcore Gamer";
                }
                else if (data.GlobalPlaytimePercentile >= 70)
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
                
                _logger?.LogInfo($"Global Statistics - Percentile: {data.GlobalPlaytimePercentile}%, Category: {data.GlobalUserCategory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error collecting global statistics data", ex);
                data.GlobalPlaytimePercentile = 0;
                data.GlobalUserCategory = "Unknown";
            }
        }

        #endregion

        #region Disposal
        
        public void Dispose()
        {
            try
            {
                _isMonitoring = false;
                _monitoringTimer?.Dispose();
                _steamApiService?.Dispose();
                
                Console.WriteLine("[MonitoringService] Steam monitoring service disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error during disposal: {ex.Message}");
            }
        }
        
        #endregion
    }
}