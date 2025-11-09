using InfoPanel.SteamAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
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
        public async Task<GameStatsData> CollectGameStatsDataAsync(string? currentGameName = null, int currentGameAppId = 0)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Starting game stats data collection...");
                var gameStatsData = new GameStatsData();

                // 1. Collect achievement data for current game
                if (!string.IsNullOrEmpty(currentGameName) && currentGameAppId > 0)
                {
                    await CollectCurrentGameAchievementsAsync(gameStatsData, currentGameName, currentGameAppId);
                }
                else
                {
                    _logger?.LogDebug("[GameStatsService] No current game - skipping achievement collection");
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
        private async Task CollectCurrentGameAchievementsAsync(GameStatsData gameStatsData, string gameName, int appId)
        {
            try
            {
                _logger?.LogDebug($"[GameStatsService] Collecting achievements for {gameName} (ID: {appId})");
                
                // Placeholder for achievement API calls
                // Real implementation would call Steam achievements API
                
                // Simulate achievement data based on game name
                gameStatsData.CurrentGameName = gameName;
                gameStatsData.CurrentGameAppId = appId;
                gameStatsData.CurrentGameAchievementsTotal = 47; // Simulated
                gameStatsData.CurrentGameAchievementsUnlocked = 23; // Simulated
                gameStatsData.CurrentGameAchievementPercentage = Math.Round(
                    (double)gameStatsData.CurrentGameAchievementsUnlocked / gameStatsData.CurrentGameAchievementsTotal * 100, 1);
                
                gameStatsData.LatestAchievementName = "Master Player"; // Simulated
                gameStatsData.LatestAchievementDate = DateTime.Now.AddDays(-2); // Simulated
                gameStatsData.LatestAchievementDescription = "Reached level 50 in the game"; // Simulated
                
                await Task.CompletedTask; // Prevent async warning
                _logger?.LogInfo($"[GameStatsService] Current game achievements: {gameStatsData.CurrentGameAchievementsUnlocked}/{gameStatsData.CurrentGameAchievementsTotal} ({gameStatsData.CurrentGameAchievementPercentage}%)");
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting current game achievements", ex);
                SetDefaultAchievementValues(gameStatsData);
            }
        }

        /// <summary>
        /// Collects overall achievement statistics across all games
        /// </summary>
        private async Task CollectOverallAchievementStatsAsync(GameStatsData gameStatsData)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Collecting overall achievement statistics...");
                
                // Placeholder for overall achievement statistics
                // Real implementation would aggregate achievement data across all games
                
                gameStatsData.TotalAchievements = 1247; // Simulated
                gameStatsData.PerfectGames = 8; // Simulated (games with 100% achievements)
                gameStatsData.AverageGameCompletion = 67.3; // Simulated percentage
                gameStatsData.RareAchievementsUnlocked = 15; // Simulated
                
                await Task.CompletedTask; // Prevent async warning
                _logger?.LogInfo($"[GameStatsService] Overall achievements: {gameStatsData.TotalAchievements} total, {gameStatsData.PerfectGames} perfect games");
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting overall achievement stats", ex);
                gameStatsData.TotalAchievements = 0;
                gameStatsData.PerfectGames = 0;
                gameStatsData.AverageGameCompletion = 0;
                gameStatsData.RareAchievementsUnlocked = 0;
            }
        }

        /// <summary>
        /// Collects game news and updates
        /// </summary>
        private async Task CollectGameNewsAsync(GameStatsData gameStatsData, int appId)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Collecting game news...");
                
                // Placeholder for game news collection
                // Real implementation would call Steam news API
                
                if (appId > 0)
                {
                    gameStatsData.LatestNewsTitle = "Major Update Released"; // Simulated
                    gameStatsData.LatestNewsDate = DateTime.Now.AddDays(-1); // Simulated
                    gameStatsData.LatestNewsUrl = "https://store.steampowered.com/news"; // Simulated
                }
                else
                {
                    gameStatsData.LatestNewsTitle = null;
                    gameStatsData.LatestNewsDate = null;
                    gameStatsData.LatestNewsUrl = null;
                }
                
                await Task.CompletedTask; // Prevent async warning
                _logger?.LogDebug($"[GameStatsService] Game news collected: {gameStatsData.LatestNewsTitle ?? "No news"}");
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting game news", ex);
                gameStatsData.LatestNewsTitle = "Error loading news";
                gameStatsData.LatestNewsDate = null;
                gameStatsData.LatestNewsUrl = null;
            }
        }

        /// <summary>
        /// Collects advanced gaming metrics
        /// </summary>
        private async Task CollectAdvancedGamingMetricsAsync(GameStatsData gameStatsData)
        {
            try
            {
                _logger?.LogDebug("[GameStatsService] Collecting advanced gaming metrics...");
                
                // Placeholder for advanced metrics
                // Real implementation would calculate complex statistics
                
                gameStatsData.GlobalPlaytimePercentile = 78.5; // Simulated - player's percentile ranking
                gameStatsData.GlobalUserCategory = "Enthusiast"; // Simulated category
                gameStatsData.GamingStreakDays = 12; // Simulated consecutive gaming days
                gameStatsData.MonthlyPlaytimeHours = 45.7; // Simulated monthly hours
                
                await Task.CompletedTask; // Prevent async warning
                _logger?.LogInfo($"[GameStatsService] Advanced metrics: {gameStatsData.GlobalPlaytimePercentile}% percentile, {gameStatsData.GlobalUserCategory} category");
            }
            catch (Exception ex)
            {
                _logger?.LogError("[GameStatsService] Error collecting advanced gaming metrics", ex);
                gameStatsData.GlobalPlaytimePercentile = 0;
                gameStatsData.GlobalUserCategory = "Unknown";
                gameStatsData.GamingStreakDays = 0;
                gameStatsData.MonthlyPlaytimeHours = 0;
            }
        }

        /// <summary>
        /// Sets default values for achievement data when collection fails
        /// </summary>
        private void SetDefaultAchievementValues(GameStatsData gameStatsData)
        {
            gameStatsData.CurrentGameName = null;
            gameStatsData.CurrentGameAppId = 0;
            gameStatsData.CurrentGameAchievementsTotal = 0;
            gameStatsData.CurrentGameAchievementsUnlocked = 0;
            gameStatsData.CurrentGameAchievementPercentage = 0;
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
            if (HasError) return "Unknown";
            if (CurrentGameAchievementPercentage >= 100) return "Perfect";
            if (CurrentGameAchievementPercentage >= 90) return "Nearly Perfect";
            if (CurrentGameAchievementPercentage >= 75) return "Excellent";
            if (CurrentGameAchievementPercentage >= 50) return "Good";
            if (CurrentGameAchievementPercentage >= 25) return "Fair";
            if (CurrentGameAchievementPercentage > 0) return "Started";
            return "None";
        }
        
        /// <summary>
        /// Gets overall achievement hunter level
        /// </summary>
        public string GetAchievementHunterLevel()
        {
            if (HasError) return "Unknown";
            if (PerfectGames >= 20) return "Legend";
            if (PerfectGames >= 10) return "Master";
            if (PerfectGames >= 5) return "Expert";
            if (PerfectGames >= 2) return "Enthusiast";
            if (TotalAchievements >= 100) return "Hunter";
            return "Beginner";
        }
        
        /// <summary>
        /// Gets gaming dedication level based on streak and monthly hours
        /// </summary>
        public string GetGamingDedicationLevel()
        {
            if (HasError) return "Unknown";
            if (GamingStreakDays >= 30 && MonthlyPlaytimeHours >= 80) return "Hardcore";
            if (GamingStreakDays >= 14 && MonthlyPlaytimeHours >= 40) return "Dedicated";
            if (GamingStreakDays >= 7 || MonthlyPlaytimeHours >= 20) return "Regular";
            if (MonthlyPlaytimeHours >= 5) return "Casual";
            return "Occasional";
        }
        
        #endregion
    }
}