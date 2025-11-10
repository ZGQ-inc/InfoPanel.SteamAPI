using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using InfoPanel.SteamAPI.Models;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Real Steam Web API service for authentic data collection
    /// Implements comprehensive Steam API integration with proper error handling and rate limiting
    /// Based on official Steam Web API documentation from https://steamapi.xpaw.me/
    /// </summary>
    public class SteamApiService : IDisposable
    {
        #region Fields and Properties
        
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _steamId64;
        private readonly FileLoggingService? _logger;
        private readonly object _rateLimitLock = new object();
        private DateTime _lastApiCall = DateTime.MinValue;
        private const int MIN_REQUEST_INTERVAL_MS = 1100; // 1.1 seconds between requests for safety
        private bool _disposed = false;
        
        /// <summary>
        /// Base URL for Steam Web API
        /// </summary>
        private const string STEAM_API_BASE_URL = "https://api.steampowered.com";
        
        /// <summary>
        /// JSON serializer options for Steam API responses
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        
        #endregion

        #region Constructor and Initialization
        
        /// <summary>
        /// Initializes a new instance of the SteamApiService with real API integration
        /// </summary>
        /// <param name="apiKey">Steam Web API key from https://steamcommunity.com/dev/apikey</param>
        /// <param name="steamId64">64-bit Steam ID (17 digits starting with 7656119)</param>
        /// <param name="logger">Optional file logging service for detailed API logging</param>
        public SteamApiService(string apiKey, string steamId64, FileLoggingService? logger = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("your-steam-api-key"))
                throw new ArgumentException("Valid Steam Web API key is required", nameof(apiKey));
            
            if (string.IsNullOrWhiteSpace(steamId64) || steamId64.Contains("your-steam-id"))
                throw new ArgumentException("Valid Steam ID64 is required", nameof(steamId64));
            
            if (!IsValidSteamId64(steamId64))
                throw new ArgumentException($"Invalid SteamID64 format: {steamId64}. Must be 17 digits starting with 7656119", nameof(steamId64));
            
            _apiKey = apiKey;
            _steamId64 = steamId64;
            _logger = logger;
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InfoPanel-SteamAPI/1.0.0");
            
            _logger?.LogInfo($"SteamApiService initialized for Steam ID: {steamId64}");
        }
        
        /// <summary>
        /// Validates SteamID64 format
        /// </summary>
        private static bool IsValidSteamId64(string steamId64)
        {
            return steamId64.Length == 17 && 
                   steamId64.All(char.IsDigit) && 
                   steamId64.StartsWith("7656119");
        }
        
        #endregion

        #region Rate Limiting and Core API Methods
        
        /// <summary>
        /// Ensures we don't exceed Steam API rate limits with proper async handling
        /// </summary>
        private async Task EnforceRateLimitAsync()
        {
            DateTime now = DateTime.Now;
            DateTime nextAllowedCall;
            
            lock (_rateLimitLock)
            {
                var timeSinceLastCall = now - _lastApiCall;
                var waitTime = MIN_REQUEST_INTERVAL_MS - (int)timeSinceLastCall.TotalMilliseconds;
                
                if (waitTime > 0)
                {
                    nextAllowedCall = now.AddMilliseconds(waitTime);
                }
                else
                {
                    nextAllowedCall = now;
                }
                
                _lastApiCall = nextAllowedCall;
            }
            
            var delay = nextAllowedCall - now;
            if (delay.TotalMilliseconds > 0)
            {
                _logger?.LogDebug($"Rate limiting: waiting {delay.TotalMilliseconds:F0}ms before API call");
                await Task.Delay(delay);
            }
        }
        
        /// <summary>
        /// Core method for making Steam API calls with retry logic and error handling
        /// </summary>
        private async Task<string?> CallSteamApiAsync(string endpoint)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 1000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await EnforceRateLimitAsync();
                    
                    var fullUrl = $"{STEAM_API_BASE_URL}/{endpoint}";
                    _logger?.LogDebug($"Making Steam API call (attempt {attempt + 1}/{maxRetries}): {fullUrl.Replace(_apiKey, "[REDACTED]")}");
                    
                    var requestStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    using var response = await _httpClient.GetAsync(fullUrl);
                    requestStopwatch.Stop();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        stopwatch.Stop();
                        
                        _logger?.LogError($"=== STEAM API SUCCESS === URL: {fullUrl.Replace(_apiKey, "[REDACTED]")}");
                        _logger?.LogError($"=== RESPONSE STATUS === {(int)response.StatusCode} {response.StatusCode}, Length: {content.Length} chars, Time: {requestStopwatch.ElapsedMilliseconds}ms");
                        
                        // Log response headers that might indicate API version or limits
                        if (response.Headers.Any())
                        {
                            var headers = string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
                            _logger?.LogError($"=== RESPONSE HEADERS === {headers}");
                        }
                        
                        // Always log the full response for troubleshooting
                        if (content.Length < 15000) // Increased threshold for debugging
                        {
                            _logger?.LogError($"=== FULL STEAM API RESPONSE === {content}");
                        }
                        else
                        {
                            // For very large responses, log a substantial preview
                            var preview = content.Length > 3000 ? content.Substring(0, 3000) + $"... [TRUNCATED - Full length: {content.Length} chars]" : content;
                            _logger?.LogError($"=== TRUNCATED STEAM API RESPONSE === {preview}");
                        }
                        
                        return content;
                    }
                    
                    // Handle non-success status codes with detailed logging
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorHeaders = response.Headers.Any() ? 
                        string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")) : "None";
                    
                    _logger?.LogError($"Steam API Error - URL: {fullUrl.Replace(_apiKey, "[REDACTED]")}");
                    _logger?.LogError($"Error Status: {(int)response.StatusCode} {response.StatusCode} - {response.ReasonPhrase}");
                    _logger?.LogError($"Error Headers: {errorHeaders}");
                    _logger?.LogError($"Error Response Content: {errorContent}");
                    
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retryDelay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                        _logger?.LogWarning($"Rate limited by Steam API, waiting {retryDelay.TotalSeconds:F1} seconds before retry {attempt + 1}/{maxRetries}");
                        await Task.Delay(retryDelay);
                        continue;
                    }
                    
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger?.LogError($"Steam API returned 403 Forbidden - check API key validity or access permissions");
                        if (errorContent.Contains("upgrade") || errorContent.Contains("premium") || errorContent.Contains("limit"))
                        {
                            _logger?.LogError($"POTENTIAL API UPGRADE REQUIRED: Error response suggests API limitations or upgrade needed");
                        }
                        return null;
                    }
                    
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger?.LogError($"Steam API returned 401 Unauthorized - invalid API key");
                        return null;
                    }
                    
                    // Check for any upgrade-related messages in error responses
                    if (errorContent.ToLower().Contains("upgrade") || errorContent.ToLower().Contains("premium") || 
                        errorContent.ToLower().Contains("limit") || errorContent.ToLower().Contains("tier"))
                    {
                        _logger?.LogError($"POTENTIAL API UPGRADE REQUIRED: Error response contains upgrade/limit keywords");
                    }
                    
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == maxRetries - 1)
                    {
                        _logger?.LogError($"Steam API call failed after {maxRetries} attempts", ex);
                        return null;
                    }
                    
                    var retryDelay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    _logger?.LogWarning($"Network error on attempt {attempt + 1}/{maxRetries}, retrying in {retryDelay.TotalSeconds:F1} seconds: {ex.Message}");
                    await Task.Delay(retryDelay);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    _logger?.LogWarning($"Steam API call timed out on attempt {attempt + 1}/{maxRetries}");
                    if (attempt == maxRetries - 1)
                        return null;
                    
                    await Task.Delay(TimeSpan.FromMilliseconds(baseDelayMs * (attempt + 1)));
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Unexpected error during Steam API call on attempt {attempt + 1}/{maxRetries}", ex);
                    if (attempt == maxRetries - 1)
                        return null;
                    
                    await Task.Delay(TimeSpan.FromMilliseconds(baseDelayMs));
                }
            }
            
            return null;
        }
        
        #endregion

        #region API Endpoints
        
        /// <summary>
        /// Gets basic player information including name, avatar, and online status
        /// </summary>
        public async Task<PlayerSummariesResponse?> GetPlayerSummaryAsync()
        {
            try
            {
                var endpoint = $"ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={_steamId64}&format=json";
                _logger?.LogError($"=== API CALL START === GetPlayerSummary API - SteamID: {_steamId64}");
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetPlayerSummary Response Length: {jsonResponse.Length}");
                    var response = JsonSerializer.Deserialize<PlayerSummariesResponse>(jsonResponse, JsonOptions);
                    
                    var playerCount = response?.Response?.Players?.Count ?? 0;
                    var playerName = response?.Response?.Players?.FirstOrDefault()?.PersonaName ?? "unknown";
                    _logger?.LogError($"=== PARSED RESULT === Players found: {playerCount}, Primary player: {playerName}");
                    
                    return response;
                }
                
                _logger?.LogError("=== NO RESPONSE === Received empty or null response from Steam API for player summary");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize player summary JSON response: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting player summary: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets basic player information for a specific Steam ID
        /// </summary>
        public async Task<PlayerSummariesResponse?> GetPlayerSummaryAsync(string steamId)
        {
            try
            {
                var endpoint = $"ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={steamId}&format=json";
                _logger?.LogError($"=== API CALL START === GetPlayerSummary API - SteamID: {steamId}");
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetPlayerSummary Response Length: {jsonResponse.Length}");
                    var response = JsonSerializer.Deserialize<PlayerSummariesResponse>(jsonResponse, JsonOptions);
                    
                    var playerCount = response?.Response?.Players?.Count ?? 0;
                    var playerName = response?.Response?.Players?.FirstOrDefault()?.PersonaName ?? "unknown";
                    _logger?.LogError($"=== PARSED RESULT === Player {steamId}: {playerCount} found, Name: {playerName}");
                    
                    return response;
                }
                
                _logger?.LogError($"=== NO RESPONSE === Received empty response for Steam ID: {steamId}");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize player summary for {steamId}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting player summary for {steamId}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets basic player information for multiple Steam IDs in a single API call
        /// </summary>
        public async Task<PlayerSummariesResponse?> GetPlayerSummariesAsync(IEnumerable<string> steamIds)
        {
            try
            {
                if (!steamIds.Any())
                    return null;
                
                // Steam API supports up to 100 Steam IDs per request
                var steamIdsList = steamIds.Take(100).ToList();
                var steamIdsString = string.Join(",", steamIdsList);
                
                var endpoint = $"ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={steamIdsString}&format=json";
                _logger?.LogError($"=== API CALL START === GetPlayerSummaries BATCH API - {steamIdsList.Count} Steam IDs");
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetPlayerSummaries BATCH Response Length: {jsonResponse.Length}");
                    var response = JsonSerializer.Deserialize<PlayerSummariesResponse>(jsonResponse, JsonOptions);
                    
                    var playerCount = response?.Response?.Players?.Count ?? 0;
                    _logger?.LogError($"=== PARSED RESULT === Batch request: {playerCount} players returned for {steamIdsList.Count} requested");
                    
                    return response;
                }
                
                _logger?.LogError("=== NO RESPONSE === Received empty response for batch Steam IDs request");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize batch player summaries: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting batch player summaries: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the complete list of owned games with playtime statistics
        /// </summary>
        public async Task<OwnedGamesResponse?> GetOwnedGamesAsync()
        {
            try
            {
                var endpoint = $"IPlayerService/GetOwnedGames/v1/?key={_apiKey}&steamid={_steamId64}&include_appinfo=1&include_played_free_games=1&format=json";
                _logger?.LogError($"=== API CALL START === GetOwnedGames API - SteamID: {_steamId64}, IncludeAppInfo: true, IncludeFreeGames: true");
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetOwnedGames Response Length: {jsonResponse.Length}");
                    var response = JsonSerializer.Deserialize<OwnedGamesResponse>(jsonResponse, JsonOptions);
                    
                    var gameCount = response?.Response?.GameCount ?? 0;
                    var actualGamesCount = response?.Response?.Games?.Count ?? 0;
                    _logger?.LogError($"=== PARSED RESULT === Owned games - Reported count: {gameCount}, Actual games: {actualGamesCount}");
                    
                    if (response?.Response?.Games != null && response.Response.Games.Any())
                    {
                        var recentGame = response.Response.Games
                            .Where(g => g.Playtime2weeks > 0)
                            .OrderByDescending(g => g.Playtime2weeks)
                            .FirstOrDefault();
                        
                        if (recentGame != null)
                        {
                            _logger?.LogError($"=== GAME ANALYSIS === Most played recent: {recentGame.Name} ({recentGame.Playtime2weeks} minutes)");
                        }
                    }
                    
                    return response;
                }
                
                _logger?.LogError("=== NO RESPONSE === Received empty or null response from Steam API for owned games");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize owned games JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting owned games: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets games played in the last 2 weeks with recent playtime
        /// </summary>
        public async Task<OwnedGamesResponse?> GetRecentlyPlayedGamesAsync()
        {
            try
            {
                var endpoint = $"IPlayerService/GetRecentlyPlayedGames/v1/?key={_apiKey}&steamid={_steamId64}&format=json";
                _logger?.LogError($"=== API CALL START === GetRecentlyPlayedGames API - SteamID: {_steamId64}");
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetRecentlyPlayedGames Response Length: {jsonResponse.Length}");
                    var response = JsonSerializer.Deserialize<OwnedGamesResponse>(jsonResponse, JsonOptions);
                    
                    var gameCount = response?.Response?.GameCount ?? 0;
                    var actualGamesCount = response?.Response?.Games?.Count ?? 0;
                    _logger?.LogError($"=== PARSED RESULT === Recent games - Reported count: {gameCount}, Actual games: {actualGamesCount}");
                    
                    if (response?.Response?.Games != null && response.Response.Games.Any())
                    {
                        var topGame = response.Response.Games
                            .OrderByDescending(g => g.Playtime2weeks)
                            .FirstOrDefault();
                        
                        if (topGame != null)
                        {
                            _logger?.LogError($"=== GAME ANALYSIS === Top recent game: {topGame.Name} ({topGame.Playtime2weeks} minutes in 2w)");
                        }
                        
                        var totalMinutes = response.Response.Games.Sum(g => g.Playtime2weeks);
                        _logger?.LogError($"=== TOTALS === Total recent playtime: {totalMinutes} minutes ({totalMinutes / 60.0:F1} hours)");
                    }
                    
                    return response;
                }
                
                _logger?.LogError("=== NO RESPONSE === Received empty or null response from Steam API for recently played games");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError("Failed to deserialize recently played games JSON response", ex);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error getting recently played games", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Gets the player's Steam level
        /// </summary>
        public async Task<SteamLevelResponse?> GetSteamLevelAsync()
        {
            try
            {
                var endpoint = $"IPlayerService/GetSteamLevel/v1/?key={_apiKey}&steamid={_steamId64}&format=json";
                _logger?.LogError($"=== API CALL START === GetSteamLevel API - SteamID: {_steamId64}");
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetSteamLevel Response Length: {jsonResponse.Length}");
                    var response = JsonSerializer.Deserialize<SteamLevelResponse>(jsonResponse, JsonOptions);
                    var level = response?.Response?.PlayerLevel ?? 0;
                    _logger?.LogError($"=== PARSED RESULT === Steam level: {level}");
                    return response;
                }
                
                _logger?.LogError("=== NO RESPONSE === Received empty or null response from Steam API for Steam level");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize Steam level JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting Steam level: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the friends list for the Steam user
        /// Uses ISteamUser/GetFriendList API endpoint
        /// </summary>
        public async Task<FriendsListResponse?> GetFriendsListAsync()
        {
            try
            {
                _logger?.LogError($"=== API CALL START === GetFriendsList API - SteamID: {_steamId64}");

                string endpoint = $"/ISteamUser/GetFriendList/v1/?key={_apiKey}&steamid={_steamId64}&relationship=friend&format=json";
                string? jsonResponse = await CallSteamApiAsync(endpoint);

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetFriendsList Response Length: {jsonResponse.Length}");
                    
                    var friendsResponse = JsonSerializer.Deserialize<FriendsListResponse>(jsonResponse, JsonOptions);
                    int friendCount = friendsResponse?.FriendsList?.Friends?.Count ?? 0;
                    
                    _logger?.LogError($"=== PARSED RESULT === Friends found: {friendCount}");
                    return friendsResponse;
                }

                _logger?.LogError("=== NO RESPONSE === Received empty or null response from Steam API for friends list");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize friends list JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting friends list: {ex.Message}");
                return null;
            }
        }
        
        #endregion

        /// <summary>
        /// Tests the API connection and validates the API key
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger?.LogInfo("Testing Steam API connection...");
                
                var playerSummary = await GetPlayerSummaryAsync();
                if (playerSummary?.Response?.Players?.Any() == true)
                {
                    var player = playerSummary.Response.Players.First();
                    _logger?.LogInfo($"Steam API connection successful - Player: {player.PersonaName} (ID: {player.SteamId})");
                    return true;
                }
                
                _logger?.LogWarning("Steam API connection test failed - no player data returned");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Steam API connection test failed", ex);
                return false;
            }
        }

        #region Enhanced API Methods (Token-based)
        
        /// <summary>
        /// Gets player badges using enhanced community token access
        /// </summary>
        public async Task<BadgesResponse?> GetPlayerBadgesAsync(string? communityToken = null)
        {
            try
            {
                _logger?.LogError($"=== API CALL START === GetPlayerBadges API - SteamID: {_steamId64}");

                string endpoint;
                if (!string.IsNullOrEmpty(communityToken))
                {
                    endpoint = $"IPlayerService/GetBadges/v1/?access_token={communityToken}&steamid={_steamId64}&format=json";
                    _logger?.LogError("=== AUTHENTICATION === Using community token for enhanced badge data");
                }
                else
                {
                    endpoint = $"IPlayerService/GetBadges/v1/?key={_apiKey}&steamid={_steamId64}&format=json";
                    _logger?.LogError("=== AUTHENTICATION === Using API key for basic badge data");
                }
                
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetPlayerBadges Response Length: {jsonResponse.Length}");
                    
                    var response = JsonSerializer.Deserialize<BadgesResponse>(jsonResponse, JsonOptions);
                    var badgeCount = response?.Response?.Badges?.Count ?? 0;
                    var playerXp = response?.Response?.PlayerXp ?? 0;
                    var playerLevel = response?.Response?.PlayerLevel ?? 0;
                    
                    _logger?.LogError($"=== PARSED RESULT === Badges found: {badgeCount}, Player XP: {playerXp}, Player Level: {playerLevel}");
                    return response;
                }

                _logger?.LogError("=== NO RESPONSE === Received empty or null response from Steam API for player badges");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize badges JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting player badges: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets player achievements for a specific game
        /// </summary>
        public async Task<PlayerAchievementsResponse?> GetPlayerAchievementsAsync(int appId)
        {
            try
            {
                _logger?.LogError($"=== API CALL START === GetPlayerAchievements API - SteamID: {_steamId64}, AppID: {appId}");
                
                var endpoint = $"ISteamUserStats/GetPlayerAchievements/v1/?key={_apiKey}&steamid={_steamId64}&appid={appId}&format=json";
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _logger?.LogError($"=== API RESPONSE === GetPlayerAchievements Response Length: {jsonResponse.Length}");
                    
                    var response = JsonSerializer.Deserialize<PlayerAchievementsResponse>(jsonResponse, JsonOptions);
                    var achievementCount = response?.PlayerStats?.Achievements?.Count ?? 0;
                    var unlockedCount = response?.PlayerStats?.Achievements?.Count(a => a.Achieved == 1) ?? 0;
                    var completionPercent = achievementCount > 0 ? (double)unlockedCount / achievementCount * 100 : 0;
                    
                    _logger?.LogError($"=== PARSED RESULT === App {appId}: {achievementCount} total achievements, {unlockedCount} unlocked ({completionPercent:F1}%)");
                    return response;
                }

                _logger?.LogError($"=== NO RESPONSE === Received empty or null response from Steam API for achievements (App {appId})");
                return null;
            }
            catch (JsonException ex)
            {
                _logger?.LogError($"=== JSON ERROR === Failed to deserialize achievements JSON for app {appId}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"=== GENERAL ERROR === Error getting achievements for app {appId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets news articles for a specific game
        /// </summary>
        public async Task<GameNewsResponse?> GetGameNewsAsync(int appId, int count = 5, int maxLength = 300)
        {
            try
            {
                var endpoint = $"ISteamNews/GetNewsForApp/v2/?appid={appId}&count={count}&maxlength={maxLength}&format=json";
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    var response = JsonSerializer.Deserialize<GameNewsResponse>(jsonResponse, JsonOptions);
                    var newsCount = response?.AppNews?.NewsItems?.Count ?? 0;
                    _logger?.LogDebug($"Retrieved {newsCount} news articles for app {appId}");
                    return response;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting news for app {appId}", ex);
                return null;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Converts Steam persona state integer to readable string
        /// </summary>
        public static string GetPersonaStateString(int personaState)
        {
            return SteamPersonaState.GetPersonaStateString(personaState);
        }

        /// <summary>
        /// Gets a formatted Steam icon URL for a game
        /// </summary>
        public static string GetGameIconUrl(int appId, string iconHash)
        {
            if (string.IsNullOrEmpty(iconHash))
                return string.Empty;
                
            return $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconHash}.jpg";
        }

        /// <summary>
        /// Gets a formatted Steam logo URL for a game
        /// </summary>
        public static string GetGameLogoUrl(int appId, string logoHash)
        {
            if (string.IsNullOrEmpty(logoHash))
                return string.Empty;
                
            return $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{logoHash}.jpg";
        }

        /// <summary>
        /// Converts Unix timestamp to DateTimeOffset
        /// </summary>
        public static DateTimeOffset FromUnixTimestamp(long unixTimestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Disposes of the HTTP client and other resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _httpClient?.Dispose();
                    _logger?.LogInfo("SteamApiService disposed");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Error during SteamApiService disposal", ex);
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        #endregion
    }
}
