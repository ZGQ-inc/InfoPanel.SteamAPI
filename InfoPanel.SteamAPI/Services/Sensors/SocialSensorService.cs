using System;
using System.Data;
using System.Linq;
using InfoPanel.Plugins;
using InfoPanel.SteamAPI.Models;
using InfoPanel.SteamAPI.Services.Monitoring;

namespace InfoPanel.SteamAPI.Services.Sensors
{
    /// <summary>
    /// Social Domain Sensor Service
    /// Updates InfoPanel sensors for social/friends data:
    /// - Friends count (total, online, in game)
    /// - Friends activity table
    /// - Popular game among friends
    /// </summary>
    public class SocialSensorService : IDisposable
    {
        private const string DOMAIN_NAME = "SOCIAL_SENSORS";
        
        // Configuration and services
        private readonly ConfigurationService _configService;
        private readonly EnhancedLoggingService? _enhancedLogger;
        
        // Thread safety
        private readonly object _sensorLock = new();
        
        // Social sensors (injected via constructor)
        private readonly PluginSensor _friendsOnlineSensor;
        private readonly PluginSensor _friendsInGameSensor;
        private readonly PluginSensor _totalFriendsCountSensor;
        private readonly PluginTable _friendsActivityTable;
        private readonly PluginText _friendsPopularGameSensor;
        
        private bool _disposed = false;
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public SocialSensorService(
            ConfigurationService configService,
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            PluginSensor totalFriendsCountSensor,
            PluginTable friendsActivityTable,
            PluginText friendsPopularGameSensor,
            EnhancedLoggingService? enhancedLogger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _friendsOnlineSensor = friendsOnlineSensor ?? throw new ArgumentNullException(nameof(friendsOnlineSensor));
            _friendsInGameSensor = friendsInGameSensor ?? throw new ArgumentNullException(nameof(friendsInGameSensor));
            _totalFriendsCountSensor = totalFriendsCountSensor ?? throw new ArgumentNullException(nameof(totalFriendsCountSensor));
            _friendsActivityTable = friendsActivityTable ?? throw new ArgumentNullException(nameof(friendsActivityTable));
            _friendsPopularGameSensor = friendsPopularGameSensor ?? throw new ArgumentNullException(nameof(friendsPopularGameSensor));
            _enhancedLogger = enhancedLogger;
            
            Console.WriteLine($"[{DOMAIN_NAME}] SocialSensorService initialized");
        }
        
        /// <summary>
        /// Subscribe to social monitoring events
        /// </summary>
        public void SubscribeToMonitoring(SocialMonitoringService socialMonitoring)
        {
            if (socialMonitoring == null)
                throw new ArgumentNullException(nameof(socialMonitoring));
            
            socialMonitoring.SocialDataUpdated += OnSocialDataUpdated;
            
            _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.SubscribeToMonitoring", "Subscribed to social monitoring events");
        }
        
        /// <summary>
        /// Unsubscribe from social monitoring events
        /// </summary>
        public void UnsubscribeFromMonitoring(SocialMonitoringService socialMonitoring)
        {
            if (socialMonitoring == null)
                return;
            
            socialMonitoring.SocialDataUpdated -= OnSocialDataUpdated;
            
            _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.UnsubscribeFromMonitoring", "Unsubscribed from social monitoring events");
        }
        
        /// <summary>
        /// Event handler for social data updates
        /// </summary>
        private void OnSocialDataUpdated(object? sender, SocialDataEventArgs e)
        {
            if (e?.SocialData == null)
            {
                _enhancedLogger?.LogWarning($"{DOMAIN_NAME}.OnSocialDataUpdated", "Received null social data");
                return;
            }
            
            try
            {
                UpdateSocialSensors(e.SocialData, e.SessionCache);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DOMAIN_NAME}] Error updating social sensors: {ex.Message}");
                
