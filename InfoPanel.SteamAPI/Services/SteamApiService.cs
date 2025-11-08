using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using InfoPanel.SteamAPI.Models;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Service for communicating with Steam Web API
    /// Handles authentication, rate limiting, and data retrieval
    /// </summary>
    public class SteamApiService : IDisposable
    {
        #region Fields and Properties
        
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _steamId;
        private readonly object _rateLimitLock = new object();
        private DateTime _lastApiCall = DateTime.MinValue;
        private const int MIN_REQUEST_INTERVAL_MS = 1000; // 1 second between requests
        private bool _disposed = false;
        
        /// <summary>
        /// Base URL for Steam Web API
        /// </summary>
        private const string STEAM_API_BASE_URL = "https://api.steampowered.com";
        
        #endregion

        #region Constructor and Initialization
        
        /// <summary>
        /// Initializes a new instance of the SteamApiService
        /// </summary>
        /// <param name="apiKey">Steam Web API key</param>
        /// <param name="steamId">Steam ID to monitor</param>
        public SteamApiService(string apiKey, string steamId)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            
            if (string.IsNullOrWhiteSpace(steamId))
                throw new ArgumentException("Steam ID cannot be null or empty", nameof(steamId));
            
            _apiKey = apiKey;
            _steamId = steamId;
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InfoPanel-SteamAPI/1.0");
        }
        
        #endregion

        #region Rate Limiting
        
        /// <summary>
        /// Ensures we don't exceed Steam API rate limits
        /// </summary>
        private async Task EnforceRateLimit()
        {
            lock (_rateLimitLock)
            {
                var timeSinceLastCall = DateTime.Now - _lastApiCall;
                var waitTime = MIN_REQUEST_INTERVAL_MS - (int)timeSinceLastCall.TotalMilliseconds;
                
                if (waitTime > 0)
                {
                    Task.Delay(waitTime).Wait();
                }
                
                _lastApiCall = DateTime.Now;
            }
        }
        
        #endregion

        #region API Endpoints
        
        /// <summary>
        /// Gets basic player information including name, avatar, and online status
        /// </summary>
        public async Task<PlayerSummary?> GetPlayerSummaryAsync()
        {
            try
            {
                await EnforceRateLimit();
                
                var url = $"{STEAM_API_BASE_URL}/ISteamUser/GetPlayerSummaries/v0002/?key={_apiKey}&steamids={_steamId}";
                var response = await _httpClient.GetStringAsync(url);
                
                
                using var document = JsonDocument.Parse(response);
                var players = document.RootElement.GetProperty("response").GetProperty("players");
                
                if (players.GetArrayLength() == 0)
                {
                    return null;
                }
                
                var player = players[0];
                return new PlayerSummary
                {
                    SteamId = player.GetProperty("steamid").GetString() ?? "",
                    PersonaName = player.GetProperty("personaname").GetString() ?? "",
                    ProfileUrl = player.GetProperty("profileurl").GetString() ?? "",
                    Avatar = player.GetProperty("avatar").GetString() ?? "",
                    PersonaState = player.GetProperty("personastate").GetInt32(),
                    PersonaStateFlags = player.TryGetProperty("personastateflags", out var flags) ? flags.GetInt32() : 0,
                    LastLogOff = player.TryGetProperty("lastlogoff", out var logoff) ? logoff.GetInt64() : 0,
                    GameExtraInfo = player.TryGetProperty("gameextrainfo", out var extraInfo) ? extraInfo.GetString() : null,
                    GameId = player.TryGetProperty("gameid", out var gameId) ? gameId.GetString() : null,
                    GameServerIp = player.TryGetProperty("gameserverip", out var serverIp) ? serverIp.GetString() : null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamApiService] Error getting player summary: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets the list of games owned by the player with playtime information
        /// </summary>
        public async Task<List<OwnedGame>> GetOwnedGamesAsync()
        {
            try
            {
                await EnforceRateLimit();
                
                var url = $"{STEAM_API_BASE_URL}/IPlayerService/GetOwnedGames/v0001/?key={_apiKey}&steamid={_steamId}&format=json&include_appinfo=true&include_played_free_games=true";
                var response = await _httpClient.GetStringAsync(url);
                
                
                using var document = JsonDocument.Parse(response);
                var responseElement = document.RootElement.GetProperty("response");
                
                if (!responseElement.TryGetProperty("games", out var gamesElement))
                {
                    return new List<OwnedGame>();
                }
                
                var games = new List<OwnedGame>();
                foreach (var game in gamesElement.EnumerateArray())
                {
                    games.Add(new OwnedGame
                    {
                        AppId = game.GetProperty("appid").GetInt32(),
                        Name = game.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        PlaytimeForever = game.TryGetProperty("playtime_forever", out var forever) ? forever.GetInt32() : 0,
                        PlaytimeWindows = game.TryGetProperty("playtime_windows_forever", out var windows) ? windows.GetInt32() : 0,
                        PlaytimeMac = game.TryGetProperty("playtime_mac_forever", out var mac) ? mac.GetInt32() : 0,
                        PlaytimeLinux = game.TryGetProperty("playtime_linux_forever", out var linux) ? linux.GetInt32() : 0,
                        RtimeLastPlayed = game.TryGetProperty("rtime_last_played", out var lastPlayed) ? lastPlayed.GetInt64() : 0,
                        PlaytimeDisconnected = game.TryGetProperty("playtime_disconnected", out var disconnected) ? disconnected.GetInt32() : 0
                    });
                }
                
                return games;
            }
            catch (Exception ex)
            {
                // Keep error logging for debugging critical issues
                Console.WriteLine($"[SteamApiService] Error getting owned games: {ex.Message}");
                return new List<OwnedGame>();
            }
        }
        
        /// <summary>
        /// Gets recently played games (last 2 weeks)
        /// </summary>
        public async Task<List<RecentlyPlayedGame>> GetRecentlyPlayedGamesAsync()
        {
            try
            {
                await EnforceRateLimit();
                
                var url = $"{STEAM_API_BASE_URL}/IPlayerService/GetRecentlyPlayedGames/v0001/?key={_apiKey}&steamid={_steamId}&format=json";
                var response = await _httpClient.GetStringAsync(url);
                
                
                using var document = JsonDocument.Parse(response);
                var responseElement = document.RootElement.GetProperty("response");
                
                if (!responseElement.TryGetProperty("games", out var gamesElement))
                {
                    return new List<RecentlyPlayedGame>();
                }
                
                var games = new List<RecentlyPlayedGame>();
                foreach (var game in gamesElement.EnumerateArray())
                {
                    games.Add(new RecentlyPlayedGame
                    {
                        AppId = game.GetProperty("appid").GetInt32(),
                        Name = game.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        Playtime2Weeks = game.TryGetProperty("playtime_2weeks", out var twoWeeks) ? twoWeeks.GetInt32() : 0,
                        PlaytimeForever = game.TryGetProperty("playtime_forever", out var forever) ? forever.GetInt32() : 0,
                        ImgIconUrl = game.TryGetProperty("img_icon_url", out var icon) ? icon.GetString() ?? "" : "",
                        ImgLogoUrl = game.TryGetProperty("img_logo_url", out var logo) ? logo.GetString() ?? "" : ""
                    });
                }
                
                return games;
            }
            catch (Exception ex)
            {
                // Keep error logging for debugging critical issues
                Console.WriteLine($"[SteamApiService] Error getting recently played games: {ex.Message}");
                return new List<RecentlyPlayedGame>();
            }
        }
        
        /// <summary>
        /// Gets player level information
        /// </summary>
        public async Task<int> GetSteamLevelAsync()
        {
            try
            {
                await EnforceRateLimit();
                
                var url = $"{STEAM_API_BASE_URL}/IPlayerService/GetSteamLevel/v1/?key={_apiKey}&steamid={_steamId}";
                var response = await _httpClient.GetStringAsync(url);
                
                
                using var document = JsonDocument.Parse(response);
                var responseElement = document.RootElement.GetProperty("response");
                
                return responseElement.GetProperty("player_level").GetInt32();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamApiService] Error getting Steam level: {ex.Message}");
                return 0;
            }
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Converts persona state integer to readable string
        /// </summary>
        public static string GetPersonaStateString(int personaState)
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
        /// Tests if the API key and Steam ID are valid
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var summary = await GetPlayerSummaryAsync();
                return summary != null && !string.IsNullOrEmpty(summary.PersonaName);
            }
            catch
            {
                return false;
            }
        }
        
        #endregion

        #region IDisposable Implementation
        
        /// <summary>
        /// Disposes of the HTTP client and other resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
        
        #endregion
    }

    #region Data Transfer Objects
    
    /// <summary>
    /// Represents a player summary from Steam API
    /// </summary>
    public class PlayerSummary
    {
        public string SteamId { get; set; } = "";
        public string PersonaName { get; set; } = "";
        public string ProfileUrl { get; set; } = "";
        public string Avatar { get; set; } = "";
        public int PersonaState { get; set; }
        public int PersonaStateFlags { get; set; }
        public long LastLogOff { get; set; }
        public string? GameExtraInfo { get; set; }
        public string? GameId { get; set; }
        public string? GameServerIp { get; set; }
    }
    
    /// <summary>
    /// Represents an owned game from Steam API
    /// </summary>
    public class OwnedGame
    {
        public int AppId { get; set; }
        public string Name { get; set; } = "";
        public int PlaytimeForever { get; set; } // in minutes
        public int PlaytimeWindows { get; set; } // in minutes
        public int PlaytimeMac { get; set; } // in minutes
        public int PlaytimeLinux { get; set; } // in minutes
        public long RtimeLastPlayed { get; set; } // Unix timestamp
        public int PlaytimeDisconnected { get; set; } // in minutes
    }
    
    /// <summary>
    /// Represents a recently played game from Steam API
    /// </summary>
    public class RecentlyPlayedGame
    {
        public int AppId { get; set; }
        public string Name { get; set; } = "";
        public int Playtime2Weeks { get; set; } // in minutes
        public int PlaytimeForever { get; set; } // in minutes
        public string ImgIconUrl { get; set; } = "";
        public string ImgLogoUrl { get; set; } = "";
    }
    
    #endregion
}
