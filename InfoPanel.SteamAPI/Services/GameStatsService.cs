using InfoPanel.SteamAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for game statistics and achievements
    /// </summary>
    public static class GameStatsConstants
    {
        // Achievement completion thresholds
        public const double PERFECT_COMPLETION_THRESHOLD = 100.0;
        public const double NEARLY_PERFECT_THRESHOLD = 90.0;
        public const double EXCELLENT_THRESHOLD = 75.0;
        public const double GOOD_THRESHOLD = 50.0;
        public const double FAIR_THRESHOLD = 25.0;
        public const double STARTED_THRESHOLD = 0.0;
        
        // Achievement hunter level thresholds
        public const int LEGEND_PERFECT_GAMES_THRESHOLD = 20;
        public const int MASTER_PERFECT_GAMES_THRESHOLD = 10;
        public const int EXPERT_PERFECT_GAMES_THRESHOLD = 5;
        public const int ENTHUSIAST_PERFECT_GAMES_THRESHOLD = 2;
        public const int HUNTER_TOTAL_ACHIEVEMENTS_THRESHOLD = 100;
        
        // Gaming dedication thresholds
        public const int HARDCORE_STREAK_THRESHOLD = 30;
        public const double HARDCORE_MONTHLY_HOURS_THRESHOLD = 80.0;
        public const int DEDICATED_STREAK_THRESHOLD = 14;
        public const double DEDICATED_MONTHLY_HOURS_THRESHOLD = 40.0;
        public const int REGULAR_STREAK_THRESHOLD = 7;
        public const double REGULAR_MONTHLY_HOURS_THRESHOLD = 20.0;
        public const double CASUAL_MONTHLY_HOURS_THRESHOLD = 5.0;
        
        // Default values
        public const int DEFAULT_APP_ID = 0;
        public const int DEFAULT_ACHIEVEMENT_COUNT = 0;
        public const double DEFAULT_PERCENTAGE = 0.0;
        public const double DEFAULT_PLAYTIME = 0.0;
        public const int DEFAULT_STREAK_DAYS = 0;
        
        // Error messages
        public const string NO_CURRENT_GAME_MESSAGE = "No current game";
        public const string ERROR_LOADING_NEWS = "Error loading news";
        public const string UNKNOWN_CATEGORY = "Unknown";
    }

    /// <summary>
    /// Service responsible for collecting detailed game statistics and achievements
    /// Handles game-specific achievements, detailed stats, game news, and advanced features
    /// Optimized for slow updates (60-second intervals) since this data changes infrequently
    /// </summary>
    public class GameStatsService
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly SteamApiService _steamApiService;
        
        #endregion

        #region Constructor
        
        public GameStatsService(
            ConfigurationService configService, 
            SteamApiService steamApiService,
            FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _steamApiService = steamApiService ?? throw new ArgumentNullException(nameof(steamApiService));
            _logger = logger;
        }
        
        #endregion

        #region Game Stats Data Collection

        /// <summary>
        /// Collects detailed game statistics and achievements (slow tier - 60s interval)
        /// Achievements, game-specific stats, news, and advanced features
        /// </summary>
        public async Task<GameStatsData> CollectGameStatsDataAsync(string? currentGameName = null, int currentGameAppId = GameStatsConstants.DEFAULT_APP_ID)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Starting game stats data collection...");
                var gameStatsData = new GameStatsData();

                // 1. Collect achievement data for current game
                if (!string.IsNullOrEmpty(currentGameName) && currentGameAppId > GameStatsConstants.DEFAULT_APP_ID)
                {
                    await CollectCurrentGameAchievementsAsync(gameStatsData, currentGameName, currentGameAppId);
                }
                else
                {
                    _logger?.LogDebug($"[GameStatsService] {GameStatsConstants.NO_CURRENT_GAME_MESSAGE} - skipping achievement collection");
                    SetDefaultAchievementValues(gameStatsData);
                }

                // 2. Collect overall achievement statistics
                await CollectOverallAchievementStatsAsync(gameStatsData);

                // 3. Collect game news and updates (placeholder)
                await CollectGameNewsAsync(gameStatsData, currentGameAppId);

                // 4. Collect advanced gaming metrics
                await CollectAdvancedGamingMetricsAsync(gameStatsData);

                // Set status based on collected data
                gameStatsData.Status = "Game stats updated";
                gameStatsData.Timestamp = DateTime.Now;
                
                _logger?.LogDebug($"[GameStatsService] Game stats collection completed - Achievements: {gameStatsData.CurrentGameAchievementsUnlocked}/{gameStatsData.CurrentGameAchievementsTotal}");
                return gameStatsData;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting game stats data", ex);
                return new GameStatsData
                {
                    HasError = true,
                    ErrorMessage = $"Game stats error: {ex.Message}",
                    Status = "Error",
                    Timestamp = DateTime.Now
                };
            }
        }

        #endregion

        #region Private Data Collection Methods

        /// <summary>
        /// Collects achievement data for the current game
        /// </summary>
        private Task CollectCurrentGameAchievementsAsync(GameStatsData gameStatsData, string gameName, int appId)
        {
            try
            {
                _logger?.LogDebug($"[GameStatsService] Collecting achievements for {gameName} (ID: {appId})");
                
                // TODO: Implement real Steam achievements API call
                // This is a placeholder implementation until Steam achievements API is integrated
                
                gameStatsData.CurrentGameName = gameName;
                gameStatsData.CurrentGameAppId = appId;
                gameStatsData.CurrentGameAchievementsTotal = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                gameStatsData.CurrentGameAchievementsUnlocked = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                gameStatsData.CurrentGameAchievementPercentage = GameStatsConstants.DEFAULT_PERCENTAGE;
                
                gameStatsData.LatestAchievementName = null;
                gameStatsData.LatestAchievementDate = null;
                gameStatsData.LatestAchievementDescription = null;
                
                _logger?.LogInfo($"[GameStatsService] Current game achievements: {gameStatsData.CurrentGameAchievementsUnlocked}/{gameStatsData.CurrentGameAchievementsTotal} ({gameStatsData.CurrentGameAchievementPercentage}%)");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting current game achievements", ex);
                SetDefaultAchievementValues(gameStatsData);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Collects overall achievement statistics across all games
        /// </summary>
        private Task CollectOverallAchievementStatsAsync(GameStatsData gameStatsData)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Collecting overall achievement statistics...");
                
                // TODO: Implement real overall achievement statistics aggregation
                // This would require analyzing achievement data across all owned games
                
                gameStatsData.TotalAchievements = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                gameStatsData.PerfectGames = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                gameStatsData.AverageGameCompletion = GameStatsConstants.DEFAULT_PERCENTAGE;
                gameStatsData.RareAchievementsUnlocked = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                
                _logger?.LogInfo($"[GameStatsService] Overall achievements: {gameStatsData.TotalAchievements} total, {gameStatsData.PerfectGames} perfect games");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting overall achievement stats", ex);
                gameStatsData.TotalAchievements = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                gameStatsData.PerfectGames = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                gameStatsData.AverageGameCompletion = GameStatsConstants.DEFAULT_PERCENTAGE;
                gameStatsData.RareAchievementsUnlocked = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Collects game news and updates
        /// </summary>
        private Task CollectGameNewsAsync(GameStatsData gameStatsData, int appId)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Collecting game news...");
                
                // TODO: Implement real Steam news API call
                // This would fetch actual game news from Steam's news API
                
                if (appId > GameStatsConstants.DEFAULT_APP_ID)
                {
                    gameStatsData.LatestNewsTitle = null; // No news data until API implemented
                    gameStatsData.LatestNewsDate = null;
                    gameStatsData.LatestNewsUrl = null;
                }
                else
                {
                    gameStatsData.LatestNewsTitle = null;
                    gameStatsData.LatestNewsDate = null;
                    gameStatsData.LatestNewsUrl = null;
                }
                
                _logger?.LogDebug($"[GameStatsService] Game news collected: {gameStatsData.LatestNewsTitle ?? "No news"}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting game news", ex);
                gameStatsData.LatestNewsTitle = GameStatsConstants.ERROR_LOADING_NEWS;
                gameStatsData.LatestNewsDate = null;
                gameStatsData.LatestNewsUrl = null;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Collects advanced gaming metrics
        /// </summary>
        private Task CollectAdvancedGamingMetricsAsync(GameStatsData gameStatsData)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Collecting advanced gaming metrics...");
                
                // TODO: Implement real advanced metrics calculation
                // This would analyze complex gaming patterns and statistics
                
                gameStatsData.GlobalPlaytimePercentile = GameStatsConstants.DEFAULT_PERCENTAGE;
                gameStatsData.GlobalUserCategory = GameStatsConstants.UNKNOWN_CATEGORY;
                gameStatsData.GamingStreakDays = GameStatsConstants.DEFAULT_STREAK_DAYS;
                gameStatsData.MonthlyPlaytimeHours = GameStatsConstants.DEFAULT_PLAYTIME;
                
                _logger?.LogInfo($"[GameStatsService] Advanced metrics: {gameStatsData.GlobalPlaytimePercentile}% percentile, {gameStatsData.GlobalUserCategory} category");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting advanced gaming metrics", ex);
                gameStatsData.GlobalPlaytimePercentile = GameStatsConstants.DEFAULT_PERCENTAGE;
                gameStatsData.GlobalUserCategory = GameStatsConstants.UNKNOWN_CATEGORY;
                gameStatsData.GamingStreakDays = GameStatsConstants.DEFAULT_STREAK_DAYS;
                gameStatsData.MonthlyPlaytimeHours = GameStatsConstants.DEFAULT_PLAYTIME;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Sets default values for achievement data when collection fails
        /// </summary>
        private void SetDefaultAchievementValues(GameStatsData gameStatsData)
        {
            gameStatsData.CurrentGameName = null;
            gameStatsData.CurrentGameAppId = GameStatsConstants.DEFAULT_APP_ID;
            gameStatsData.CurrentGameAchievementsTotal = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
            gameStatsData.CurrentGameAchievementsUnlocked = GameStatsConstants.DEFAULT_ACHIEVEMENT_COUNT;
            gameStatsData.CurrentGameAchievementPercentage = GameStatsConstants.DEFAULT_PERCENTAGE;
            gameStatsData.LatestAchievementName = null;
            gameStatsData.LatestAchievementDate = null;
            gameStatsData.LatestAchievementDescription = null;
        }

        #endregion
    }

    /// <summary>
    /// Data model specifically for game statistics and achievement information
    /// Contains achievement data, game stats, news, and advanced gaming metrics
    /// </summary>
    public class GameStatsData
    {
        #region Core Properties
        
        public string? Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        
        #endregion

        #region Current Game Achievement Properties
        
        /// <summary>
        /// Name of the game for achievement tracking
        /// </summary>
        public string? CurrentGameName { get; set; }
        
        /// <summary>
        /// App ID of the game for achievement tracking
        /// </summary>
        public int CurrentGameAppId { get; set; }
        
        /// <summary>
        /// Total achievements available in current game
        /// </summary>
        public int CurrentGameAchievementsTotal { get; set; }
        
        /// <summary>
        /// Achievements unlocked in current game
        /// </summary>
        public int CurrentGameAchievementsUnlocked { get; set; }
        
        /// <summary>
        /// Achievement completion percentage for current game
        /// </summary>
        public double CurrentGameAchievementPercentage { get; set; }
        
        /// <summary>
        /// Name of most recently unlocked achievement
        /// </summary>
        public string? LatestAchievementName { get; set; }
        
        /// <summary>
        /// Date when latest achievement was unlocked
        /// </summary>
        public DateTime? LatestAchievementDate { get; set; }
        
        /// <summary>
        /// Description of latest achievement
        /// </summary>
        public string? LatestAchievementDescription { get; set; }
        
        #endregion

        #region Overall Achievement Statistics
        
        /// <summary>
        /// Total achievements unlocked across all games
        /// </summary>
        public int TotalAchievements { get; set; }
        
        /// <summary>
        /// Number of games with 100% achievement completion
        /// </summary>
        public int PerfectGames { get; set; }
        
        /// <summary>
        /// Average game completion percentage across all games
        /// </summary>
        public double AverageGameCompletion { get; set; }
        
        /// <summary>
        /// Number of rare achievements unlocked
        /// </summary>
        public int RareAchievementsUnlocked { get; set; }
        
        #endregion

        #region Game News Properties
        
        /// <summary>
        /// Title of latest game news
        /// </summary>
        public string? LatestNewsTitle { get; set; }
        
        /// <summary>
        /// Date of latest game news
        /// </summary>
        public DateTime? LatestNewsDate { get; set; }
        
        /// <summary>
        /// URL to latest game news
        /// </summary>
        public string? LatestNewsUrl { get; set; }
        
        #endregion

        #region Advanced Gaming Metrics
        
        /// <summary>
        /// Player's global playtime percentile ranking
        /// </summary>
        public double GlobalPlaytimePercentile { get; set; }
        
        /// <summary>
        /// Player's gaming category (Casual, Enthusiast, Hardcore, etc.)
        /// </summary>
        public string? GlobalUserCategory { get; set; }
        
        /// <summary>
        /// Current consecutive gaming streak in days
        /// </summary>
        public int GamingStreakDays { get; set; }
        
        /// <summary>
        /// Total playtime hours in current month
        /// </summary>
        public double MonthlyPlaytimeHours { get; set; }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Gets achievement completion level for current game
        /// </summary>
        public string GetCurrentGameCompletionLevel()
        {
            if (HasError) return GameStatsConstants.UNKNOWN_CATEGORY;
            if (CurrentGameAchievementPercentage >= GameStatsConstants.PERFECT_COMPLETION_THRESHOLD) return "Perfect";
            if (CurrentGameAchievementPercentage >= GameStatsConstants.NEARLY_PERFECT_THRESHOLD) return "Nearly Perfect";
            if (CurrentGameAchievementPercentage >= GameStatsConstants.EXCELLENT_THRESHOLD) return "Excellent";
            if (CurrentGameAchievementPercentage >= GameStatsConstants.GOOD_THRESHOLD) return "Good";
            if (CurrentGameAchievementPercentage >= GameStatsConstants.FAIR_THRESHOLD) return "Fair";
            if (CurrentGameAchievementPercentage > GameStatsConstants.STARTED_THRESHOLD) return "Started";
            return "None";
        }
        
        /// <summary>
        /// Gets overall achievement hunter level
        /// </summary>
        public string GetAchievementHunterLevel()
        {
            if (HasError) return GameStatsConstants.UNKNOWN_CATEGORY;
            if (PerfectGames >= GameStatsConstants.LEGEND_PERFECT_GAMES_THRESHOLD) return "Legend";
            if (PerfectGames >= GameStatsConstants.MASTER_PERFECT_GAMES_THRESHOLD) return "Master";
            if (PerfectGames >= GameStatsConstants.EXPERT_PERFECT_GAMES_THRESHOLD) return "Expert";
            if (PerfectGames >= GameStatsConstants.ENTHUSIAST_PERFECT_GAMES_THRESHOLD) return "Enthusiast";
            if (TotalAchievements >= GameStatsConstants.HUNTER_TOTAL_ACHIEVEMENTS_THRESHOLD) return "Hunter";
            return "Beginner";
        }
        
        /// <summary>
        /// Gets gaming dedication level based on streak and monthly hours
        /// </summary>
        public string GetGamingDedicationLevel()
        {
            if (HasError) return GameStatsConstants.UNKNOWN_CATEGORY;
            if (GamingStreakDays >= GameStatsConstants.HARDCORE_STREAK_THRESHOLD && MonthlyPlaytimeHours >= GameStatsConstants.HARDCORE_MONTHLY_HOURS_THRESHOLD) return "Hardcore";
            if (GamingStreakDays >= GameStatsConstants.DEDICATED_STREAK_THRESHOLD && MonthlyPlaytimeHours >= GameStatsConstants.DEDICATED_MONTHLY_HOURS_THRESHOLD) return "Dedicated";
            if (GamingStreakDays >= GameStatsConstants.REGULAR_STREAK_THRESHOLD || MonthlyPlaytimeHours >= GameStatsConstants.REGULAR_MONTHLY_HOURS_THRESHOLD) return "Regular";
            if (MonthlyPlaytimeHours >= GameStatsConstants.CASUAL_MONTHLY_HOURS_THRESHOLD) return "Casual";
            return "Occasional";
        }
        
        #endregion
    }
}