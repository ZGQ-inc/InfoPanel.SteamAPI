using InfoPanel.SteamAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for social features and community data
    /// </summary>
    public static class SocialConstants
    {
        // Social activity level thresholds
        public const int VERY_SOCIAL_FRIENDS_THRESHOLD = 5;
        public const int SOCIAL_FRIENDS_THRESHOLD = 2;
        
        // Community engagement thresholds
        public const int HIGHLY_ENGAGED_THRESHOLD = 50;
        public const int ACTIVE_ENGAGEMENT_THRESHOLD = 20;
        public const int CASUAL_ENGAGEMENT_THRESHOLD = 5;
        
        // Defaults
        public const string UNKNOWN_POPULAR_GAME = "Unknown";
        public const string NO_POPULAR_GAME = "None";
        public const string ERROR_POPULAR_GAME = "Error";
    }

    /// <summary>
    /// Service responsible for collecting social and community data
    /// Handles friends lists, friends activity, community features, and social statistics
    /// Optimized for medium updates (15-second intervals)
    /// </summary>
    public class SocialDataService
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly SteamApiService _steamApiService;
        
        #endregion

        #region Constructor
        
        public SocialDataService(
            ConfigurationService configService, 
            SteamApiService steamApiService,
            FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _steamApiService = steamApiService ?? throw new ArgumentNullException(nameof(steamApiService));
            _logger = logger;
        }
        
        #endregion

        #region Social Data Collection

        /// <summary>
        /// Collects social and community data (medium tier - 15s interval)
        /// Friends status, friends activity, community features
        /// </summary>
        public async Task<SocialData> CollectSocialDataAsync()
        {
            try
            {
                _logger?.LogDebug("[SocialDataService] Starting social data collection...");
                var socialData = new SocialData();

                // 1. Collect friends data
                await CollectFriendsDataAsync(socialData);

                // 2. Collect community features (placeholder for now)
                await CollectCommunityDataAsync(socialData);

                // Set status based on collected data
                socialData.Status = "Social data updated";
                socialData.Timestamp = DateTime.Now;
                
                _logger?.LogDebug($"[SocialDataService] Social data collection completed - Friends: {socialData.FriendsOnline}, In Game: {socialData.FriendsInGame}");
                return socialData;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[SocialDataService] Error collecting social data", ex);
                return new SocialData
                {
                    HasError = true,
                    ErrorMessage = $"Social data error: {ex.Message}",
                    Status = "Error",
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Converts Steam PersonaState to readable string
        /// </summary>
        private static string GetPersonaStateString(int personaState)
        {
            return personaState switch
            {
                0 => "Offline",
                1 => "Online", 
                2 => "Busy",
                3 => "Away",
                4 => "Snooze",
                5 => "Looking to Trade",
                6 => "Looking to Play",
                _ => "Unknown"
            };
        }
        
        #endregion
        
        #region Private Data Collection Methods

        /// <summary>
        /// Collects friends data for social metrics
        /// </summary>
        private async Task CollectFriendsDataAsync(SocialData socialData)
        {
            try
            {
                _logger?.LogDebug("[SocialDataService] Collecting friends data...");
                
                // Get friends list from Steam API
                var friendsResponse = await _steamApiService.GetFriendsListAsync();
                
                if (friendsResponse?.FriendsList?.Friends != null)
                {
                    var friends = friendsResponse.FriendsList.Friends;
                    socialData.TotalFriends = friends.Count;
                    
                    // Get actual friend status data from Steam API instead of estimates
                    int onlineCount = 0;
                    int inGameCount = 0;
                    var friendsActivity = new List<FriendActivity>();
                    var gamesPlayedByFriends = new Dictionary<string, int>();
                    
                    _logger?.LogDebug($"[SocialDataService] Checking status for {friends.Count} friends using batch API...");
                    
                    // Collect all friend Steam IDs
                    var friendSteamIds = friends.Select(f => f.SteamId).ToList();
                    
                    // Get all friend summaries in a single batch API call
                    var batchResponse = await _steamApiService.GetPlayerSummariesAsync(friendSteamIds);
                    var players = batchResponse?.Response?.Players;
                    
                    if (players != null)
                    {
                        foreach (var player in players)
                        {
                            // Check if friend is online (PersonaState: 0=Offline, 1=Online, 2=Busy, 3=Away, 4=Snooze, 5=Looking to trade, 6=Looking to play)
                            if (player.PersonaState > 0)
                            {
                                onlineCount++;
                                
                                // Add ALL online friends to activity list, not just those in games
                                friendsActivity.Add(new FriendActivity
                                {
                                    FriendName = player.PersonaName,
                                    CurrentGame = player.GameExtraInfo ?? "Not in game",
                                    Status = GetPersonaStateString(player.PersonaState),
                                    LastSeen = DateTimeOffset.FromUnixTimeSeconds(player.LastLogoff).DateTime
                                });
                                
                                // Check if friend is in game
                                if (!string.IsNullOrEmpty(player.GameExtraInfo))
                                {
                                    inGameCount++;
                                    
                                    // Track what game they're playing for popular game calculation
                                    if (!gamesPlayedByFriends.ContainsKey(player.GameExtraInfo))
                                        gamesPlayedByFriends[player.GameExtraInfo] = 0;
                                    gamesPlayedByFriends[player.GameExtraInfo]++;
                                }
                            }
                        }
                        
                        _logger?.LogDebug($"[SocialDataService] Batch API result: {onlineCount} friends online, {inGameCount} in games");
                    }
                    else
                    {
                        _logger?.LogWarning("[SocialDataService] No player data returned from batch API call");
                    }
                    
                    // Set real data instead of estimates
                    socialData.FriendsOnline = onlineCount;
                    socialData.FriendsInGame = inGameCount;
                    socialData.FriendsActivity = friendsActivity;
                    
                    // Calculate most popular game among friends
                    socialData.FriendsPopularGame = gamesPlayedByFriends.Count > 0 
                        ? gamesPlayedByFriends.OrderByDescending(kvp => kvp.Value).First().Key
                        : SocialConstants.NO_POPULAR_GAME;
                    
                    _logger?.LogInfo($"[SocialDataService] Friends data - Total: {socialData.TotalFriends}, Online: {socialData.FriendsOnline}, In Game: {socialData.FriendsInGame}, Popular Game: {socialData.FriendsPopularGame}");
                }
                else
                {
                    _logger?.LogDebug("[SocialDataService] No friends data received from Steam API");
                    socialData.TotalFriends = 0;
                    socialData.FriendsOnline = 0;
                    socialData.FriendsInGame = 0;
                    socialData.FriendsPopularGame = SocialConstants.NO_POPULAR_GAME;
                    socialData.FriendsActivity = new List<FriendActivity>();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("[SocialDataService] Error collecting friends data", ex);
                socialData.TotalFriends = 0;
                socialData.FriendsOnline = 0;
                socialData.FriendsInGame = 0;
                socialData.FriendsPopularGame = SocialConstants.ERROR_POPULAR_GAME;
                socialData.FriendsActivity = new List<FriendActivity>();
            }
        }

        /// <summary>
        /// Collects community features data (placeholder for future expansion)
        /// </summary>
        private Task CollectCommunityDataAsync(SocialData socialData)
        {
            try
            {
                _logger?.LogDebug("[SocialDataService] Collecting community data...");
                
                // Placeholder for community features like:
                // - Steam groups
                // - Community market activity
                // - Badges and trading cards
                // - Workshop subscriptions
                
                socialData.CommunityBadges = 0;
                socialData.CommunityGroups = 0;
                socialData.WorkshopItems = 0;
                
                _logger?.LogDebug("[SocialDataService] Community data collection completed (placeholder)");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[SocialDataService] Error collecting community data", ex);
                socialData.CommunityBadges = 0;
                socialData.CommunityGroups = 0;
                socialData.WorkshopItems = 0;
                return Task.CompletedTask;
            }
        }

        #endregion
    }
    
    /// <summary>
    /// Represents a friend's current activity on Steam
    /// </summary>
    public class FriendActivity
    {
        public string FriendName { get; set; } = string.Empty;
        public string CurrentGame { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }
    }

    /// <summary>
    /// Data model specifically for social and community information
    /// Contains friends status, community features, and social statistics
    /// </summary>
    public class SocialData
    {
        #region Core Properties
        
        public string? Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        
        #endregion

        #region Friends Properties
        
        /// <summary>
        /// Total number of Steam friends
        /// </summary>
        public int TotalFriends { get; set; }
        
        /// <summary>
        /// Number of friends currently online
        /// </summary>
        public int FriendsOnline { get; set; }
        
        /// <summary>
        /// Number of friends currently in a game
        /// </summary>
        public int FriendsInGame { get; set; }
        
        /// <summary>
        /// Most popular game among friends
        /// </summary>
        public string? FriendsPopularGame { get; set; }
        
        /// <summary>
        /// List of active friends and what they're doing
        /// </summary>
        public List<FriendActivity>? FriendsActivity { get; set; }
        
        #endregion

        #region Community Properties
        
        /// <summary>
        /// Number of Steam community badges
        /// </summary>
        public int CommunityBadges { get; set; }
        
        /// <summary>
        /// Number of Steam groups joined
        /// </summary>
        public int CommunityGroups { get; set; }
        
        /// <summary>
        /// Number of Workshop items subscribed
        /// </summary>
        public int WorkshopItems { get; set; }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Gets social activity level based on friends interaction
        /// </summary>
        public string GetSocialActivityLevel()
        {
            if (HasError) return "Unknown";
            if (FriendsInGame > SocialConstants.VERY_SOCIAL_FRIENDS_THRESHOLD) return "Very Social";
            if (FriendsInGame > SocialConstants.SOCIAL_FRIENDS_THRESHOLD) return "Social";
            if (FriendsOnline > 0) return "Connected";
            return "Solo";
        }
        
        /// <summary>
        /// Gets community engagement level
        /// </summary>
        public string GetCommunityEngagement()
        {
            var total = CommunityBadges + CommunityGroups + WorkshopItems;
            if (total > SocialConstants.HIGHLY_ENGAGED_THRESHOLD) return "Highly Engaged";
            if (total > SocialConstants.ACTIVE_ENGAGEMENT_THRESHOLD) return "Active";
            if (total > SocialConstants.CASUAL_ENGAGEMENT_THRESHOLD) return "Casual";
            return "Minimal";
        }
        
        #endregion
    }
}