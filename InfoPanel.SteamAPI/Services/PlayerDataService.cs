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
        private readonly EnhancedLoggingService? _enhancedLogger;
        private readonly SteamApiService _steamApiService;
        private readonly SessionTrackingService? _sessionTracker;
        
        #endregion

        #region Constructor
        
        public PlayerDataService(
            ConfigurationService configService, 
            SteamApiService steamApiService,
            SessionTrackingService? sessionTracker = null,
            FileLoggingService? logger = null,
            EnhancedLoggingService? enhancedLogger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _steamApiService = steamApiService ?? throw new ArgumentNullException(nameof(steamApiService));
            _sessionTracker = sessionTracker;
            _logger = logger;
            _enhancedLogger = enhancedLogger;
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
                // Enhanced logging for player data collection start
                if (_enhancedLogger != null)
                {
                    _enhancedLogger.LogDebug("PLAYER", "Starting player data collection");
                }
                else
                {
                    _enhancedLogger?.LogDebug("PlayerDataService", "Starting player data collection", new
                    {
                        Timestamp = DateTime.Now
                    });
                }
                
                var playerData = new PlayerData();

                // 1. Get basic player summary (online status, current game)
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
                    
                    // Enhanced logging for avatar data with delta detection 
                    if (_enhancedLogger != null)
                    {
                        _enhancedLogger.LogDebug("PLAYER", "Avatar data updated", new
                        {
                            PlayerName = playerData.PlayerName,
                            OnlineState = playerData.OnlineState,
                            ProfileImageUrl = playerData.ProfileImageUrl,
                            HasFullAvatar = !string.IsNullOrEmpty(player.AvatarFull)
                        });
                    }
                    else
                    {
                        // Fallback to enhanced logging
                        _enhancedLogger?.LogDebug("PlayerDataService", "Avatar URLs retrieved", new
                        {
                            HasSmallAvatar = !string.IsNullOrEmpty(player.Avatar),
                            HasMediumAvatar = !string.IsNullOrEmpty(player.AvatarMedium),
                            HasFullAvatar = !string.IsNullOrEmpty(player.AvatarFull),
                            ProfileImageUrl = playerData.ProfileImageUrl
                        });
                    }
                    
                    // Get Steam Level (separate API call)
                    try
                    {
                        var levelResponse = await _steamApiService.GetSteamLevelAsync();
                        playerData.SteamLevel = levelResponse?.Response?.PlayerLevel ?? 0;
                        _enhancedLogger?.LogDebug("PlayerDataService", "Steam level retrieved", new
                        {
                            SteamLevel = playerData.SteamLevel
                        });
                    }
                    catch (Exception levelEx)
                    {
                        _enhancedLogger?.LogWarning("PlayerDataService", "Could not fetch Steam level", new
                        {
                            ErrorMessage = levelEx.Message
                        });
                        playerData.SteamLevel = 0; // Default to 0 if unable to fetch
                    }
                    
                    // Current game state (CRITICAL for responsiveness) - Enhanced logging with delta detection
                    if (_enhancedLogger != null)
                    {
                        _enhancedLogger.LogDebug("PLAYER", "Game state check", new
                        {
                            GameExtraInfo = player.GameExtraInfo,
                            GameId = player.GameId,
                            GameServerIp = player.GameServerIp,
                            HasGameData = !string.IsNullOrEmpty(player.GameExtraInfo)
                        });
                    }
                    else
                    {
                        _enhancedLogger?.LogDebug("PlayerDataService", "Game state check", new
                        {
                            GameExtraInfo = player.GameExtraInfo,
                            GameId = player.GameId,
                            GameServerIp = player.GameServerIp,
                            HasGameData = !string.IsNullOrEmpty(player.GameExtraInfo)
                        });
                        Console.WriteLine($"[PlayerDataService] Raw Steam API game fields - GameExtraInfo: '{player.GameExtraInfo}', GameId: '{player.GameId}', GameServerIp: '{player.GameServerIp}'");
                    }
                    
                    if (!string.IsNullOrEmpty(player.GameExtraInfo))
                    {
                        playerData.CurrentGameName = player.GameExtraInfo;
                        playerData.CurrentGameServerIp = player.GameServerIp;
                        playerData.CurrentGameExtraInfo = player.GameExtraInfo;
                        
                        // Enhanced logging for game detection
                        if (_enhancedLogger != null)
                        {
                            _enhancedLogger.LogInfo("PLAYER", "Game detected", new
                            {
                                GameName = playerData.CurrentGameName,
                                GameId = player.GameId,
                                HasServerIp = !string.IsNullOrEmpty(playerData.CurrentGameServerIp)
                            });
                        }
                        else
                        {
                            Console.WriteLine($"[PlayerDataService] GAME DETECTED! Setting CurrentGameName='{playerData.CurrentGameName}'");
                        }
                        
                        // Parse GameId as int if possible
                        if (int.TryParse(player.GameId, out int gameId))
                        {
                            playerData.CurrentGameAppId = gameId;
                            
                            // Enhanced logging for app ID
                            if (_enhancedLogger != null)
                            {
                                _enhancedLogger.LogInfo("PLAYER", "Game app ID set", new
                                {
                                    GameName = playerData.CurrentGameName,
                                    AppId = playerData.CurrentGameAppId
                                });
                            }
                            else
                            {
                                Console.WriteLine($"[PlayerDataService] GAME APP ID SET! CurrentGameAppId={playerData.CurrentGameAppId}");
                            }
                            
                            // Get game banner URL
                            try
                            {
                                playerData.CurrentGameBannerUrl = await GetGameBannerUrlAsync(gameId);
                                
                                // Enhanced logging for banner with delta detection
                                if (_enhancedLogger != null)
                                {
                                    _enhancedLogger.LogDebug("PLAYER", "Game banner updated", new
                                    {
                                        GameName = playerData.CurrentGameName,
                                        AppId = gameId,
                                        BannerUrl = playerData.CurrentGameBannerUrl,
                                        HasBanner = !string.IsNullOrEmpty(playerData.CurrentGameBannerUrl)
                                    });
                                }
                                else
                                {
                                    _enhancedLogger?.LogDebug("PlayerDataService", "Game banner URL set", new
                                    {
                                        BannerUrl = playerData.CurrentGameBannerUrl
                                    });
                                }
                            }
                            catch (Exception bannerEx)
                            {
                                if (_enhancedLogger != null)
                                {
                                    _enhancedLogger.LogWarning("PLAYER", "Failed to fetch game banner", new { AppId = gameId }, bannerEx);
                                }
                                else
                                {
                                    _enhancedLogger?.LogWarning("PlayerDataService", "Failed to fetch game banner", new 
                                    { 
                                        AppId = gameId,
                                        ErrorMessage = bannerEx.Message 
                                    });
                                }
                                playerData.CurrentGameBannerUrl = null;
                            }
                            
                            // Get current game total playtime from owned games
                            try
                            {
                                playerData.CurrentGameTotalPlaytimeHours = await GetGameTotalPlaytimeAsync(gameId);
                                
                                _enhancedLogger?.LogDebug("PLAYER", "Game total playtime fetched", new
                                {
                                    GameName = playerData.CurrentGameName,
                                    AppId = gameId,
                                    TotalPlaytimeHours = Math.Round(playerData.CurrentGameTotalPlaytimeHours, 1)
                                });
                            }
                            catch (Exception playtimeEx)
                            {
                                _enhancedLogger?.LogWarning("PLAYER", "Failed to fetch game playtime", new 
                                { 
                                    AppId = gameId,
                                    ErrorMessage = playtimeEx.Message 
                                });
                                playerData.CurrentGameTotalPlaytimeHours = 0;
                            }
                        }
                        else
                        {
                            _enhancedLogger?.LogDebug("PlayerDataService", "Could not parse GameId", new
                            {
                                GameId = player.GameId,
                                GameName = playerData.CurrentGameName
                            });
                        }
                        
                        _enhancedLogger?.LogInfo("PlayerDataService", "Player in game", new
                        {
                            GameName = playerData.CurrentGameName,
                            AppId = playerData.CurrentGameAppId
                        });
                    }
                    else
                    {
                        // CRITICAL FIX: Steam API's GameExtraInfo is the SINGLE SOURCE OF TRUTH
                        // When empty, the player is definitively NOT in a game
                        // We MUST clear game fields to break the circular dependency that kept sessions alive forever
                        
                        // Always clear game state when Steam says no game
                        playerData.CurrentGameName = null;
                        playerData.CurrentGameAppId = SteamConstants.INVALID_GAME_APP_ID;
                        playerData.CurrentGameServerIp = null;
                        playerData.CurrentGameExtraInfo = null;
                        playerData.CurrentGameBannerUrl = null;
                        playerData.CurrentGameTotalPlaytimeHours = 0;
                        
                        _enhancedLogger?.LogInfo("PLAYER", "Steam API reports no game - clearing game state", new
                        {
                            PlayerName = playerData.PlayerName,
                            OnlineState = playerData.OnlineState,
                            PreviousGameCleared = true,
                            Reason = "GameExtraInfo is empty - definitive not in game"
                        });
                        
                        // AFTER clearing (important!), check if we should display last played game
                        // This is ONLY for visual display - NOT used for session tracking
                        if (_sessionTracker != null)
                        {
                            var lastPlayed = _sessionTracker.GetLastPlayedGame();
                            if (!string.IsNullOrEmpty(lastPlayed.gameName) && !string.IsNullOrEmpty(lastPlayed.bannerUrl))
                            {
                                // Set ONLY name and banner for display - DO NOT set AppId (breaks session detection)
                                playerData.CurrentGameName = lastPlayed.gameName;
                                playerData.CurrentGameBannerUrl = lastPlayed.bannerUrl;
                                // Explicitly do NOT set CurrentGameAppId - keep it as INVALID_GAME_APP_ID
                                // This ensures SessionTrackingService knows we're not in a game
                                
                                // ALSO populate LastPlayed* properties for sensors to use
                                playerData.LastPlayedGameName = lastPlayed.gameName;
                                playerData.LastPlayedGameAppId = lastPlayed.appId;
                                playerData.LastPlayedGameBannerUrl = lastPlayed.bannerUrl;
                                playerData.LastPlayedGameTimestamp = lastPlayed.timestamp;
                                
                                _enhancedLogger?.LogInfo("PLAYER", "Showing last played game for display only", new
                                {
                                    GameName = lastPlayed.gameName,
                                    AppId = lastPlayed.appId,
                                    BannerUrl = lastPlayed.bannerUrl?.Substring(0, Math.Min(50, lastPlayed.bannerUrl.Length)) + "...",
                                    LastPlayed = lastPlayed.timestamp?.ToString("yyyy-MM-dd HH:mm:ss"),
                                    Note = "Display only - CurrentGameAppId remains INVALID to signal no active game"
                                });
                            }
                            else
                            {
                                _enhancedLogger?.LogDebug("PLAYER", "No last played game to display", new
                                {
                                    PlayerName = playerData.PlayerName,
                                    HasSessionTracker = true,
                                    LastPlayedAvailable = false
                                });
                            }
                        }
                    }
                    
                    // Enhanced logging for player data collection summary
                    if (_enhancedLogger != null)
                    {
                        _enhancedLogger.LogInfo("PLAYER", "Player data collected", new
                        {
                            PlayerName = playerData.PlayerName,
                            IsOnline = playerData.IsOnline(),
                            CurrentGame = playerData.CurrentGameName ?? "None",
                            SteamLevel = playerData.SteamLevel,
                            HasError = playerData.HasError
                        });
                    }
                    else
                    {
                        _enhancedLogger?.LogInfo("PlayerDataService", "Player data collected", new
                        {
                            PlayerName = playerData.PlayerName,
                            IsOnline = playerData.IsOnline(),
                            CurrentGame = playerData.CurrentGameName ?? "None",
                            SteamLevel = playerData.SteamLevel,
                            HasError = playerData.HasError
                        });
                    }
                }
                else
                {
                    if (_enhancedLogger != null)
                    {
                        _enhancedLogger.LogWarning("PLAYER", "Player summary returned null or empty");
                    }
                    else
                    {
                        _enhancedLogger?.LogWarning("PlayerDataService", "Player summary returned null or empty", new
                        {
                            ApiResponse = "null or empty"
                        });
                    }
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
                    
                    // Get average session time from recent sessions
                    var recentStats = _sessionTracker.GetRecentSessionStats(daysBack: 30);  // Last 30 days
                    playerData.AverageSessionTimeMinutes = recentStats.averageMinutes;
                    
                    // CRITICAL: Log session info retrieval with full details
                    _enhancedLogger?.LogInfo("PlayerDataService.SessionTracking", "Retrieved session info", new
                    {
                        IsActive = sessionInfo.isActive,
                        SessionMinutes = sessionInfo.sessionMinutes,
                        SessionStart = sessionInfo.sessionStart?.ToString("HH:mm:ss"),
                        AverageSessionMinutes = Math.Round(recentStats.averageMinutes, 1),
                        RecentSessionCount = recentStats.sessionCount,
                        TotalHours = Math.Round(recentStats.totalHours, 2),
                        HasSessionTracker = _sessionTracker != null,
                        PlayerDataAvgSet = Math.Round(playerData.AverageSessionTimeMinutes, 1)
                    });
                    
                    if (sessionInfo.isActive && sessionInfo.sessionStart.HasValue)
                    {
                        _enhancedLogger?.LogDebug("PlayerDataService", "Active gaming session", new
                        {
                            SessionMinutes = sessionInfo.sessionMinutes,
                            SessionStart = sessionInfo.sessionStart.Value.ToString("HH:mm:ss"),
                            AverageSessionMinutes = Math.Round(recentStats.averageMinutes, 1)
                        });
                    }
                    else
                    {
                        _enhancedLogger?.LogDebug("PlayerDataService", "No active gaming session", new
                        {
                            HasTracker = _sessionTracker != null,
                            AverageWillBe = Math.Round(recentStats.averageMinutes, 1)
                        });
                    }
                }
                else
                {
                    // Set basic session defaults when no tracker available
                    playerData.CurrentSessionTimeMinutes = 0;
                    playerData.TodayPlaytimeHours = 0;
                    playerData.CurrentSessionStartTime = null;
                    playerData.AverageSessionTimeMinutes = 0;
                }

                // 3. Set status based on collected data
                playerData.Status = playerData.IsOnline() ? "Online" : "Offline";
                playerData.Timestamp = DateTime.Now;
                
                _enhancedLogger?.LogDebug("PlayerDataService", "Player data collection completed", new
                {
                    PlayerName = playerData.PlayerName,
                    Status = playerData.Status,
                    HasError = playerData.HasError
                });
                return playerData;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("PlayerDataService", "Error collecting player data", ex);
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
        /// Gets the game library hero image URL from Steam CDN
        /// Uses the high-quality library_hero.jpg (3840x1240) instead of header banner
        /// </summary>
        private async Task<string?> GetGameBannerUrlAsync(int appId)
        {
            try
            {
                // Use CDN pattern for library_hero image (3840x1240 - high quality)
                // Primary CDN: CloudFlare
                var libraryHeroUrl = $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{appId}/library_hero.jpg";
                
                _enhancedLogger?.LogDebug("PlayerDataService.GetGameBannerUrlAsync", "Using library_hero image from CDN", new
                {
                    AppId = appId,
                    LibraryHeroUrl = libraryHeroUrl,
                    ImageSize = "3840x1240"
                });
                
                // Verify the image exists with a HEAD request (fast check)
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                try
                {
                    var headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, libraryHeroUrl));
                    if (headResponse.IsSuccessStatusCode)
                    {
                        _enhancedLogger?.LogDebug("PlayerDataService.GetGameBannerUrlAsync", "Library hero image verified", new
                        {
                            AppId = appId,
                            StatusCode = (int)headResponse.StatusCode
                        });
                        return libraryHeroUrl;
                    }
                }
                catch
                {
                    // HEAD request failed, but image might still exist - return URL anyway
                    // Steam CDN is 99% reliable for published games
                }
                
                _enhancedLogger?.LogDebug("PlayerDataService.GetGameBannerUrlAsync", "Using library_hero URL (verification skipped)", new
                {
                    AppId = appId,
                    Note = "CDN image URL returned without verification"
                });
                
                // Return the URL even if HEAD request failed - Steam CDN is highly reliable
                return libraryHeroUrl;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogWarning("PlayerDataService.GetGameBannerUrlAsync", "Error constructing library hero URL", new
                {
                    AppId = appId,
                    ErrorMessage = ex.Message
                });
                return null;
            }
        }
        
        /// <summary>
        /// Gets the total playtime for a specific game from GetOwnedGames API
        /// </summary>
        private async Task<double> GetGameTotalPlaytimeAsync(int appId)
        {
            try
            {
                _enhancedLogger?.LogDebug("PlayerDataService.GetGameTotalPlaytimeAsync", "Fetching game playtime", new
                {
                    AppId = appId
                });
                
                // Call GetOwnedGames API to get playtime for this specific game
                var ownedGamesResponse = await _steamApiService.GetOwnedGamesAsync();
                
                if (ownedGamesResponse?.Response?.Games != null)
                {
                    // Find the game by AppId
                    var game = ownedGamesResponse.Response.Games.FirstOrDefault(g => g.AppId == appId);
                    
                    if (game != null)
                    {
                        // PlaytimeForever is in minutes, convert to hours
                        var playtimeHours = game.PlaytimeForever / 60.0;
                        
                        _enhancedLogger?.LogInfo("PlayerDataService.GetGameTotalPlaytimeAsync", "Game playtime retrieved", new
                        {
                            AppId = appId,
                            GameName = game.Name,
                            PlaytimeMinutes = game.PlaytimeForever,
                            PlaytimeHours = Math.Round(playtimeHours, 1)
                        });
                        
                        return playtimeHours;
                    }
                    else
                    {
                        _enhancedLogger?.LogWarning("PlayerDataService.GetGameTotalPlaytimeAsync", "Game not found in owned games", new
                        {
                            AppId = appId,
                            TotalGamesOwned = ownedGamesResponse.Response.Games.Count
                        });
                        return 0;
                    }
                }
                
                _enhancedLogger?.LogWarning("PlayerDataService.GetGameTotalPlaytimeAsync", "No owned games data returned from API");
                return 0;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("PlayerDataService.GetGameTotalPlaytimeAsync", "Error fetching game playtime", ex, new
                {
                    AppId = appId
                });
                return 0;
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
        public double CurrentGameTotalPlaytimeHours { get; set; }  // Total playtime for current game from Steam API
        
        #endregion

        #region Session Tracking Properties
        
        public double CurrentSessionTimeMinutes { get; set; }
        public double TodayPlaytimeHours { get; set; }
        public DateTime? CurrentSessionStartTime { get; set; }
        public double AverageSessionTimeMinutes { get; set; }
        
        #endregion

        #region Last Played Game Properties
        
        // These properties hold the last played game information for display purposes
        // when the player is not currently in a game
        public string? LastPlayedGameName { get; set; }
        public int LastPlayedGameAppId { get; set; }
        public string? LastPlayedGameBannerUrl { get; set; }
        public DateTime? LastPlayedGameTimestamp { get; set; }
        
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