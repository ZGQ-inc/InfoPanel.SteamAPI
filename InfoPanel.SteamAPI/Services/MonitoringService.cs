using InfoPanel.SteamAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for monitoring service timing and configuration
    /// </summary>
    public static class MonitoringConstants
    {
        #region Timer Configuration
        /// <summary>Milliseconds conversion factor for timer intervals</summary>
        public const int MILLISECONDS_PER_SECOND = 1000;
        
        /// <summary>Medium timer offset to stagger API calls (milliseconds)</summary>
        public const int MEDIUM_TIMER_OFFSET_MS = 500;
        
        /// <summary>Slow timer offset to stagger API calls (milliseconds)</summary>
        public const int SLOW_TIMER_OFFSET_MS = 1000;
        
        /// <summary>Monitoring loop delay interval (milliseconds)</summary>
        public const int MONITORING_LOOP_DELAY_MS = 1000;
        #endregion
        
        #region Steam ID Validation
        /// <summary>Required length for Steam ID64</summary>
        public const int STEAM_ID64_LENGTH = 17;
        
        /// <summary>Steam ID64 prefix for individual accounts</summary>
        public const string STEAM_ID64_PREFIX = "7656119";
        #endregion
        
        #region Default Values
        /// <summary>Default application ID when no current game</summary>
        public const int DEFAULT_APP_ID = 0;
        
        /// <summary>Initial cycle count for timer tracking</summary>
        public const int INITIAL_CYCLE_COUNT = 0;
        #endregion
        
        #region Logging Messages
        /// <summary>Service name prefix for consistent logging</summary>
        public const string SERVICE_NAME = "MonitoringService";
        
        /// <summary>Log message for already monitoring state</summary>
        public const string MSG_ALREADY_MONITORING = "Already monitoring";
        
        /// <summary>Log message for monitoring cancelled</summary>
        public const string MSG_MONITORING_CANCELLED = "Steam monitoring cancelled";
        
        /// <summary>Log message for monitoring stopped</summary>
        public const string MSG_MONITORING_STOPPED = "Tiered monitoring stopped";
        
        /// <summary>Log message for Steam API connection established</summary>
        public const string MSG_API_CONNECTION_ESTABLISHED = "Steam API connection established";
        
        /// <summary>Log message for specialized services initialized</summary>
        public const string MSG_SERVICES_INITIALIZED = "Specialized services initialized";
        
        /// <summary>Log message for service disposal</summary>
        public const string MSG_SERVICE_DISPOSED = "Tiered monitoring service disposed";
        #endregion
    }

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
        private volatile int _fastCycleCount = MonitoringConstants.INITIAL_CYCLE_COUNT;
        private volatile int _mediumCycleCount = MonitoringConstants.INITIAL_CYCLE_COUNT;
        private volatile int _slowCycleCount = MonitoringConstants.INITIAL_CYCLE_COUNT;
        
        // Current aggregated state - accumulates data from all services
        private SteamData? _currentAggregatedState;
        private readonly object _statelock = new();
        
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
                    Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] {MonitoringConstants.MSG_ALREADY_MONITORING}");
                    return;
                }
                
                _isMonitoring = true;
            }
            
            try
            {
                // Initialize Steam API service
                await InitializeSteamApiAsync();
                
                // Start tiered monitoring timers with different intervals
                var fastIntervalMs = _configService.FastUpdateIntervalSeconds * MonitoringConstants.MILLISECONDS_PER_SECOND;    // Game state, session time (5s)
                var mediumIntervalMs = _configService.MediumUpdateIntervalSeconds * MonitoringConstants.MILLISECONDS_PER_SECOND; // Friends status (15s) 
                var slowIntervalMs = _configService.SlowUpdateIntervalSeconds * MonitoringConstants.MILLISECONDS_PER_SECOND;     // Library stats, achievements (60s)
                
                _logger?.LogInfo($"Starting tiered monitoring: Fast={_configService.FastUpdateIntervalSeconds}s, Medium={_configService.MediumUpdateIntervalSeconds}s, Slow={_configService.SlowUpdateIntervalSeconds}s");
                
                // Start all timers with a small stagger to avoid simultaneous API calls
                _fastTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(fastIntervalMs));
                _mediumTimer.Change(TimeSpan.FromMilliseconds(MonitoringConstants.MEDIUM_TIMER_OFFSET_MS), TimeSpan.FromMilliseconds(mediumIntervalMs));  // 0.5s offset
                _slowTimer.Change(TimeSpan.FromMilliseconds(MonitoringConstants.SLOW_TIMER_OFFSET_MS), TimeSpan.FromMilliseconds(slowIntervalMs));     // 1s offset
                
                // Keep the task alive while monitoring
                while (_isMonitoring && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(MonitoringConstants.MONITORING_LOOP_DELAY_MS, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] {MonitoringConstants.MSG_MONITORING_CANCELLED}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Error during Steam monitoring: {ex.Message}");
                throw;
            }
            finally
            {
                StopMonitoring();
            }
        }
        
        /// <summary>
        /// Stops the Steam monitoring process
        /// </summary>
        public void StopMonitoring()
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
            
            Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] {MonitoringConstants.MSG_MONITORING_STOPPED}");
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
                    throw new InvalidOperationException($"Steam ID64 format is invalid: {steamId64}. Must be {MonitoringConstants.STEAM_ID64_LENGTH} digits starting with {MonitoringConstants.STEAM_ID64_PREFIX}.");
                }
                
                _steamApiService = new SteamApiService(apiKey, steamId64, _logger);
                
                // Test the connection
                var isValid = await _steamApiService.TestConnectionAsync();
                if (!isValid)
                {
                    throw new InvalidOperationException("Failed to connect to Steam API. Check your API key and Steam ID.");
                }
                
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] {MonitoringConstants.MSG_API_CONNECTION_ESTABLISHED}");
                
                // Initialize specialized data collection services
                InitializeSpecializedServices();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Failed to initialize Steam API: {ex.Message}");
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
                
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] {MonitoringConstants.MSG_SERVICES_INITIALIZED}");
                _logger?.LogInfo("Specialized data collection services initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Failed to initialize specialized services: {ex.Message}");
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
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Error in fast timer: {ex.Message}");
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
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Error in medium timer: {ex.Message}");
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
                
                // Collect ONLY library and game stats data (don't re-collect player data)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var libraryData = await _libraryDataService.CollectLibraryDataAsync();
                        var gameStatsData = await _gameStatsService.CollectGameStatsDataAsync("", 0);
                        
                        var steamData = new SteamData
                        {
                            Status = "Library data updated",
                            Timestamp = DateTime.Now
                        };
                        
                        // Only update library and achievement data
                        if (libraryData != null)
                        {
                            steamData.TotalGamesOwned = libraryData.TotalGamesOwned;
                            steamData.TotalLibraryPlaytimeHours = libraryData.TotalLibraryPlaytimeHours;
                            steamData.MostPlayedGameName = libraryData.MostPlayedGameName;
                            steamData.MostPlayedGameHours = libraryData.MostPlayedGameHours;
                            steamData.RecentPlaytimeHours = libraryData.RecentPlaytimeHours;
                            steamData.RecentGamesCount = libraryData.RecentGamesCount;
                            steamData.MostPlayedRecentGame = libraryData.MostPlayedRecentGame;  // Fix: Added missing recent game mapping
                            steamData.RecentGames = libraryData.RecentGames;                    // Fix: Added missing recent games list for table population
                        }
                        
                        // Removed artificial GameStatsData mappings (TotalAchievements, PerfectGames, AverageGameCompletion)
                        // These were placeholder calculations not available via Steam Web API
                        
                        OnDataUpdated(steamData);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Error collecting slow data", ex);
                        OnDataUpdated(new SteamData($"Slow data error: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Error in slow timer: {ex.Message}");
                _logger?.LogError($"Slow timer error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Triggers the DataUpdated event with accumulated/merged data
        /// </summary>
        private void OnDataUpdated(SteamData newData)
        {
            try
            {
                SteamData dataToSend;
                
                lock (_statelock)
                {
                    // Merge new data with current aggregated state
                    _currentAggregatedState = MergeDataIntoCurrentState(newData);
                    dataToSend = _currentAggregatedState;
                }
                
                DataUpdated?.Invoke(this, new DataUpdatedEventArgs(dataToSend));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Error in DataUpdated event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Merges new data into the current aggregated state, only updating non-empty values
        /// </summary>
        private SteamData MergeDataIntoCurrentState(SteamData newData)
        {
            // If we don't have a current state, create one from the new data
            if (_currentAggregatedState == null)
            {
                return new SteamData
                {
                    Status = newData.Status,
                    Timestamp = newData.Timestamp,
                    HasError = newData.HasError,
                    ErrorMessage = newData.ErrorMessage,
                    
                    // Copy all available data from the new data
                    PlayerName = newData.PlayerName,
                    OnlineState = newData.OnlineState,
                    SteamLevel = newData.SteamLevel,
                    ProfileUrl = newData.ProfileUrl,
                    AvatarUrl = newData.AvatarUrl,
                    ProfileImageUrl = newData.ProfileImageUrl,
                    LastLogOff = newData.LastLogOff,
                    CurrentGameName = newData.CurrentGameName,
                    CurrentGameAppId = newData.CurrentGameAppId,
                    CurrentGameExtraInfo = newData.CurrentGameExtraInfo,
                    CurrentGameBannerUrl = newData.CurrentGameBannerUrl,
                    TotalGamesOwned = newData.TotalGamesOwned,
                    TotalLibraryPlaytimeHours = newData.TotalLibraryPlaytimeHours,
                    MostPlayedGameName = newData.MostPlayedGameName,
                    MostPlayedGameHours = newData.MostPlayedGameHours,
                    RecentPlaytimeHours = newData.RecentPlaytimeHours,
                    RecentGamesCount = newData.RecentGamesCount,
                    MostPlayedRecentGame = newData.MostPlayedRecentGame,
                    RecentGames = newData.RecentGames,                           // Fix: Added recent games list
                    // Removed artificial achievement data mappings (TotalAchievements, PerfectGames, AverageGameCompletion)
                    
                    // Social data
                    TotalFriendsCount = newData.TotalFriendsCount,
                    FriendsOnline = newData.FriendsOnline,
                    FriendsInGame = newData.FriendsInGame,
                    FriendsPopularGame = newData.FriendsPopularGame
                };
            }
            
            // Merge new data with existing state - only update non-empty/non-default values
            var mergedData = new SteamData
            {
                Status = !string.IsNullOrEmpty(newData.Status) ? newData.Status : _currentAggregatedState.Status,
                Timestamp = newData.Timestamp != default ? newData.Timestamp : _currentAggregatedState.Timestamp,
                HasError = newData.HasError || _currentAggregatedState.HasError,
                ErrorMessage = !string.IsNullOrEmpty(newData.ErrorMessage) ? newData.ErrorMessage : _currentAggregatedState.ErrorMessage,
                
                // Player profile - only update if new data has meaningful values
                PlayerName = !string.IsNullOrEmpty(newData.PlayerName) ? newData.PlayerName : _currentAggregatedState.PlayerName,
                OnlineState = !string.IsNullOrEmpty(newData.OnlineState) ? newData.OnlineState : _currentAggregatedState.OnlineState,
                SteamLevel = newData.SteamLevel > 0 ? newData.SteamLevel : _currentAggregatedState.SteamLevel,
                ProfileUrl = !string.IsNullOrEmpty(newData.ProfileUrl) ? newData.ProfileUrl : _currentAggregatedState.ProfileUrl,
                AvatarUrl = !string.IsNullOrEmpty(newData.AvatarUrl) ? newData.AvatarUrl : _currentAggregatedState.AvatarUrl,
                ProfileImageUrl = !string.IsNullOrEmpty(newData.ProfileImageUrl) ? newData.ProfileImageUrl : _currentAggregatedState.ProfileImageUrl,
                LastLogOff = newData.LastLogOff > 0 ? newData.LastLogOff : _currentAggregatedState.LastLogOff,
                
                // Current game - update if new data provides game info
                CurrentGameName = !string.IsNullOrEmpty(newData.CurrentGameName) ? newData.CurrentGameName : _currentAggregatedState.CurrentGameName,
                CurrentGameAppId = newData.CurrentGameAppId > 0 ? newData.CurrentGameAppId : _currentAggregatedState.CurrentGameAppId,
                CurrentGameExtraInfo = !string.IsNullOrEmpty(newData.CurrentGameExtraInfo) ? newData.CurrentGameExtraInfo : _currentAggregatedState.CurrentGameExtraInfo,
                CurrentGameBannerUrl = !string.IsNullOrEmpty(newData.CurrentGameBannerUrl) ? newData.CurrentGameBannerUrl : _currentAggregatedState.CurrentGameBannerUrl,
                
                // Library data - update if new data provides library info
                TotalGamesOwned = newData.TotalGamesOwned > 0 ? newData.TotalGamesOwned : _currentAggregatedState.TotalGamesOwned,
                TotalLibraryPlaytimeHours = newData.TotalLibraryPlaytimeHours > 0 ? newData.TotalLibraryPlaytimeHours : _currentAggregatedState.TotalLibraryPlaytimeHours,
                MostPlayedGameName = !string.IsNullOrEmpty(newData.MostPlayedGameName) ? newData.MostPlayedGameName : _currentAggregatedState.MostPlayedGameName,
                MostPlayedGameHours = newData.MostPlayedGameHours > 0 ? newData.MostPlayedGameHours : _currentAggregatedState.MostPlayedGameHours,
                RecentPlaytimeHours = newData.RecentPlaytimeHours > 0 ? newData.RecentPlaytimeHours : _currentAggregatedState.RecentPlaytimeHours,
                RecentGamesCount = newData.RecentGamesCount > 0 ? newData.RecentGamesCount : _currentAggregatedState.RecentGamesCount,
                MostPlayedRecentGame = !string.IsNullOrEmpty(newData.MostPlayedRecentGame) ? newData.MostPlayedRecentGame : _currentAggregatedState.MostPlayedRecentGame,
                RecentGames = newData.RecentGames?.Count > 0 ? newData.RecentGames : _currentAggregatedState.RecentGames,
                
                // Removed artificial achievement data mappings (TotalAchievements, PerfectGames, AverageGameCompletion)
                // These were placeholder calculations not available via Steam Web API
                
                // Social data - update if new data provides social info
                TotalFriendsCount = newData.TotalFriendsCount > 0 ? newData.TotalFriendsCount : _currentAggregatedState.TotalFriendsCount,
                FriendsOnline = newData.FriendsOnline > 0 ? newData.FriendsOnline : _currentAggregatedState.FriendsOnline,
                FriendsInGame = newData.FriendsInGame > 0 ? newData.FriendsInGame : _currentAggregatedState.FriendsInGame,
                FriendsPopularGame = !string.IsNullOrEmpty(newData.FriendsPopularGame) ? newData.FriendsPopularGame : _currentAggregatedState.FriendsPopularGame,
                FriendsList = newData.FriendsList?.Count > 0 ? newData.FriendsList : _currentAggregatedState.FriendsList  // Fix: Include friends list in merge
            };
            
            return mergedData;
        }
        
        #endregion

        #region Data Conversion Methods

        /// <summary>
        /// Converts PlayerData to SteamData for event system compatibility
        /// </summary>
        private SteamData ConvertPlayerDataToSteamData(PlayerData playerData)
        {
            var steamData = new SteamData
            {
                // Core properties
                Status = playerData.Status,
                Timestamp = playerData.Timestamp,
                HasError = playerData.HasError,
                ErrorMessage = playerData.ErrorMessage,
                
                // Player profile
                PlayerName = playerData.PlayerName,
                SteamLevel = playerData.SteamLevel,  // Fix: Added missing Steam Level mapping
                ProfileUrl = playerData.ProfileUrl,
                AvatarUrl = playerData.AvatarUrl,
                ProfileImageUrl = playerData.ProfileImageUrl,
                OnlineState = playerData.OnlineState,
                LastLogOff = playerData.LastLogOff,
                
                // Current game
                CurrentGameName = playerData.CurrentGameName,
                CurrentGameAppId = playerData.CurrentGameAppId,
                CurrentGameExtraInfo = playerData.CurrentGameExtraInfo,
                CurrentGameBannerUrl = playerData.CurrentGameBannerUrl,
                CurrentGameServerIp = playerData.CurrentGameServerIp,
                
                // Details
                Details = $"Player data: {playerData.PlayerName}, Level: {playerData.SteamLevel}, Game: {playerData.CurrentGameName ?? "None"}"
            };
            
            // Debug logging for image URLs
            _logger?.LogDebug($"[MonitoringService] Converting PlayerData to SteamData - ProfileImageUrl: {playerData.ProfileImageUrl}, CurrentGameBannerUrl: {playerData.CurrentGameBannerUrl}");
            _logger?.LogDebug($"[MonitoringService] SteamData created - ProfileImageUrl: {steamData.ProfileImageUrl}, CurrentGameBannerUrl: {steamData.CurrentGameBannerUrl}");
            
            return steamData;
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
                
                // Social data mapping
                TotalFriendsCount = socialData.TotalFriends,        // Fix: Map total friends
                FriendsOnline = socialData.FriendsOnline,           // Fix: Map online friends
                FriendsInGame = socialData.FriendsInGame,           // Fix: Map friends in game
                FriendsPopularGame = socialData.FriendsPopularGame, // Fix: Map popular game
                
                // Fix: Map friends activity list for table population
                FriendsList = ConvertFriendsActivityToSteamFriends(socialData.FriendsActivity),
                
                Details = $"Social data: {socialData.FriendsOnline} friends online, {socialData.FriendsInGame} in game"
            };
        }

        /// <summary>
        /// Converts a list of FriendActivity objects to SteamFriend objects for table display
        /// </summary>
        private List<SteamFriend>? ConvertFriendsActivityToSteamFriends(List<FriendActivity>? friendsActivity)
        {
            if (friendsActivity == null || friendsActivity.Count == 0)
                return null;

            var steamFriends = new List<SteamFriend>();
            
            foreach (var friend in friendsActivity)
            {
                steamFriends.Add(new SteamFriend
                {
                    PersonaName = friend.FriendName,
                    OnlineStatus = friend.Status,
                    GameName = friend.CurrentGame,
                    LastLogOff = ((DateTimeOffset)friend.LastSeen).ToUnixTimeSeconds()
                });
            }
            
            return steamFriends;
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
                MostPlayedRecentGame = libraryData.MostPlayedRecentGame,   // Fix: Added missing recent game mapping
                RecentGames = libraryData.RecentGames,                     // Fix: Added missing recent games list for table population
                
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
                
                // Removed artificial achievement data mappings (TotalAchievements, PerfectGames, AverageGameCompletion)
                // These were placeholder calculations not available via Steam Web API
                
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
                var currentGameAppId = playerData?.CurrentGameAppId ?? MonitoringConstants.DEFAULT_APP_ID;
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
                    aggregatedData.ProfileImageUrl = playerData.ProfileImageUrl;
                    aggregatedData.OnlineState = playerData.OnlineState;
                    aggregatedData.LastLogOff = playerData.LastLogOff;
                    aggregatedData.CurrentGameName = playerData.CurrentGameName;
                    aggregatedData.CurrentGameAppId = playerData.CurrentGameAppId;
                    aggregatedData.CurrentGameExtraInfo = playerData.CurrentGameExtraInfo;
                    aggregatedData.CurrentGameBannerUrl = playerData.CurrentGameBannerUrl;
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
                    // Removed artificial achievement data mappings (TotalAchievements, PerfectGames, AverageGameCompletion)
                    // These were placeholder calculations not available via Steam Web API
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
                
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] {MonitoringConstants.MSG_SERVICE_DISPOSED}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{MonitoringConstants.SERVICE_NAME}] Error during disposal: {ex.Message}");
            }
        }
        
        #endregion
    }
}