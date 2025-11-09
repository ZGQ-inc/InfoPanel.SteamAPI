using InfoPanel.SteamAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Service responsible for collecting critical player data that needs real-time updates
    /// Handles player profile, online status, current game state, and session tracking
    /// Optimized for fast updates (5-second intervals)
    /// </summary>
    public class PlayerDataService
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly SteamApiService _steamApiService;
        private readonly SessionTrackingService? _sessionTracker;
        
        #endregion

        #region Constructor
        
        public PlayerDataService(
            ConfigurationService configService, 
            SteamApiService steamApiService,
            SessionTrackingService? sessionTracker = null,
            FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _steamApiService = steamApiService ?? throw new ArgumentNullException(nameof(steamApiService));
            _sessionTracker = sessionTracker;
            _logger = logger;
        }
        
        #endregion

        #region Player Data Collection

        /// <summary>
        /// Collects critical real-time player data (fast tier - 5s interval)
        /// Player profile, online status, current game state, session tracking
        /// </summary>
        public async Task<PlayerData> CollectPlayerDataAsync()
        {
            try
            {
                _logger?.LogDebug("[PlayerDataService] Starting player data collection...");
                var playerData = new PlayerData();

                // 1. Get basic player summary (online status, current game)
                _logger?.LogDebug("[PlayerDataService] Collecting player summary...");
                var playerSummary = await _steamApiService.GetPlayerSummaryAsync();
                
                if (playerSummary?.Response?.Players?.Any() == true)
                {
                    var player = playerSummary.Response.Players.First();
                    
                    // Core player profile data
                    playerData.PlayerName = player.PersonaName ?? "Unknown";
                    playerData.ProfileUrl = player.ProfileUrl;
                    playerData.AvatarUrl = player.AvatarMedium ?? player.Avatar;
                    playerData.LastLogOff = player.LastLogoff;
                    playerData.OnlineState = MapPersonaStateToString(player.PersonaState);
                    
                    // Current game state (CRITICAL for responsiveness)
                    if (!string.IsNullOrEmpty(player.GameExtraInfo))
                    {
                        playerData.CurrentGameName = player.GameExtraInfo;
                        playerData.CurrentGameServerIp = player.GameServerIp;
                        playerData.CurrentGameExtraInfo = player.GameExtraInfo;
                        
                        // Parse GameId as int if possible
                        if (int.TryParse(player.GameId, out int gameId))
                        {
                            playerData.CurrentGameAppId = gameId;
                        }
                        
                        _logger?.LogInfo($"[PlayerDataService] Player in game: {playerData.CurrentGameName} (ID: {playerData.CurrentGameAppId})");
                    }
                    else
                    {
                        // Clear game state when not playing
                        playerData.CurrentGameName = null;
                        playerData.CurrentGameAppId = 0;
                        playerData.CurrentGameServerIp = null;
                        playerData.CurrentGameExtraInfo = null;
                        _logger?.LogDebug("[PlayerDataService] Player not in any game");
                    }
                    
                    _logger?.LogInfo($"[PlayerDataService] Player data collected - {playerData.PlayerName}, Online: {playerData.IsOnline()}, Game: {playerData.CurrentGameName ?? "None"}");
                }
                else
                {
                    _logger?.LogWarning("[PlayerDataService] Player summary returned null or empty");
                    playerData.HasError = true;
                    playerData.ErrorMessage = "No player data available";
                    return playerData;
                }

                // 2. Session tracking would be handled here (temporarily simplified)
                // TODO: Add session tracking integration after validating SteamData model compatibility
                if (_sessionTracker != null)
                {
                    _logger?.LogDebug("[PlayerDataService] Session tracking integration will be added in next iteration");
                }

                // Set basic session defaults for now
                playerData.CurrentSessionTimeMinutes = 0;
                playerData.TodayPlaytimeHours = 0;
                playerData.CurrentSessionStartTime = null;

                // 3. Set status based on collected data
                playerData.Status = playerData.IsOnline() ? "Online" : "Offline";
                playerData.Timestamp = DateTime.Now;
                
                _logger?.LogDebug("[PlayerDataService] Player data collection completed successfully");
                return playerData;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[PlayerDataService] Error collecting player data", ex);
                return new PlayerData
                {
                    HasError = true,
                    ErrorMessage = $"Player data error: {ex.Message}",
                    Status = "Error",
                    Timestamp = DateTime.Now
                };
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Maps Steam PersonaState integer to readable string
        /// </summary>
        private static string MapPersonaStateToString(int personaState)
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

        #endregion
    }

    /// <summary>
    /// Data model specifically for player-related information
    /// Contains core player profile and game state data
    /// </summary>
    public class PlayerData
    {
        #region Core Properties
        
        public string? Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        
        #endregion

        #region Player Profile Properties
        
        public string? PlayerName { get; set; }
        public string? ProfileUrl { get; set; }
        public string? AvatarUrl { get; set; }
        public string? OnlineState { get; set; }
        public long LastLogOff { get; set; }
        
        #endregion

        #region Current Game Properties
        
        public string? CurrentGameName { get; set; }
        public int CurrentGameAppId { get; set; }
        public string? CurrentGameExtraInfo { get; set; }
        public string? CurrentGameServerIp { get; set; }
        
        #endregion

        #region Session Tracking Properties
        
        public double CurrentSessionTimeMinutes { get; set; }
        public double TodayPlaytimeHours { get; set; }
        public DateTime? CurrentSessionStartTime { get; set; }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Checks if player is currently online
        /// </summary>
        public bool IsOnline()
        {
            return OnlineState != null && OnlineState != "Offline";
        }
        
        /// <summary>
        /// Checks if player is currently in a game
        /// </summary>
        public bool IsInGame()
        {
            return !string.IsNullOrEmpty(CurrentGameName) && CurrentGameAppId > 0;
        }
        
        #endregion
    }
}