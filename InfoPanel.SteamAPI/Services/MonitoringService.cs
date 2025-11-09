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

        #region Data Conversion Methods
        
        /// <summary>
        /// Collects friends activity data including online status and current games
        /// </summary>
        /// <summary>
        /// Collects global statistics comparison data
        /// </summary>
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