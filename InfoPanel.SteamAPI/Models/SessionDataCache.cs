using System;
using InfoPanel.SteamAPI.Services;

namespace InfoPanel.SteamAPI.Models
{
    /// <summary>
    /// Shared cache for session data that needs to be preserved across domain updates.
    /// Player domain owns and updates this cache. Social and Library domains read from it.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class SessionDataCache
    {
        #region Current Session Data
        
        /// <summary>
        /// Current gaming session duration in minutes (rounded up)
        /// </summary>
        public int CurrentSessionMinutes { get; set; }
        
        /// <summary>
        /// Start time of the current gaming session (null if not in game)
        /// </summary>
        public DateTime? SessionStartTime { get; set; }
        
        #endregion

        #region Historical Session Data
        
        /// <summary>
        /// Average session duration across all recorded sessions in minutes
        /// </summary>
        public double AverageSessionMinutes { get; set; }
        
        #endregion

        #region Last Played Game Data
        
        /// <summary>
        /// Name of the last played game (for display when not currently playing)
        /// </summary>
        public string? LastPlayedGameName { get; set; }
        
        /// <summary>
        /// App ID of the last played game
        /// </summary>
        public int LastPlayedGameAppId { get; set; }
        
        /// <summary>
        /// Banner URL for the last played game
        /// </summary>
        public string? LastPlayedGameBannerUrl { get; set; }
        
        #endregion

        #region Metadata
        
        /// <summary>
        /// Timestamp when this cache was last updated by the player domain
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        #endregion

        #region Thread Safety
        
        /// <summary>
        /// Lock object for thread-safe access to cache properties.
        /// Use this when reading or writing multiple properties atomically.
        /// </summary>
        public object Lock { get; } = new object();
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new empty session data cache
        /// </summary>
        public SessionDataCache()
        {
            LastUpdated = DateTime.Now;
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Creates a snapshot of the current cache state (thread-safe)
        /// </summary>
        public SessionDataCache Clone()
        {
            lock (Lock)
            {
                return new SessionDataCache
                {
                    CurrentSessionMinutes = this.CurrentSessionMinutes,
                    SessionStartTime = this.SessionStartTime,
                    AverageSessionMinutes = this.AverageSessionMinutes,
                    LastPlayedGameName = this.LastPlayedGameName,
                    LastPlayedGameAppId = this.LastPlayedGameAppId,
                    LastPlayedGameBannerUrl = this.LastPlayedGameBannerUrl,
                    LastUpdated = this.LastUpdated
                };
            }
        }
        
        /// <summary>
        /// Updates all cache properties from player data (thread-safe)
        /// </summary>
        public void UpdateFromPlayerData(PlayerData playerData)
        {
            if (playerData == null) return;
            
            lock (Lock)
            {
                CurrentSessionMinutes = (int)Math.Ceiling(playerData.CurrentSessionTimeMinutes);
                SessionStartTime = playerData.CurrentSessionStartTime;
                AverageSessionMinutes = playerData.AverageSessionTimeMinutes;
                LastPlayedGameName = playerData.LastPlayedGameName;
                LastPlayedGameAppId = playerData.LastPlayedGameAppId;
                LastPlayedGameBannerUrl = playerData.LastPlayedGameBannerUrl;
                LastUpdated = DateTime.Now;
                
                // Debug logging to trace value propagation
                Console.WriteLine($"[SessionDataCache] Updated from PlayerData: CurrentSession={CurrentSessionMinutes}m, AvgSession={Math.Round(AverageSessionMinutes, 1)}m, LastGame={LastPlayedGameName ?? "None"}");
            }
        }
        
        #endregion
    }
}
