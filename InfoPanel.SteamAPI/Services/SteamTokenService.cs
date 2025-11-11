using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InfoPanel.SteamAPI.Models;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Advanced Steam token management service for enhanced API access
    /// Handles automatic token acquisition, refresh, and storage for Steam Web API
    /// </summary>
    public class SteamTokenService : IDisposable
    {
        #region Fields

        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly EnhancedLoggingService? _enhancedLogger;
        private readonly HttpClient _httpClient;
        private readonly string _tokenFilePath;
        private SteamTokenData? _cachedTokens;
        private DateTime _lastTokenCheck = DateTime.MinValue;
        private readonly TimeSpan _tokenCheckInterval = TimeSpan.FromMinutes(30);

        #endregion

        #region Constructor

        public SteamTokenService(ConfigurationService configService, FileLoggingService? logger = null, EnhancedLoggingService? enhancedLogger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            _enhancedLogger = enhancedLogger;
            
            // Initialize HTTP client with Steam-like headers
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            
            // Set token file path in plugin directory
            var pluginDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
            _tokenFilePath = Path.Combine(pluginDirectory, "steam.tokens");
            
            _enhancedLogger?.LogInfo("SteamTokenService.Constructor", "SteamTokenService initialized", new { TokenFilePath = _tokenFilePath });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a valid community token, attempting refresh if expired
        /// </summary>
        public async Task<string?> GetCommunityTokenAsync()
        {
            try
            {
                await RefreshTokenCacheIfNeededAsync();
                
                var tokens = _cachedTokens;
                if (IsTokenValid(tokens?.CommunityToken))
                {
                    _enhancedLogger?.LogDebug("SteamTokenService.GetCommunityTokenAsync", "Using cached community token");
                    return tokens!.CommunityToken.Token;
                }
                
                _enhancedLogger?.LogInfo("SteamTokenService.GetCommunityTokenAsync", "Community token expired or missing, attempting refresh");
                
                // Attempt automatic refresh
                var newToken = await AttemptCommunityTokenRefreshAsync();
                if (newToken != null)
                {
                    await SaveCommunityTokenAsync(newToken);
                    _enhancedLogger?.LogInfo("SteamTokenService.GetCommunityTokenAsync", "Community token refreshed successfully");
                    return newToken.Token;
                }
                
                _enhancedLogger?.LogWarning("SteamTokenService.GetCommunityTokenAsync", "Could not automatically refresh community token");
                return null;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.GetCommunityTokenAsync", "Error getting community token", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Gets a valid store token, attempting refresh if expired
        /// </summary>
        public async Task<string?> GetStoreTokenAsync()
        {
            try
            {
                await RefreshTokenCacheIfNeededAsync();
                
                var tokens = _cachedTokens;
                if (IsTokenValid(tokens?.StoreToken))
                {
                    _enhancedLogger?.LogDebug("SteamTokenService.GetStoreTokenAsync", "Using cached store token");
                    return tokens!.StoreToken.Token;
                }
                
                _enhancedLogger?.LogInfo("SteamTokenService.GetStoreTokenAsync", "Store token expired or missing, attempting refresh");
                
                // Attempt automatic refresh
                var newToken = await AttemptStoreTokenRefreshAsync();
                if (newToken != null)
                {
                    await SaveStoreTokenAsync(newToken);
                    _enhancedLogger?.LogInfo("SteamTokenService.GetStoreTokenAsync", "Store token refreshed successfully");
                    return newToken.Token;
                }
                
                _enhancedLogger?.LogWarning("SteamTokenService.GetStoreTokenAsync", "Could not automatically refresh store token");
                return null;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.GetStoreTokenAsync", "Error getting store token", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Manually sets a community token from user input
        /// </summary>
        public async Task<bool> SetCommunityTokenManuallyAsync(string tokenInput)
        {
            try
            {
                var tokenInfo = ParseTokenInput(tokenInput, "web:community");
                if (tokenInfo != null)
                {
                    await SaveCommunityTokenAsync(tokenInfo);
                    _enhancedLogger?.LogInfo("SteamTokenService.SetCommunityTokenManuallyAsync", "Community token set manually");
                    return true;
                }
                
                _enhancedLogger?.LogWarning("SteamTokenService.SetCommunityTokenManuallyAsync", "Invalid community token format provided");
                return false;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.SetCommunityTokenManuallyAsync", "Error setting community token manually", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Manually sets a store token from user input
        /// </summary>
        public async Task<bool> SetStoreTokenManuallyAsync(string tokenInput)
        {
            try
            {
                var tokenInfo = ParseTokenInput(tokenInput, "web:store");
                if (tokenInfo != null)
                {
                    await SaveStoreTokenAsync(tokenInfo);
                    _enhancedLogger?.LogInfo("SteamTokenService.SetStoreTokenManuallyAsync", "Store token set manually");
                    return true;
                }
                
                _enhancedLogger?.LogWarning("SteamTokenService.SetStoreTokenManuallyAsync", "Invalid store token format provided");
                return false;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.SetStoreTokenManuallyAsync", "Error setting store token manually", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Forces a refresh of both tokens
        /// </summary>
        public async Task<bool> ForceRefreshAllTokensAsync()
        {
            try
            {
                _enhancedLogger?.LogInfo("SteamTokenService.ForceRefreshAllTokensAsync", "Force refreshing all Steam tokens");
                
                var communityRefreshed = false;
                var storeRefreshed = false;
                
                // Attempt community token refresh
                var communityToken = await AttemptCommunityTokenRefreshAsync();
                if (communityToken != null)
                {
                    await SaveCommunityTokenAsync(communityToken);
                    communityRefreshed = true;
                }
                
                // Attempt store token refresh  
                var storeToken = await AttemptStoreTokenRefreshAsync();
                if (storeToken != null)
                {
                    await SaveStoreTokenAsync(storeToken);
                    storeRefreshed = true;
                }
                
                var result = communityRefreshed || storeRefreshed;
                _enhancedLogger?.LogInfo("SteamTokenService.ForceRefreshAllTokensAsync", "Token refresh completed", new { CommunityRefreshed = communityRefreshed, StoreRefreshed = storeRefreshed });
                
                return result;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.ForceRefreshAllTokensAsync", "Error during force token refresh", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Gets token status information
        /// </summary>
        public async Task<TokenStatus> GetTokenStatusAsync()
        {
            await RefreshTokenCacheIfNeededAsync();
            
            var tokens = _cachedTokens;
            return new TokenStatus
            {
                CommunityTokenValid = IsTokenValid(tokens?.CommunityToken),
                StoreTokenValid = IsTokenValid(tokens?.StoreToken),
                CommunityTokenExpires = tokens?.CommunityToken?.Expires,
                StoreTokenExpires = tokens?.StoreToken?.Expires,
                LastRefresh = tokens?.LastRefresh,
                AutoRefreshEnabled = _configService.GetBoolValue("TokenManagement", "AutoRefreshTokens", true)
            };
        }

        #endregion

        #region Private Methods

        private async Task RefreshTokenCacheIfNeededAsync()
        {
            if (DateTime.Now - _lastTokenCheck > _tokenCheckInterval || _cachedTokens == null)
            {
                _cachedTokens = await LoadTokensAsync();
                _lastTokenCheck = DateTime.Now;
            }
        }
        
        private bool IsTokenValid(SteamToken? token)
        {
            if (token == null || string.IsNullOrEmpty(token.Token))
                return false;
                
            if (token.Expires.HasValue && DateTime.UtcNow >= token.Expires.Value)
                return false;
                
            return true;
        }
        
        private Task<SteamToken?> AttemptCommunityTokenRefreshAsync()
        {
            try
            {
                _enhancedLogger?.LogDebug("SteamTokenService.AttemptCommunityTokenRefreshAsync", "Attempting to refresh community token");
                
                // This is a simplified approach - in reality, this requires authenticated session
                // For now, we'll return null to indicate manual token entry is needed
                
                // TODO: Implement automatic token acquisition
                // This would require:
                // 1. Steam login session management
                // 2. CSRF token handling
                // 3. Cookie management
                // 4. HTML parsing for token extraction
                
                _enhancedLogger?.LogInfo("SteamTokenService.AttemptCommunityTokenRefreshAsync", "Automatic community token refresh not implemented - manual entry required");
                return Task.FromResult<SteamToken?>(null);
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.AttemptCommunityTokenRefreshAsync", "Community token refresh failed", ex);
                return Task.FromResult<SteamToken?>(null);
            }
        }
        
        private async Task<SteamToken?> AttemptStoreTokenRefreshAsync()
        {
            try
            {
                _enhancedLogger?.LogDebug("SteamTokenService.AttemptStoreTokenRefreshAsync", "Attempting to refresh store token");
                
                // Attempt to fetch store token from public endpoint
                var response = await _httpClient.GetStringAsync("https://store.steampowered.com/pointssummary/ajaxgetasyncconfig");
                
                // Parse JSON response to extract webapi_token
                using var document = JsonDocument.Parse(response);
                if (document.RootElement.TryGetProperty("webapi_token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        return new SteamToken
                        {
                            Token = token,
                            Expires = DateTime.UtcNow.AddHours(24), // Estimate 24-hour expiration
                            Scope = "web:store",
                            SteamId = null
                        };
                    }
                }
                
                _enhancedLogger?.LogWarning("SteamTokenService.AttemptStoreTokenRefreshAsync", "Could not parse store token from response");
                return null;
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.AttemptStoreTokenRefreshAsync", "Store token refresh failed", ex);
                return null;
            }
        }
        
        private SteamToken? ParseTokenInput(string input, string scope)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;
                
            input = input.Trim();
            
            try
            {
                // Handle full JSON paste (for store tokens)
                if (input.StartsWith("{") && input.EndsWith("}"))
                {
                    using var document = JsonDocument.Parse(input);
                    if (document.RootElement.TryGetProperty("webapi_token", out var tokenElement))
                    {
                        var token = tokenElement.GetString();
                        if (!string.IsNullOrEmpty(token))
                        {
                            return new SteamToken
                            {
                                Token = token,
                                Expires = DateTime.UtcNow.AddHours(24),
                                Scope = scope,
                                SteamId = scope == "web:community" ? _configService.SteamId64 : null
                            };
                        }
                    }
                }
                
                // Handle direct token paste
                if (input.Length > 20 && !input.Contains(" ") && !input.Contains("\n"))
                {
                    return new SteamToken
                    {
                        Token = input,
                        Expires = DateTime.UtcNow.AddHours(24),
                        Scope = scope,
                        SteamId = scope == "web:community" ? _configService.SteamId64 : null
                    };
                }
                
                // Handle community token JSON format
                if (scope == "web:community")
                {
                    try
                    {
                        var communityData = JsonSerializer.Deserialize<Dictionary<string, object>>(input);
                        if (communityData.ContainsKey("token"))
                        {
                            return new SteamToken
                            {
                                Token = communityData["token"].ToString()!,
                                Expires = DateTime.UtcNow.AddHours(24),
                                Scope = scope,
                                SteamId = _configService.SteamId64
                            };
                        }
                    }
                    catch
                    {
                        // Fall through to return null
                    }
                }
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.ParseTokenInput", "Error parsing token input", ex, new { ErrorMessage = ex.Message });
            }
            
            return null;
        }
        
        private async Task<SteamTokenData> LoadTokensAsync()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    var json = await File.ReadAllTextAsync(_tokenFilePath);
                    var tokens = JsonSerializer.Deserialize<SteamTokenData>(json);
                    if (tokens != null)
                    {
                        _enhancedLogger?.LogDebug("SteamTokenService.LoadTokensAsync", "Loaded tokens from file");
                        return tokens;
                    }
                }
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.LoadTokensAsync", "Error loading tokens from file", ex);
            }
            
            return new SteamTokenData();
        }
        
        private async Task SaveCommunityTokenAsync(SteamToken token)
        {
            await RefreshTokenCacheIfNeededAsync();
            
            var tokens = _cachedTokens ?? new SteamTokenData();
            tokens.CommunityToken = token;
            tokens.LastRefresh = DateTime.UtcNow;
            
            await SaveTokensAsync(tokens);
            _cachedTokens = tokens;
        }
        
        private async Task SaveStoreTokenAsync(SteamToken token)
        {
            await RefreshTokenCacheIfNeededAsync();
            
            var tokens = _cachedTokens ?? new SteamTokenData();
            tokens.StoreToken = token;
            tokens.LastRefresh = DateTime.UtcNow;
            
            await SaveTokensAsync(tokens);
            _cachedTokens = tokens;
        }
        
        private async Task SaveTokensAsync(SteamTokenData tokens)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var json = JsonSerializer.Serialize(tokens, options);
                await File.WriteAllTextAsync(_tokenFilePath, json);
                
                _enhancedLogger?.LogDebug("SteamTokenService.SaveTokensAsync", "Tokens saved to file");
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.SaveTokensAsync", "Error saving tokens to file", ex);
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            try
            {
                _httpClient?.Dispose();
                _enhancedLogger?.LogInfo("SteamTokenService.Dispose", "SteamTokenService disposed successfully");
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SteamTokenService.Dispose", "Error during SteamTokenService disposal", ex);
            }
        }

        #endregion
    }
    
    #region Data Models
    
    public class SteamTokenData
    {
        public SteamToken? CommunityToken { get; set; }
        public SteamToken? StoreToken { get; set; }
        public DateTime? LastRefresh { get; set; }
        public bool AutoRefreshEnabled { get; set; } = true;
    }
    
    public class SteamToken
    {
        public string Token { get; set; } = string.Empty;
        public DateTime? Expires { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string? SteamId { get; set; }
    }
    
    public class TokenStatus
    {
        public bool CommunityTokenValid { get; set; }
        public bool StoreTokenValid { get; set; }
        public DateTime? CommunityTokenExpires { get; set; }
        public DateTime? StoreTokenExpires { get; set; }
        public DateTime? LastRefresh { get; set; }
        public bool AutoRefreshEnabled { get; set; }
    }
    
    #endregion
}