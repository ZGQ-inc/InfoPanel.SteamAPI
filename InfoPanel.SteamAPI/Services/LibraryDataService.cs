using InfoPanel.SteamAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for library data and playtime calculations
    /// </summary>
    public static class LibraryConstants
    {
        // Time conversion constants
        public const double MINUTES_TO_HOURS_DIVISOR = 60.0;
        
        // Game state constants
        public const int UNPLAYED_GAME_PLAYTIME = 0;
        public const int INVALID_GAME_APP_ID = 0;
        
        // Library engagement thresholds (hours)
        public const double HARDCORE_GAMER_THRESHOLD = 50.0;
        public const double ENTHUSIAST_THRESHOLD = 20.0;
        public const double REGULAR_PLAYER_THRESHOLD = 5.0;
        public const double CASUAL_PLAYER_THRESHOLD = 1.0;
        
        // Recent activity thresholds (hours)
        public const double VERY_ACTIVE_THRESHOLD = 40.0;
        public const double ACTIVE_THRESHOLD = 20.0;
        public const double MODERATE_THRESHOLD = 5.0;
        public const double LIGHT_THRESHOLD = 0.0;
        
        // Default values
        public const double DEFAULT_PLAYTIME = 0.0;
        public const int DEFAULT_GAME_COUNT = 0;
    }

    /// <summary>
    /// Service responsible for collecting game library and playtime data
    /// Handles owned games, library statistics, recent games, and playtime tracking
    /// Optimized for slow updates (60-second intervals) since library data changes infrequently
    /// </summary>
    public class LibraryDataService
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly SteamApiService _steamApiService;
        
        #endregion

        #region Constructor
        
        public LibraryDataService(
            ConfigurationService configService, 
            SteamApiService steamApiService,
            FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _steamApiService = steamApiService ?? throw new ArgumentNullException(nameof(steamApiService));
            _logger = logger;
        }
        
        #endregion

        #region Library Data Collection

        /// <summary>
        /// Collects game library and playtime data (slow tier - 60s interval)
        /// Owned games, library statistics, recent games, playtime tracking
        /// </summary>
        public async Task<LibraryData> CollectLibraryDataAsync()
        {
            try
            {
                _logger?.LogDebug("[LibraryDataService] Starting library data collection...");
                var libraryData = new LibraryData();

                // 1. Collect owned games data
                if (_configService.EnableLibraryMonitoring)
                {
                    await CollectOwnedGamesDataAsync(libraryData);
                }
                else
                {
                    _logger?.LogDebug("[LibraryDataService] Library monitoring is disabled");
                }

                // 2. Collect recent activity data
                if (_configService.EnableCurrentGameMonitoring)
                {
                    await CollectRecentActivityDataAsync(libraryData);
                }
                else
                {
                    _logger?.LogDebug("[LibraryDataService] Current game monitoring is disabled");
                }

                // Set status based on collected data
                libraryData.Status = "Library data updated";
                libraryData.Timestamp = DateTime.Now;
                
                _logger?.LogDebug($"[LibraryDataService] Library data collection completed - Games: {libraryData.TotalGamesOwned}, Total Hours: {libraryData.TotalLibraryPlaytimeHours:F1}");
                return libraryData;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[LibraryDataService] Error collecting library data", ex);
                return new LibraryData
                {
                    HasError = true,
                    ErrorMessage = $"Library data error: {ex.Message}",
                    Status = "Error",
                    Timestamp = DateTime.Now
                };
            }
        }

        #endregion

        #region Private Data Collection Methods

        /// <summary>
        /// Collects owned games data and library statistics
        /// </summary>
        private async Task CollectOwnedGamesDataAsync(LibraryData libraryData)
        {
            try
            {
                _logger?.LogDebug("[LibraryDataService] Collecting owned games data...");
                var ownedGames = await _steamApiService.GetOwnedGamesAsync();
                
                if (ownedGames?.Response?.Games?.Any() == true)
                {
                    var games = ownedGames.Response.Games;
                    libraryData.TotalGamesOwned = games.Count;
                    libraryData.TotalLibraryPlaytimeHours = games.Sum(g => g.PlaytimeForever) / LibraryConstants.MINUTES_TO_HOURS_DIVISOR;
                    
                    // Find most played game
                    var mostPlayed = games.OrderByDescending(g => g.PlaytimeForever).FirstOrDefault();
                    if (mostPlayed != null)
                    {
                        libraryData.MostPlayedGameName = mostPlayed.Name;
                        libraryData.MostPlayedGameHours = mostPlayed.PlaytimeForever / LibraryConstants.MINUTES_TO_HOURS_DIVISOR;
                        libraryData.MostPlayedGameAppId = mostPlayed.AppId;
                    }
                    
                    // Calculate library statistics
                    libraryData.UnplayedGames = games.Count(g => g.PlaytimeForever == LibraryConstants.UNPLAYED_GAME_PLAYTIME);
                    libraryData.AveragePlaytimePerGame = libraryData.TotalGamesOwned > LibraryConstants.DEFAULT_GAME_COUNT 
                        ? libraryData.TotalLibraryPlaytimeHours / libraryData.TotalGamesOwned 
                        : LibraryConstants.DEFAULT_PLAYTIME;
                    
                    _logger?.LogInfo($"[LibraryDataService] Library Data - Games: {libraryData.TotalGamesOwned}, Total Hours: {libraryData.TotalLibraryPlaytimeHours:F1}, Unplayed: {libraryData.UnplayedGames}");
                }
                else
                {
                    _logger?.LogWarning("[LibraryDataService] Owned games returned null or empty");
                    SetDefaultLibraryValues(libraryData);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("[LibraryDataService] Error collecting owned games data", ex);
                SetDefaultLibraryValues(libraryData);
            }
        }

        /// <summary>
        /// Collects recent activity and playtime data
        /// </summary>
        private async Task CollectRecentActivityDataAsync(LibraryData libraryData)
        {
            try
            {
                _logger?.LogDebug("[LibraryDataService] Collecting recent activity data...");
                var recentGames = await _steamApiService.GetRecentlyPlayedGamesAsync();
                
                if (recentGames?.Response?.Games?.Any() == true)
                {
                    var recentGamesList = recentGames.Response.Games;
                    libraryData.RecentPlaytimeHours = recentGamesList.Sum(g => g.Playtime2weeks ?? 0) / LibraryConstants.MINUTES_TO_HOURS_DIVISOR;
                    libraryData.RecentGamesCount = recentGamesList.Count;
                    libraryData.RecentGames = recentGamesList;
                    
                    // Find most played recent game
                    var mostPlayedRecent = recentGamesList.OrderByDescending(g => g.Playtime2weeks ?? 0).FirstOrDefault();
                    if (mostPlayedRecent != null)
                    {
                        libraryData.MostPlayedRecentGame = mostPlayedRecent.Name;
                        libraryData.MostPlayedRecentHours = (mostPlayedRecent.Playtime2weeks ?? 0) / LibraryConstants.MINUTES_TO_HOURS_DIVISOR;
                    }
                    
                    _logger?.LogInfo($"[LibraryDataService] Recent Activity - Games: {libraryData.RecentGamesCount}, Hours (2w): {libraryData.RecentPlaytimeHours:F1}");
                }
                else
                {
                    _logger?.LogWarning("[LibraryDataService] Recent games returned null or empty");
                    SetDefaultRecentValues(libraryData);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("[LibraryDataService] Error collecting recent activity data", ex);
                SetDefaultRecentValues(libraryData);
            }
        }

        /// <summary>
        /// Sets default values for library data when collection fails
        /// </summary>
        private void SetDefaultLibraryValues(LibraryData libraryData)
        {
            libraryData.TotalGamesOwned = LibraryConstants.DEFAULT_GAME_COUNT;
            libraryData.TotalLibraryPlaytimeHours = LibraryConstants.DEFAULT_PLAYTIME;
            libraryData.MostPlayedGameName = null;
            libraryData.MostPlayedGameHours = LibraryConstants.DEFAULT_PLAYTIME;
            libraryData.MostPlayedGameAppId = LibraryConstants.INVALID_GAME_APP_ID;
            libraryData.UnplayedGames = LibraryConstants.DEFAULT_GAME_COUNT;
            libraryData.AveragePlaytimePerGame = LibraryConstants.DEFAULT_PLAYTIME;
        }

        /// <summary>
        /// Sets default values for recent activity data when collection fails
        /// </summary>
        private void SetDefaultRecentValues(LibraryData libraryData)
        {
            libraryData.RecentPlaytimeHours = LibraryConstants.DEFAULT_PLAYTIME;
            libraryData.RecentGamesCount = LibraryConstants.DEFAULT_GAME_COUNT;
            libraryData.RecentGames = null;
            libraryData.MostPlayedRecentGame = null;
            libraryData.MostPlayedRecentHours = LibraryConstants.DEFAULT_PLAYTIME;
        }

        #endregion
    }

    /// <summary>
    /// Data model specifically for game library and playtime information
    /// Contains owned games, library statistics, and recent activity data
    /// </summary>
    public class LibraryData
    {
        #region Core Properties
        
        public string? Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        
        #endregion

        #region Library Statistics Properties
        
        /// <summary>
        /// Total number of owned games
        /// </summary>
        public int TotalGamesOwned { get; set; }
        
        /// <summary>
        /// Total playtime across all games in hours
        /// </summary>
        public double TotalLibraryPlaytimeHours { get; set; }
        
        /// <summary>
        /// Number of games never played
        /// </summary>
        public int UnplayedGames { get; set; }
        
        /// <summary>
        /// Average playtime per game in hours
        /// </summary>
        public double AveragePlaytimePerGame { get; set; }
        
        #endregion

        #region Most Played Game Properties
        
        /// <summary>
        /// Name of most played game
        /// </summary>
        public string? MostPlayedGameName { get; set; }
        
        /// <summary>
        /// Playtime for most played game in hours
        /// </summary>
        public double MostPlayedGameHours { get; set; }
        
        /// <summary>
        /// App ID of most played game
        /// </summary>
        public int MostPlayedGameAppId { get; set; }
        
        #endregion

        #region Recent Activity Properties
        
        /// <summary>
        /// Recent playtime (last 2 weeks) in hours
        /// </summary>
        public double RecentPlaytimeHours { get; set; }
        
        /// <summary>
        /// Number of games played recently (last 2 weeks)
        /// </summary>
        public int RecentGamesCount { get; set; }
        
        /// <summary>
        /// List of recently played games
        /// </summary>
        public System.Collections.Generic.List<SteamGame>? RecentGames { get; set; }
        
        /// <summary>
        /// Most played recent game name
        /// </summary>
        public string? MostPlayedRecentGame { get; set; }
        
        /// <summary>
        /// Hours played for most played recent game
        /// </summary>
        public double MostPlayedRecentHours { get; set; }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Gets library engagement level based on playtime and game count
        /// </summary>
        public string GetLibraryEngagement()
        {
            if (HasError) return "Unknown";
            
            var averageHours = AveragePlaytimePerGame;
            if (averageHours > LibraryConstants.HARDCORE_GAMER_THRESHOLD) return "Hardcore Gamer";
            if (averageHours > LibraryConstants.ENTHUSIAST_THRESHOLD) return "Enthusiast";
            if (averageHours > LibraryConstants.REGULAR_PLAYER_THRESHOLD) return "Regular Player";
            if (averageHours > LibraryConstants.CASUAL_PLAYER_THRESHOLD) return "Casual Player";
            return "Collector";
        }
        
        /// <summary>
        /// Gets recent activity level
        /// </summary>
        public string GetRecentActivityLevel()
        {
            if (HasError) return "Unknown";
            if (RecentPlaytimeHours > LibraryConstants.VERY_ACTIVE_THRESHOLD) return "Very Active";
            if (RecentPlaytimeHours > LibraryConstants.ACTIVE_THRESHOLD) return "Active";
            if (RecentPlaytimeHours > LibraryConstants.MODERATE_THRESHOLD) return "Moderate";
            if (RecentPlaytimeHours > LibraryConstants.LIGHT_THRESHOLD) return "Light";
            return "Inactive";
        }
        
        /// <summary>
        /// Gets library completion percentage (played vs unplayed)
        /// </summary>
        public double GetCompletionPercentage()
        {
            if (TotalGamesOwned == LibraryConstants.DEFAULT_GAME_COUNT) return LibraryConstants.DEFAULT_PLAYTIME;
            var playedGames = TotalGamesOwned - UnplayedGames;
            return (double)playedGames / TotalGamesOwned * 100;
        }
        
        #endregion
    }
}