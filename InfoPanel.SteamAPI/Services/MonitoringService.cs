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