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
        private readonly EnhancedLoggingService? _enhancedLogger;
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
        /// <param name="logger">Optional file logging service for detailed API logging (legacy)</param>
        /// <param name="enhancedLogger">Optional enhanced logging service</param>
        public SteamApiService(string apiKey, string steamId64, FileLoggingService? logger = null, EnhancedLoggingService? enhancedLogger = null)
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
            _enhancedLogger = enhancedLogger;
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InfoPanel-SteamAPI/1.0.0");
            
            // Enhanced logging for initialization
            _enhancedLogger?.LogInfo("SteamApiService.Constructor", "SteamApiService initialized", new
            {
                SteamId = steamId64,
                HasApiKey = !string.IsNullOrEmpty(apiKey),
                Timeout = "30s",
                UserAgent = "InfoPanel-SteamAPI/1.0.0"
            });
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
                _enhancedLogger?.LogDebug("SteamApiService.EnforceRateLimitAsync", "Rate limiting active", new
                {
                    WaitTimeMs = Math.Round(delay.TotalMilliseconds, 0)
                });
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
                    _enhancedLogger?.LogDebug("SteamApiService.CallSteamApiAsync", "Making Steam API call", new
                    {
                        Attempt = attempt + 1,
                        MaxRetries = maxRetries,
                        Url = fullUrl.Replace(_apiKey, "[REDACTED]")
                    });
                    
                    var requestStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    using var response = await _httpClient.GetAsync(fullUrl);
                    requestStopwatch.Stop();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        stopwatch.Stop();
                        
                        _enhancedLogger?.LogInfo("SteamApiService.CallSteamApiAsync", "Steam API call succeeded", new
                        {
                            Url = fullUrl.Replace(_apiKey, "[REDACTED]"),
                            StatusCode = (int)response.StatusCode,
                            Status = response.StatusCode.ToString(),
                            ResponseLength = content.Length,
                            RequestTimeMs = requestStopwatch.ElapsedMilliseconds,
                            TotalTimeMs = stopwatch.ElapsedMilliseconds
                        });
                        
                        // Log response headers that might indicate API version or limits
                        if (response.Headers.Any())
                        {
                            var headers = string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
                            _enhancedLogger?.LogDebug("SteamApiService.CallSteamApiAsync", "Response headers", new { Headers = headers });
                        }
                        
                        // Log full response for debugging (with size limits)
                        if (content.Length < 15000)
                        {
                            _enhancedLogger?.LogDebug("SteamApiService.CallSteamApiAsync", "Full API response", new { Response = content });
                        }
                        else
                        {
                            var preview = content.Substring(0, 3000) + $"... [TRUNCATED]";
                            _enhancedLogger?.LogDebug("SteamApiService.CallSteamApiAsync", "Truncated API response", new
                            {
                                Preview = preview,
                                FullLength = content.Length
                            });
                        }
                        
                        return content;
                    }
                    
                    // Handle non-success status codes with detailed logging
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorHeaders = response.Headers.Any() ? 
                        string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")) : "None";
                    
                    _enhancedLogger?.LogError("SteamApiService.CallSteamApiAsync", "Steam API error response", null, new
                    {
                        Url = fullUrl.Replace(_apiKey, "[REDACTED]"),
                        StatusCode = (int)response.StatusCode,
                        Status = response.StatusCode.ToString(),
                        ReasonPhrase = response.ReasonPhrase,
                        Headers = errorHeaders,
                        ErrorContent = errorContent
                    });
                    
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retryDelay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                        _enhancedLogger?.LogWarning("SteamApiService.CallSteamApiAsync", "Rate limited by Steam API", new
                        {
                            RetryDelaySeconds = Math.Round(retryDelay.TotalSeconds, 1),
                            Attempt = attempt + 1,
                            MaxRetries = maxRetries
                        });
                        await Task.Delay(retryDelay);
                        continue;
                    }
                    
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        var potentialUpgradeRequired = errorContent.Contains("upgrade") || errorContent.Contains("premium") || errorContent.Contains("limit");
                        _enhancedLogger?.LogError("SteamApiService.CallSteamApiAsync", "Steam API returned 403 Forbidden", null, new
                        {
                            Message = "Check API key validity or access permissions",
                            PotentialUpgradeRequired = potentialUpgradeRequired
                        });
                        return null;
                    }
                    
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _enhancedLogger?.LogError("SteamApiService.CallSteamApiAsync", "Steam API returned 401 Unauthorized - invalid API key", null);
                        return null;
                    }
                    
                    // Check for any upgrade-related messages in error responses
                    if (errorContent.ToLower().Contains("upgrade") || errorContent.ToLower().Contains("premium") || 
                        errorContent.ToLower().Contains("limit") || errorContent.ToLower().Contains("tier"))
                    {
                        _enhancedLogger?.LogError("SteamApiService.CallSteamApiAsync", "Potential API upgrade required", null, new
                        {
                            Message = "Error response contains upgrade/limit keywords",
                            ErrorContent = errorContent
                        });
                    }
                    
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == maxRetries - 1)
                    {
                        _enhancedLogger?.LogError("SteamApiService.CallSteamApiAsync", "Steam API call failed after all retries", ex, new
                        {
                            MaxRetries = maxRetries,
                            Endpoint = endpoint
                        });
                        return null;
                    }
                    
                    var retryDelay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    _enhancedLogger?.LogWarning("SteamApiService.CallSteamApiAsync", "Network error, retrying", new
                    {
                        Attempt = attempt + 1,
                        MaxRetries = maxRetries,
                        RetryDelaySeconds = Math.Round(retryDelay.TotalSeconds, 1),
                        ErrorMessage = ex.Message
                    });
                    await Task.Delay(retryDelay);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    _enhancedLogger?.LogWarning("SteamApiService.CallSteamApiAsync", "Steam API call timed out", new
                    {
                        Attempt = attempt + 1,
                        MaxRetries = maxRetries
                    });
                    if (attempt == maxRetries - 1)
                        return null;
                    
                    await Task.Delay(TimeSpan.FromMilliseconds(baseDelayMs * (attempt + 1)));
                }
                catch (Exception ex)
                {
                    _enhancedLogger?.LogError("SteamApiService.CallSteamApiAsync", "Unexpected error during Steam API call", ex, new
                    {
                        Attempt = attempt + 1,
                        MaxRetries = maxRetries
                    });
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
                _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummaryAsync", "Initiating API call for player summary", new { SteamId = _steamId64 });
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummaryAsync", "Received API response", new { ResponseLength = jsonResponse.Length });
                    var response = JsonSerializer.Deserialize<PlayerSummariesResponse>(jsonResponse, JsonOptions);
                    
                    var playerCount = response?.Response?.Players?.Count ?? 0;
                    var playerName = response?.Response?.Players?.FirstOrDefault()?.PersonaName ?? "unknown";
                    _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummaryAsync", "Parsed player summary data", new { PlayersFound = playerCount, PrimaryPlayer = playerName });
                    
                    return response;
                }
                
                _enhancedLogger?.LogWarning("SteamApiService.GetPlayerSummaryAsync", "Received empty or null response from Steam API", new { SteamId = _steamId64 });
                return null;
            }
            catch (JsonException ex)
            {
                _enhancedLogger?.LogError("SteamApiService.GetPlayerSummaryAsync", "Failed to deserialize player summary JSON", ex, new { ErrorMessage = ex.Message });
                return null;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamApiService.GetPlayerSummaryAsync", "Unexpected error getting player summary", ex, new { ErrorMessage = ex.Message });
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
                _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummaryAsync", "Initiating API call for specific player", new { SteamId = steamId });
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummaryAsync", "Received API response for specific player", new { SteamId = steamId, ResponseLength = jsonResponse.Length });
                    var response = JsonSerializer.Deserialize<PlayerSummariesResponse>(jsonResponse, JsonOptions);
                    
                    var playerCount = response?.Response?.Players?.Count ?? 0;
                    var playerName = response?.Response?.Players?.FirstOrDefault()?.PersonaName ?? "unknown";
                    _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummaryAsync", "Parsed specific player data", new { SteamId = steamId, PlayersFound = playerCount, PlayerName = playerName });
                    
                    return response;
                }
                
                _enhancedLogger?.LogWarning("SteamApiService.GetPlayerSummaryAsync", "Received empty response for specific Steam ID", new { SteamId = steamId });
                return null;
            }
            catch (JsonException ex)
            {
                _enhancedLogger?.LogError("SteamApiService.GetPlayerSummaryAsync", "Failed to deserialize player summary for specific ID", ex, new { SteamId = steamId, ErrorMessage = ex.Message });
                return null;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamApiService.GetPlayerSummaryAsync", "Unexpected error getting player summary for specific ID", ex, new { SteamId = steamId, ErrorMessage = ex.Message });
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
                _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummariesAsync", "Initiating batch API call for multiple players", new { RequestedCount = steamIdsList.Count });
                var jsonResponse = await CallSteamApiAsync(endpoint);
                
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummariesAsync", "Received batch API response", new { ResponseLength = jsonResponse.Length });
                    var response = JsonSerializer.Deserialize<PlayerSummariesResponse>(jsonResponse, JsonOptions);
                    
                    var playerCount = response?.Response?.Players?.Count ?? 0;
                    _enhancedLogger?.LogDebug("SteamApiService.GetPlayerSummariesAsync", "Parsed batch player data", new { PlayersReturned = playerCount, PlayersRequested = steamIdsList.Count });
                    
                    return response;
                }
                
                _enhancedLogger?.LogWarning("SteamApiService.GetPlayerSummariesAsync", "Received empty response for batch Steam IDs request", new { RequestedCount = steamIdsList.Count });
                return null;
            }
            catch (JsonException ex)
            {
                _enhancedLogger?.LogError("SteamApiService.GetPlayerSummariesAsync", "Failed to deserialize batch player summaries", ex, new { ErrorMessage = ex.Message });
                return null;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamApiService.GetPlayerSummariesAsync", "Unexpected error getting batch player summaries", ex, new { ErrorMessage = ex.Message });
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