                _enhancedLogger?.LogError($"{DOMAIN_NAME}.OnSocialDataUpdated", "Failed to update sensors", ex);
            }
        }
        
        /// <summary>
        /// Update all social sensors with data from monitoring service
        /// </summary>
        private void UpdateSocialSensors(SocialData socialData, SessionDataCache sessionCache)
        {
            lock (_sensorLock)
            {
                try
                {
                    _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdateSocialSensors", "Updating social sensors", new
                    {
                        FriendsOnline = socialData.FriendsOnline,
                        FriendsInGame = socialData.FriendsInGame,
                        TotalFriends = socialData.TotalFriends
                    });
                    
                    // Update friends count sensors
                    _friendsOnlineSensor.Value = (float)socialData.FriendsOnline;
                    _friendsInGameSensor.Value = (float)socialData.FriendsInGame;
                    _totalFriendsCountSensor.Value = (float)socialData.TotalFriends;
                    
                    // Update popular game sensor
                    var popularGame = socialData.FriendsPopularGame ?? "None";
                    _friendsPopularGameSensor.Value = popularGame;
                    
                    // Build friends activity table
                    _friendsActivityTable.Value = BuildFriendsActivityTable(socialData);
                    
                    _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.UpdateSocialSensors", "Social sensors updated successfully", new
                    {
                        FriendsOnline = socialData.FriendsOnline,
                        FriendsInGame = socialData.FriendsInGame,
                        TotalFriends = socialData.TotalFriends,
                        PopularGame = popularGame,
                        FriendsActivityCount = socialData.FriendsActivity?.Count ?? 0
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DOMAIN_NAME}] Error updating sensors: {ex.Message}");
                    
                    _enhancedLogger?.LogError($"{DOMAIN_NAME}.UpdateSocialSensors", "Sensor update failed", ex);
                    
                    SetErrorState(ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Build friends activity table from social data
        /// CRITICAL: Must use PluginText objects (not plain strings) for InfoPanel table cells
        /// Columns: Friend (name), Status (online/offline), Playing (game), Last Online (time)
        /// </summary>
        private DataTable BuildFriendsActivityTable(SocialData socialData)
        {
            var dataTable = new DataTable();
            
            try
            {
                // Initialize columns - MUST use PluginText type for InfoPanel tables
                dataTable.Columns.Add("Friend", typeof(PluginText));
                dataTable.Columns.Add("Status", typeof(PluginText));
                dataTable.Columns.Add("Playing", typeof(PluginText));
                dataTable.Columns.Add("Last Online", typeof(PluginText));
                
                if (socialData.FriendsActivity != null && socialData.FriendsActivity.Count > 0)
                {
                    // Add friends to table
                    foreach (var friend in socialData.FriendsActivity)
                    {
                        var row = dataTable.NewRow();
                        
                        // Friend name - use FriendName as both ID and display text
                        var friendName = friend.FriendName ?? "Unknown";
                        row["Friend"] = new PluginText($"friend_{friendName}", friendName);
                        
                        // Status - online/offline/away
                        var status = friend.Status ?? "Unknown";
                        row["Status"] = new PluginText($"friend_status_{friendName}", status);
                        
                        // Currently playing game - leave blank if not playing
                        var gameText = friend.CurrentGame == "Not in game" ? "" : friend.CurrentGame ?? "";
                        row["Playing"] = new PluginText($"friend_game_{friendName}", gameText);
                        
                        // Last seen time - format as relative time
                        var lastOnlineText = FormatLastSeenTime(friend.LastSeen);
                        row["Last Online"] = new PluginText($"friend_since_{friendName}", lastOnlineText);
                        
                        dataTable.Rows.Add(row);
                    }
                    
                    _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.BuildFriendsActivityTable", "Built friends activity table", new
                    {
                        FriendsCount = dataTable.Rows.Count,
                        ColumnCount = dataTable.Columns.Count,
                        ColumnNames = string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))
                    });
                }
                else
                {
                    _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.BuildFriendsActivityTable", "No friends activity data available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DOMAIN_NAME}] Error building friends activity table: {ex.Message}");
                
                _enhancedLogger?.LogError($"{DOMAIN_NAME}.BuildFriendsActivityTable", "Failed to build table", ex);
            }
            
            return dataTable;
        }
        
        /// <summary>
        /// Formats the last seen time as a relative time string (e.g., "2 hours ago", "Online now")
        /// </summary>
        private static string FormatLastSeenTime(DateTime lastSeen)
        {
            var now = DateTime.UtcNow;
            var timeSince = now - lastSeen;
            
            // If last seen is in the future or very recent (< 5 minutes), consider them online now
            if (timeSince.TotalMinutes < 5)
            {
                return "Online now";
            }
            
            // Format based on time elapsed
            if (timeSince.TotalMinutes < 60)
            {
                var minutes = (int)timeSince.TotalMinutes;
                return $"{minutes} min{(minutes != 1 ? "s" : "")} ago";
            }
            else if (timeSince.TotalHours < 24)
            {
                var hours = (int)timeSince.TotalHours;
                return $"{hours} hour{(hours != 1 ? "s" : "")} ago";
            }
            else if (timeSince.TotalDays < 7)
            {
                var days = (int)timeSince.TotalDays;
                return $"{days} day{(days != 1 ? "s" : "")} ago";
            }
            else if (timeSince.TotalDays < 30)
            {
                var weeks = (int)(timeSince.TotalDays / 7);
                return $"{weeks} week{(weeks != 1 ? "s" : "")} ago";
            }
            else
            {
                var months = (int)(timeSince.TotalDays / 30);
                return $"{months} month{(months != 1 ? "s" : "")} ago";
            }
        }
        
        /// <summary>
        /// Set all social sensors to error state
        /// </summary>
        private void SetErrorState(string errorMessage)
        {
            try
            {
                _friendsOnlineSensor.Value = 0f;
                _friendsInGameSensor.Value = 0f;
                _totalFriendsCountSensor.Value = 0f;
                _friendsPopularGameSensor.Value = "Error";
                _friendsActivityTable.Value = new DataTable();
                
                _enhancedLogger?.LogError($"{DOMAIN_NAME}.SetErrorState", "Social sensors set to error state", null, new
                {
                    ErrorMessage = errorMessage
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DOMAIN_NAME}] Error setting error state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                Console.WriteLine($"[{DOMAIN_NAME}] SocialSensorService disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DOMAIN_NAME}] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
