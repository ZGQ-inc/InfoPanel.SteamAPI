using InfoPanel.SteamAPI.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for Steam API and player data
    /// </summary>
    public static class SteamConstants
    {
        // Steam PersonaState constants
        public const int PERSONA_STATE_OFFLINE = 0;
        public const int PERSONA_STATE_ONLINE = 1;
        public const int PERSONA_STATE_BUSY = 2;
        public const int PERSONA_STATE_AWAY = 3;
        public const int PERSONA_STATE_SNOOZE = 4;
        public const int PERSONA_STATE_LOOKING_TO_TRADE = 5;
        public const int PERSONA_STATE_LOOKING_TO_PLAY = 6;
        
        // Game state constants
        public const int INVALID_GAME_APP_ID = 0;
    }

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
                    playerData.ProfileImageUrl = player.AvatarFull ?? player.AvatarMedium ?? player.Avatar;
                    playerData.LastLogOff = player.LastLogoff;
                    playerData.OnlineState = MapPersonaStateToString(player.PersonaState);
                    
                    // Debug logging for avatar URLs
                    _logger?.LogDebug($"[PlayerDataService] Avatar URLs - Small: {player.Avatar}, Medium: {player.AvatarMedium}, Full: {player.AvatarFull}");
                    _logger?.LogDebug($"[PlayerDataService] Set ProfileImageUrl to: {playerData.ProfileImageUrl}");
                    
                    // Get Steam Level (separate API call)
                    try
                    {
                        var levelResponse = await _steamApiService.GetSteamLevelAsync();
                        playerData.SteamLevel = levelResponse?.Response?.PlayerLevel ?? 0;
                        _logger?.LogDebug($"[PlayerDataService] Steam Level: {playerData.SteamLevel}");
                    }
                    catch (Exception levelEx)
                    {
                        _logger?.LogWarning($"[PlayerDataService] Could not fetch Steam Level: {levelEx.Message}");
                        playerData.SteamLevel = 0; // Default to 0 if unable to fetch
                    }
                    
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
                            
                            // Get game banner URL
                            try
                            {
                                playerData.CurrentGameBannerUrl = await GetGameBannerUrlAsync(gameId);
                                _logger?.LogDebug($"[PlayerDataService] Set CurrentGameBannerUrl to: {playerData.CurrentGameBannerUrl}");
                            }
                            catch (Exception bannerEx)
                            {
                                _logger?.LogWarning($"[PlayerDataService] Could not fetch game banner for {gameId}: {bannerEx.Message}");
                                playerData.CurrentGameBannerUrl = null;
                            }
                        }
                        else
                        {
                            _logger?.LogDebug($"[PlayerDataService] Could not parse GameId: {player.GameId}");
                        }
                        
                        _logger?.LogInfo($"[PlayerDataService] Player in game: {playerData.CurrentGameName} (ID: {playerData.CurrentGameAppId})");
                    }
                    else
                    {
                        // Clear game state when not playing
                        playerData.CurrentGameName = null;
                        playerData.CurrentGameAppId = SteamConstants.INVALID_GAME_APP_ID;
                        playerData.CurrentGameServerIp = null;
                        playerData.CurrentGameExtraInfo = null;
                        playerData.CurrentGameBannerUrl = null;
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

                // 2. Session tracking integration
                if (_sessionTracker != null)
                {
                    var sessionInfo = _sessionTracker.GetCurrentSessionInfo();
                    playerData.CurrentSessionTimeMinutes = sessionInfo.sessionMinutes;
                    playerData.CurrentSessionStartTime = sessionInfo.sessionStart;
                    
                    if (sessionInfo.isActive && sessionInfo.sessionStart.HasValue)
                    {
                        _logger?.LogDebug($"[PlayerDataService] Active session: {sessionInfo.sessionMinutes} minutes (started: {sessionInfo.sessionStart.Value:HH:mm:ss})");
                    }
                    else
                    {
                        _logger?.LogDebug("[PlayerDataService] No active gaming session");
                    }
                }
                else
                {
                    // Set basic session defaults when no tracker available
                    playerData.CurrentSessionTimeMinutes = 0;
                    playerData.TodayPlaytimeHours = 0;
                    playerData.CurrentSessionStartTime = null;
                }

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
                SteamConstants.PERSONA_STATE_OFFLINE => "Offline",
                SteamConstants.PERSONA_STATE_ONLINE => "Online", 
                SteamConstants.PERSONA_STATE_BUSY => "Busy",
                SteamConstants.PERSONA_STATE_AWAY => "Away",
                SteamConstants.PERSONA_STATE_SNOOZE => "Snooze",
                SteamConstants.PERSONA_STATE_LOOKING_TO_TRADE => "Looking to trade",
                SteamConstants.PERSONA_STATE_LOOKING_TO_PLAY => "Looking to play",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Gets the game banner image URL from Steam Store API
        /// </summary>
        private async Task<string?> GetGameBannerUrlAsync(int appId)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&filters=basic";
                _logger?.LogDebug($"[PlayerDataService] Fetching game banner for app {appId}");
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await httpClient.GetStringAsync(url);
                
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;
                
                if (root.TryGetProperty(appId.ToString(), out var gameElement) &&
                    gameElement.TryGetProperty("success", out var success) && 
                    success.GetBoolean() &&
                    gameElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("header_image", out var headerImage))
                {
                    var bannerUrl = headerImage.GetString();
                    _logger?.LogDebug($"[PlayerDataService] Found game banner URL: {bannerUrl}");
                    return bannerUrl;
                }
                
                _logger?.LogDebug($"[PlayerDataService] No banner found for app {appId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"[PlayerDataService] Error fetching game banner for app {appId}: {ex.Message}");
                return null;
            }
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
        public string? ProfileImageUrl { get; set; }
        public string? OnlineState { get; set; }
        public long LastLogOff { get; set; }
        public int SteamLevel { get; set; }
        
        #endregion

        #region Current Game Properties
        
        public string? CurrentGameName { get; set; }
        public int CurrentGameAppId { get; set; }
        public string? CurrentGameExtraInfo { get; set; }
        public string? CurrentGameServerIp { get; set; }
        public string? CurrentGameBannerUrl { get; set; }
        
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
            return !string.IsNullOrEmpty(CurrentGameName) && CurrentGameAppId > SteamConstants.INVALID_GAME_APP_ID;
        }
        
        #endregion
    }
}