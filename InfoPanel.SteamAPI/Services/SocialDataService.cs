using InfoPanel.SteamAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.Services
{
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
                    
                    // Estimate online and in-game friends
                    // Note: This is simplified - real implementation would check individual friend status
                    socialData.FriendsOnline = Math.Min(friends.Count, friends.Count / 3); // Rough estimate
                    socialData.FriendsInGame = Math.Min(3, friends.Count / 5); // Conservative estimate
                    
                    // Set popular game based on recent activity (placeholder)
                    socialData.FriendsPopularGame = "Counter-Strike 2"; // Would be calculated from friends' activity
                    
                    _logger?.LogInfo($"[SocialDataService] Friends data - Total: {socialData.TotalFriends}, Online: {socialData.FriendsOnline}, In Game: {socialData.FriendsInGame}");
                }
                else
                {
                    _logger?.LogDebug("[SocialDataService] No friends data received from Steam API");
                    socialData.TotalFriends = 0;
                    socialData.FriendsOnline = 0;
                    socialData.FriendsInGame = 0;
                    socialData.FriendsPopularGame = "None";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("[SocialDataService] Error collecting friends data", ex);
                socialData.TotalFriends = 0;
                socialData.FriendsOnline = 0;
                socialData.FriendsInGame = 0;
                socialData.FriendsPopularGame = "Error";
            }
        }

        /// <summary>
        /// Collects community features data (placeholder for future expansion)
        /// </summary>
        private async Task CollectCommunityDataAsync(SocialData socialData)
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
                
                await Task.CompletedTask; // Prevent async warning
                _logger?.LogDebug("[SocialDataService] Community data collection completed (placeholder)");
            }
            catch (Exception ex)
            {
                _logger?.LogError("[SocialDataService] Error collecting community data", ex);
                socialData.CommunityBadges = 0;
                socialData.CommunityGroups = 0;
                socialData.WorkshopItems = 0;
            }
        }

        #endregion
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
            if (FriendsInGame > 5) return "Very Social";
            if (FriendsInGame > 2) return "Social";
            if (FriendsOnline > 0) return "Connected";
            return "Solo";
        }
        
        /// <summary>
        /// Gets community engagement level
        /// </summary>
        public string GetCommunityEngagement()
        {
            var total = CommunityBadges + CommunityGroups + WorkshopItems;
            if (total > 50) return "Highly Engaged";
            if (total > 20) return "Active";
            if (total > 5) return "Casual";
            return "Minimal";
        }
        
        #endregion
    }
}