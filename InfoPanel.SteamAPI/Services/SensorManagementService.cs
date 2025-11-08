using InfoPanel.Plugins;
using InfoPanel.SteamAPI.Models;
using System;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Manages Steam sensor updates with thread safety and proper data formatting
    /// Implements thread-safe sensor update patterns for InfoPanel Steam monitoring
    /// </summary>
    public class SensorManagementService
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly object _sensorLock = new();
        
        #endregion

        #region Constructor
        
        public SensorManagementService(ConfigurationService configService, FileLoggingService? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            
            _logger?.LogInfo("SensorManagementService initialized");
        }
        
        #endregion

        #region Steam Sensor Updates
        
        /// <summary>
        /// Updates all Steam sensors with new data in a thread-safe manner
        /// </summary>
        public void UpdateSteamSensors(
            PluginText playerNameSensor,
            PluginText onlineStatusSensor,
            PluginSensor steamLevelSensor,
            PluginText currentGameSensor,
            PluginSensor currentGamePlaytimeSensor,
            PluginSensor totalGamesSensor,
            PluginSensor totalPlaytimeSensor,
            PluginSensor recentPlaytimeSensor,
            PluginText statusSensor,
            PluginText detailsSensor,
            SteamData data)
        {
            if (data == null) 
            {
                _logger?.LogWarning("UpdateSteamSensors called with null data");
                return;
            }
            
            _logger?.LogDebug($"Updating Steam sensors - Player: {data.PlayerName}, Status: {data.Status}");
            
            lock (_sensorLock)
            {
                try
                {
                    if (data.HasError)
                    {
                        _logger?.LogError($"SteamData has error: {data.ErrorMessage}");
                        SetErrorState(playerNameSensor, onlineStatusSensor, steamLevelSensor,
                            currentGameSensor, currentGamePlaytimeSensor, totalGamesSensor,
                            totalPlaytimeSensor, recentPlaytimeSensor, statusSensor, detailsSensor,
                            data.ErrorMessage ?? "Unknown error");
                        return;
                    }
                    
                    // Update profile sensors
                    _logger?.LogDebug("Updating profile sensors...");
                    UpdateProfileSensors(playerNameSensor, onlineStatusSensor, steamLevelSensor, data);
                    
                    // Update current game sensors
                    _logger?.LogDebug("Updating current game sensors...");
                    UpdateCurrentGameSensors(currentGameSensor, currentGamePlaytimeSensor, data);
                    
                    // Update library statistics sensors
                    _logger?.LogDebug("Updating library sensors...");
                    UpdateLibrarySensors(totalGamesSensor, totalPlaytimeSensor, recentPlaytimeSensor, data);
                    
                    // Update status sensors
                    _logger?.LogDebug("Updating status sensors...");
                    UpdateStatusSensors(statusSensor, detailsSensor, data);
                    
                    _logger?.LogInfo($"Successfully updated all Steam sensors for {data.PlayerName}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Error updating Steam sensors", ex);
                    Console.WriteLine($"[SensorManagementService] Error updating Steam sensors: {ex.Message}");
                    
                    // Set error state for all sensors
                    SetErrorState(playerNameSensor, onlineStatusSensor, steamLevelSensor,
                        currentGameSensor, currentGamePlaytimeSensor, totalGamesSensor,
                        totalPlaytimeSensor, recentPlaytimeSensor, statusSensor, detailsSensor,
                        ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Updates Steam profile-related sensors
        /// </summary>
        private void UpdateProfileSensors(
            PluginText playerNameSensor,
            PluginText onlineStatusSensor,
            PluginSensor steamLevelSensor,
            SteamData data)
        {
            // Update player name
            var playerName = !string.IsNullOrEmpty(data.PlayerName) ? data.PlayerName : "Unknown Player";
            playerNameSensor.Value = playerName;
            _logger?.LogDebug($"Player Name Sensor: '{playerName}'");
            
            // Update online status
            var onlineStatus = data.GetDisplayStatus();
            onlineStatusSensor.Value = onlineStatus;
            _logger?.LogDebug($"Online Status Sensor: '{onlineStatus}'");
            
            // Update Steam level
            steamLevelSensor.Value = data.SteamLevel;
            _logger?.LogDebug($"Steam Level Sensor: {data.SteamLevel}");
        }
        
        /// <summary>
        /// Updates current game-related sensors
        /// </summary>
        private void UpdateCurrentGameSensors(
            PluginText currentGameSensor,
            PluginSensor currentGamePlaytimeSensor,
            SteamData data)
        {
            // Update current game
            if (data.IsInGame() && !string.IsNullOrEmpty(data.CurrentGameName))
            {
                currentGameSensor.Value = data.CurrentGameName;
                currentGamePlaytimeSensor.Value = (float)Math.Round(data.TotalPlaytimeHours, 1);
                _logger?.LogDebug($"Current Game Sensor: '{data.CurrentGameName}' ({data.TotalPlaytimeHours:F1}h)");
            }
            else
            {
                currentGameSensor.Value = "Not Playing";
                currentGamePlaytimeSensor.Value = 0;
                _logger?.LogDebug($"Current Game Sensor: 'Not Playing' (0h)");
            }
        }
        
        /// <summary>
        /// Updates library statistics sensors
        /// </summary>
        private void UpdateLibrarySensors(
            PluginSensor totalGamesSensor,
            PluginSensor totalPlaytimeSensor,
            PluginSensor recentPlaytimeSensor,
            SteamData data)
        {
            // Update total games owned
            totalGamesSensor.Value = (float)data.TotalGamesOwned;
            _logger?.LogDebug($"Total Games Sensor: {data.TotalGamesOwned}");
            
            // Update total playtime
            var totalPlaytime = (float)Math.Round(data.TotalLibraryPlaytimeHours, 1);
            totalPlaytimeSensor.Value = totalPlaytime;
            _logger?.LogDebug($"Total Playtime Sensor: {totalPlaytime}h");
            
            // Update recent playtime
            var recentPlaytime = (float)Math.Round(data.RecentPlaytimeHours, 1);
            recentPlaytimeSensor.Value = recentPlaytime;
            _logger?.LogDebug($"Recent Playtime Sensor: {recentPlaytime}h");
        }
        
        /// <summary>
        /// Updates status and details sensors
        /// </summary>
        private void UpdateStatusSensors(
            PluginText statusSensor,
            PluginText detailsSensor,
            SteamData data)
        {
            // Update status
            var status = FormatSteamStatus(data);
            statusSensor.Value = status;
            _logger?.LogDebug($"Status Sensor: '{status}'");
            
            // Update details
            var details = FormatSteamDetails(data);
            detailsSensor.Value = details;
            _logger?.LogDebug($"Details Sensor: '{details}'");
        }
        
        #endregion

        #region Steam-Specific Formatting
        
        /// <summary>
        /// Formats the Steam status text based on data state
        /// </summary>
        private string FormatSteamStatus(SteamData data)
        {
            if (data.HasError)
            {
                return "Error";
            }
            
            if (data.IsInGame())
            {
                return $"Playing {data.CurrentGameName}";
            }
            
            if (data.IsOnline())
            {
                return data.OnlineState ?? "Online";
            }
            
            return "Offline";
        }
        
        /// <summary>
        /// Formats detailed Steam information text
        /// </summary>
        private string FormatSteamDetails(SteamData data)
        {
            if (data.HasError)
            {
                return $"Error: {data.Details}";
            }
            
            var details = $"Level {data.SteamLevel}";
            
            if (data.TotalGamesOwned > 0)
            {
                details += $" • {data.TotalGamesOwned:F0} games";
            }
            
            if (data.TotalLibraryPlaytimeHours > 0)
            {
                details += $" • {data.TotalLibraryPlaytimeHours:F0}h total";
            }
            
            if (data.RecentPlaytimeHours > 0)
            {
                details += $" • {data.RecentPlaytimeHours:F1}h recent";
            }
            
            details += $" • Updated: {data.Timestamp:HH:mm:ss}";
            
            return details;
        }
        
        #endregion

        #region Error Handling
        
        /// <summary>
        /// Sets error state for all Steam sensors when data collection fails
        /// </summary>
        private void SetErrorState(
            PluginText playerNameSensor,
            PluginText onlineStatusSensor,
            PluginSensor steamLevelSensor,
            PluginText currentGameSensor,
            PluginSensor currentGamePlaytimeSensor,
            PluginSensor totalGamesSensor,
            PluginSensor totalPlaytimeSensor,
            PluginSensor recentPlaytimeSensor,
            PluginText statusSensor,
            PluginText detailsSensor,
            string errorMessage)
        {
            try
            {
                playerNameSensor.Value = "Error";
                onlineStatusSensor.Value = "Offline";
                steamLevelSensor.Value = 0;
                currentGameSensor.Value = "Not Playing";
                currentGamePlaytimeSensor.Value = 0;
                totalGamesSensor.Value = 0;
                totalPlaytimeSensor.Value = 0;
                recentPlaytimeSensor.Value = 0;
                statusSensor.Value = "Error";
                detailsSensor.Value = $"Steam data collection failed: {errorMessage}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SensorManagementService] Error setting error state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resets all Steam sensors to default/empty state
        /// </summary>
        public void ResetSteamSensors(
            PluginText playerNameSensor,
            PluginText onlineStatusSensor,
            PluginSensor steamLevelSensor,
            PluginText currentGameSensor,
            PluginSensor currentGamePlaytimeSensor,
            PluginSensor totalGamesSensor,
            PluginSensor totalPlaytimeSensor,
            PluginSensor recentPlaytimeSensor,
            PluginText statusSensor,
            PluginText detailsSensor)
        {
            lock (_sensorLock)
            {
                try
                {
                    playerNameSensor.Value = "Loading...";
                    onlineStatusSensor.Value = "Offline";
                    steamLevelSensor.Value = 0;
                    currentGameSensor.Value = "Not Playing";
                    currentGamePlaytimeSensor.Value = 0;
                    totalGamesSensor.Value = 0;
                    totalPlaytimeSensor.Value = 0;
                    recentPlaytimeSensor.Value = 0;
                    statusSensor.Value = "Initializing...";
                    detailsSensor.Value = "Loading Steam data...";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SensorManagementService] Error resetting Steam sensors: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Updates Phase 2 Enhanced Gaming sensors with new data in a thread-safe manner
        /// </summary>
        public void UpdateEnhancedGamingSensors(
            // Recent Gaming Activity sensors
            PluginSensor recentGamesCountSensor,
            PluginText mostPlayedRecentSensor,
            PluginSensor recentSessionsSensor,
            // Session Time Tracking sensors
            PluginSensor currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginSensor averageSessionTimeSensor,
            // Friends Online Monitoring sensors
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            PluginText friendsCurrentGameSensor,
            // Achievement Tracking sensors
            PluginSensor currentGameAchievementsSensor,
            PluginSensor currentGameAchievementsUnlockedSensor,
            PluginSensor currentGameAchievementsTotalSensor,
            PluginText latestAchievementSensor,
            SteamData data)
        {
            if (data == null) return;
            
            lock (_sensorLock)
            {
                try
                {
                    if (data.HasError)
                    {
                        SetEnhancedGamingSensorsErrorState(
                            recentGamesCountSensor, mostPlayedRecentSensor, recentSessionsSensor,
                            currentSessionTimeSensor, sessionStartTimeSensor, averageSessionTimeSensor,
                            friendsOnlineSensor, friendsInGameSensor, friendsCurrentGameSensor,
                            currentGameAchievementsSensor, currentGameAchievementsUnlockedSensor,
                            currentGameAchievementsTotalSensor, latestAchievementSensor,
                            data.ErrorMessage ?? "Unknown error");
                        return;
                    }
                    
                    // Update recent gaming activity
                    UpdateRecentGamingActivitySensors(recentGamesCountSensor, mostPlayedRecentSensor, recentSessionsSensor, data);
                    
                    // Update session time tracking
                    UpdateSessionTimeSensors(currentSessionTimeSensor, sessionStartTimeSensor, averageSessionTimeSensor, data);
                    
                    // Update friends monitoring
                    UpdateFriendsMonitoringSensors(friendsOnlineSensor, friendsInGameSensor, friendsCurrentGameSensor, data);
                    
                    // Update achievement tracking
                    UpdateAchievementTrackingSensors(currentGameAchievementsSensor, currentGameAchievementsUnlockedSensor,
                        currentGameAchievementsTotalSensor, latestAchievementSensor, data);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SensorManagementService] Error updating Enhanced Gaming sensors: {ex.Message}");
                    
                    // Set error state for all Phase 2 sensors
                    SetEnhancedGamingSensorsErrorState(
                        recentGamesCountSensor, mostPlayedRecentSensor, recentSessionsSensor,
                        currentSessionTimeSensor, sessionStartTimeSensor, averageSessionTimeSensor,
                        friendsOnlineSensor, friendsInGameSensor, friendsCurrentGameSensor,
                        currentGameAchievementsSensor, currentGameAchievementsUnlockedSensor,
                        currentGameAchievementsTotalSensor, latestAchievementSensor,
                        ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Updates recent gaming activity sensors
        /// </summary>
        private void UpdateRecentGamingActivitySensors(
            PluginSensor recentGamesCountSensor,
            PluginText mostPlayedRecentSensor,
            PluginSensor recentSessionsSensor,
            SteamData data)
        {
            recentGamesCountSensor.Value = (float)data.RecentGamesCount;
            _logger?.LogDebug($"Recent Games Count Sensor: {data.RecentGamesCount}");
            
            var mostPlayedRecent = data.MostPlayedRecentGame ?? "None";
            mostPlayedRecentSensor.Value = mostPlayedRecent;
            _logger?.LogDebug($"Most Played Recent Sensor: '{mostPlayedRecent}'");
            
            recentSessionsSensor.Value = (float)data.RecentGameSessions;
            _logger?.LogDebug($"Recent Sessions Sensor: {data.RecentGameSessions}");
        }
        
        /// <summary>
        /// Updates session time tracking sensors
        /// </summary>
        private void UpdateSessionTimeSensors(
            PluginSensor currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginSensor averageSessionTimeSensor,
            SteamData data)
        {
            currentSessionTimeSensor.Value = (float)data.CurrentSessionTimeMinutes;
            _logger?.LogDebug($"Current Session Time Sensor: {data.CurrentSessionTimeMinutes} minutes");
            
            var sessionStartTime = data.SessionStartTime?.ToString("HH:mm") ?? "Not in game";
            sessionStartTimeSensor.Value = sessionStartTime;
            _logger?.LogDebug($"Session Start Time Sensor: '{sessionStartTime}'");
            
            var avgSessionTime = (float)Math.Round(data.AverageSessionTimeMinutes, 1);
            averageSessionTimeSensor.Value = avgSessionTime;
            _logger?.LogDebug($"Average Session Time Sensor: {avgSessionTime} minutes");
        }
        
        /// <summary>
        /// Updates friends monitoring sensors
        /// </summary>
        private void UpdateFriendsMonitoringSensors(
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            PluginText friendsCurrentGameSensor,
            SteamData data)
        {
            friendsOnlineSensor.Value = (float)data.FriendsOnline;
            _logger?.LogDebug($"Friends Online Sensor: {data.FriendsOnline}");
            
            friendsInGameSensor.Value = (float)data.FriendsInGame;
            _logger?.LogDebug($"Friends In Game Sensor: {data.FriendsInGame}");
            
            var friendsPopularGame = data.FriendsPopularGame ?? "None";
            friendsCurrentGameSensor.Value = friendsPopularGame;
            _logger?.LogDebug($"Friends Current Game Sensor: '{friendsPopularGame}'");
        }
        
        /// <summary>
        /// Updates achievement tracking sensors
        /// </summary>
        private void UpdateAchievementTrackingSensors(
            PluginSensor currentGameAchievementsSensor,
            PluginSensor currentGameAchievementsUnlockedSensor,
            PluginSensor currentGameAchievementsTotalSensor,
            PluginText latestAchievementSensor,
            SteamData data)
        {
            var achievementPercentage = (float)Math.Round(data.CurrentGameAchievementPercentage, 1);
            currentGameAchievementsSensor.Value = achievementPercentage;
            _logger?.LogDebug($"Current Game Achievements Sensor: {achievementPercentage}%");
            
            currentGameAchievementsUnlockedSensor.Value = (float)data.CurrentGameAchievementsUnlocked;
            _logger?.LogDebug($"Current Game Achievements Unlocked Sensor: {data.CurrentGameAchievementsUnlocked}");
            
            currentGameAchievementsTotalSensor.Value = (float)data.CurrentGameAchievementsTotal;
            _logger?.LogDebug($"Current Game Achievements Total Sensor: {data.CurrentGameAchievementsTotal}");
            
            string latestAchievement;
            if (data.LatestAchievementDate.HasValue && data.LatestAchievementDate.Value > DateTime.Now.AddDays(-7))
            {
                latestAchievement = data.LatestAchievementName ?? "None";
            }
            else
            {
                latestAchievement = "None recent";
            }
            latestAchievementSensor.Value = latestAchievement;
            _logger?.LogDebug($"Latest Achievement Sensor: '{latestAchievement}'");
        }
        
        /// <summary>
        /// Sets error state for all Phase 2 Enhanced Gaming sensors
        /// </summary>
        private void SetEnhancedGamingSensorsErrorState(
            PluginSensor recentGamesCountSensor,
            PluginText mostPlayedRecentSensor,
            PluginSensor recentSessionsSensor,
            PluginSensor currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginSensor averageSessionTimeSensor,
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            PluginText friendsCurrentGameSensor,
            PluginSensor currentGameAchievementsSensor,
            PluginSensor currentGameAchievementsUnlockedSensor,
            PluginSensor currentGameAchievementsTotalSensor,
            PluginText latestAchievementSensor,
            string errorMessage)
        {
            try
            {
                // Set error state for recent gaming activity
                recentGamesCountSensor.Value = 0;
                mostPlayedRecentSensor.Value = $"Error: {errorMessage}";
                recentSessionsSensor.Value = 0;
                
                // Set error state for session time tracking
                currentSessionTimeSensor.Value = 0;
                sessionStartTimeSensor.Value = "Error";
                averageSessionTimeSensor.Value = 0;
                
                // Set error state for friends monitoring
                friendsOnlineSensor.Value = 0;
                friendsInGameSensor.Value = 0;
                friendsCurrentGameSensor.Value = "Error";
                
                // Set error state for achievement tracking
                currentGameAchievementsSensor.Value = 0;
                currentGameAchievementsUnlockedSensor.Value = 0;
                currentGameAchievementsTotalSensor.Value = 0;
                latestAchievementSensor.Value = "Error";
                
                Console.WriteLine($"[SensorManagementService] Enhanced Gaming sensors set to error state: {errorMessage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SensorManagementService] Error setting Enhanced Gaming sensors error state: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates Phase 3 Advanced Features sensors with new data in a thread-safe manner
        /// </summary>
        public void UpdateAdvancedFeaturesSensors(
            // Detailed Game-Specific Statistics sensors
            PluginText primaryGameStatsSensor,
            PluginText secondaryGameStatsSensor,
            PluginText tertiaryGameStatsSensor,
            // Multiple Game Monitoring sensors
            PluginSensor monitoredGamesCountSensor,
            PluginSensor monitoredGamesTotalHoursSensor,
            PluginSensor monitoredGamesAvgRatingSensor,
            // Achievement Completion Tracking sensors
            PluginSensor overallAchievementCompletionSensor,
            PluginSensor perfectGamesCountSensor,
            PluginSensor totalAchievementsUnlockedSensor,
            PluginSensor achievementCompletionRankSensor,
            // News and Update Monitoring sensors
            PluginText latestGameNewsSensor,
            PluginSensor unreadNewsCountSensor,
            PluginText mostActiveNewsGameSensor,
            SteamData data)
        {
            if (data == null) return;
            
            lock (_sensorLock)
            {
                try
                {
                    if (data.HasError)
                    {
                        SetAdvancedFeaturesSensorsErrorState(
                            primaryGameStatsSensor, secondaryGameStatsSensor, tertiaryGameStatsSensor,
                            monitoredGamesCountSensor, monitoredGamesTotalHoursSensor, monitoredGamesAvgRatingSensor,
                            overallAchievementCompletionSensor, perfectGamesCountSensor, totalAchievementsUnlockedSensor,
                            achievementCompletionRankSensor, latestGameNewsSensor, unreadNewsCountSensor,
                            mostActiveNewsGameSensor, data.ErrorMessage ?? "Unknown error");
                        return;
                    }
                    
                    // Update detailed game-specific statistics
                    UpdateDetailedGameStatsSensors(primaryGameStatsSensor, secondaryGameStatsSensor, tertiaryGameStatsSensor, data);
                    
                    // Update multiple game monitoring
                    UpdateMultipleGameMonitoringSensors(monitoredGamesCountSensor, monitoredGamesTotalHoursSensor, monitoredGamesAvgRatingSensor, data);
                    
                    // Update achievement completion tracking
                    UpdateAchievementCompletionTrackingSensors(overallAchievementCompletionSensor, perfectGamesCountSensor,
                        totalAchievementsUnlockedSensor, achievementCompletionRankSensor, data);
                    
                    // Update news and update monitoring
                    UpdateNewsMonitoringSensors(latestGameNewsSensor, unreadNewsCountSensor, mostActiveNewsGameSensor, data);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SensorManagementService] Error updating Advanced Features sensors: {ex.Message}");
                    
                    // Set error state for all Phase 3 sensors
                    SetAdvancedFeaturesSensorsErrorState(
                        primaryGameStatsSensor, secondaryGameStatsSensor, tertiaryGameStatsSensor,
                        monitoredGamesCountSensor, monitoredGamesTotalHoursSensor, monitoredGamesAvgRatingSensor,
                        overallAchievementCompletionSensor, perfectGamesCountSensor, totalAchievementsUnlockedSensor,
                        achievementCompletionRankSensor, latestGameNewsSensor, unreadNewsCountSensor,
                        mostActiveNewsGameSensor, ex.Message);
                }
            }
        }
        
        #endregion

        #region Phase 3: Advanced Features Sensor Updates
        
        /// <summary>
        /// Updates detailed game-specific statistics sensors
        /// </summary>
        private void UpdateDetailedGameStatsSensors(
            PluginText primaryGameStatsSensor,
            PluginText secondaryGameStatsSensor,
            PluginText tertiaryGameStatsSensor,
            SteamData data)
        {
            primaryGameStatsSensor.Value = data.PrimaryGameStats ?? "No data";
            _logger?.LogDebug($"Primary Game Stats Sensor: '{data.PrimaryGameStats ?? "None"}'");
            
            secondaryGameStatsSensor.Value = data.SecondaryGameStats ?? "No data";
            _logger?.LogDebug($"Secondary Game Stats Sensor: '{data.SecondaryGameStats ?? "None"}'");
            
            tertiaryGameStatsSensor.Value = data.TertiaryGameStats ?? "No data";
            _logger?.LogDebug($"Tertiary Game Stats Sensor: '{data.TertiaryGameStats ?? "None"}'");
        }
        
        /// <summary>
        /// Updates multiple game monitoring sensors
        /// </summary>
        private void UpdateMultipleGameMonitoringSensors(
            PluginSensor monitoredGamesCountSensor,
            PluginSensor monitoredGamesTotalHoursSensor,
            PluginSensor monitoredGamesAvgRatingSensor,
            SteamData data)
        {
            monitoredGamesCountSensor.Value = (float)data.MonitoredGamesCount;
            _logger?.LogDebug($"Monitored Games Count Sensor: {data.MonitoredGamesCount} games");
            
            var totalHours = (float)Math.Round(data.MonitoredGamesTotalHours, 1);
            monitoredGamesTotalHoursSensor.Value = totalHours;
            _logger?.LogDebug($"Monitored Games Total Hours Sensor: {totalHours}h");
            
            var avgRating = (float)Math.Round(data.MonitoredGamesAverageRating, 1);
            monitoredGamesAvgRatingSensor.Value = avgRating;
            _logger?.LogDebug($"Monitored Games Average Rating Sensor: {avgRating}★");
        }
        
        /// <summary>
        /// Updates achievement completion tracking sensors
        /// </summary>
        private void UpdateAchievementCompletionTrackingSensors(
            PluginSensor overallAchievementCompletionSensor,
            PluginSensor perfectGamesCountSensor,
            PluginSensor totalAchievementsUnlockedSensor,
            PluginSensor achievementCompletionRankSensor,
            SteamData data)
        {
            var overallCompletion = (float)Math.Round(data.OverallAchievementCompletion, 1);
            overallAchievementCompletionSensor.Value = overallCompletion;
            _logger?.LogDebug($"Overall Achievement Completion Sensor: {overallCompletion}%");
            
            perfectGamesCountSensor.Value = (float)data.PerfectGamesCount;
            _logger?.LogDebug($"Perfect Games Count Sensor: {data.PerfectGamesCount} games");
            
            totalAchievementsUnlockedSensor.Value = (float)data.TotalAchievementsUnlocked;
            _logger?.LogDebug($"Total Achievements Unlocked Sensor: {data.TotalAchievementsUnlocked}");
            
            var completionRank = (float)Math.Round(data.AchievementCompletionRank, 1);
            achievementCompletionRankSensor.Value = completionRank;
            _logger?.LogDebug($"Achievement Completion Rank Sensor: {completionRank}%ile");
        }
        
        /// <summary>
        /// Updates news and update monitoring sensors
        /// </summary>
        private void UpdateNewsMonitoringSensors(
            PluginText latestGameNewsSensor,
            PluginSensor unreadNewsCountSensor,
            PluginText mostActiveNewsGameSensor,
            SteamData data)
        {
            latestGameNewsSensor.Value = data.LatestGameNews ?? "No news";
            _logger?.LogDebug($"Latest Game News Sensor: '{data.LatestGameNews ?? "None"}'");
            
            unreadNewsCountSensor.Value = (float)data.UnreadNewsCount;
            _logger?.LogDebug($"Unread News Count Sensor: {data.UnreadNewsCount} items");
            
            mostActiveNewsGameSensor.Value = data.MostActiveNewsGame ?? "None";
            _logger?.LogDebug($"Most Active News Game Sensor: '{data.MostActiveNewsGame ?? "None"}'");
        }
        
        /// <summary>
        /// Sets error state for all Phase 3 Advanced Features sensors
        /// </summary>
        private void SetAdvancedFeaturesSensorsErrorState(
            PluginText primaryGameStatsSensor,
            PluginText secondaryGameStatsSensor,
            PluginText tertiaryGameStatsSensor,
            PluginSensor monitoredGamesCountSensor,
            PluginSensor monitoredGamesTotalHoursSensor,
            PluginSensor monitoredGamesAvgRatingSensor,
            PluginSensor overallAchievementCompletionSensor,
            PluginSensor perfectGamesCountSensor,
            PluginSensor totalAchievementsUnlockedSensor,
            PluginSensor achievementCompletionRankSensor,
            PluginText latestGameNewsSensor,
            PluginSensor unreadNewsCountSensor,
            PluginText mostActiveNewsGameSensor,
            string errorMessage)
        {
            // Set text sensors to error state
            primaryGameStatsSensor.Value = $"Error: {errorMessage}";
            secondaryGameStatsSensor.Value = $"Error: {errorMessage}";
            tertiaryGameStatsSensor.Value = $"Error: {errorMessage}";
            latestGameNewsSensor.Value = $"Error: {errorMessage}";
            mostActiveNewsGameSensor.Value = $"Error: {errorMessage}";
            
            // Set numeric sensors to zero
            monitoredGamesCountSensor.Value = 0f;
            monitoredGamesTotalHoursSensor.Value = 0f;
            monitoredGamesAvgRatingSensor.Value = 0f;
            overallAchievementCompletionSensor.Value = 0f;
            perfectGamesCountSensor.Value = 0f;
            totalAchievementsUnlockedSensor.Value = 0f;
            achievementCompletionRankSensor.Value = 0f;
            unreadNewsCountSensor.Value = 0f;
            
            _logger?.LogError($"Set Advanced Features sensors to error state: {errorMessage}");
        }

        /// <summary>
        /// Updates Social & Community Features sensors with Phase 4 data
        /// </summary>
        public void UpdateSocialFeaturesSensors(
            // Friends Activity sensors
            PluginSensor totalFriendsCountSensor,
            PluginSensor recentlyActiveFriendsCountSensor,
            PluginText friendActivityStatusSensor,
            PluginText mostActiveFriendSensor,
            // Friend Network Games sensors
            PluginText trendingFriendGameSensor,
            PluginSensor friendNetworkGameCountSensor,
            PluginText topFriendGameSensor,
            // Community Badge sensors
            PluginSensor totalBadgesEarnedSensor,
            PluginSensor totalBadgeXPSensor,
            PluginText latestBadgeSensor,
            PluginSensor badgeCompletionRateSensor,
            // Global Statistics sensors
            PluginSensor globalPlaytimePercentileSensor,
            PluginText globalUserCategorySensor,
            SteamData data)
        {
            if (data == null)
            {
                _logger?.LogWarning("SteamData is null in UpdateSocialFeaturesSensors");
                SetSocialFeaturesSensorsToError(totalFriendsCountSensor, recentlyActiveFriendsCountSensor, friendActivityStatusSensor,
                    mostActiveFriendSensor, trendingFriendGameSensor, friendNetworkGameCountSensor, topFriendGameSensor,
                    totalBadgesEarnedSensor, totalBadgeXPSensor, latestBadgeSensor, badgeCompletionRateSensor,
                    globalPlaytimePercentileSensor, globalUserCategorySensor, "No data available");
                return;
            }

            if (data.HasError)
            {
                _logger?.LogWarning($"SteamData has error in UpdateSocialFeaturesSensors: {data.ErrorMessage}");
                SetSocialFeaturesSensorsToError(totalFriendsCountSensor, recentlyActiveFriendsCountSensor, friendActivityStatusSensor,
                    mostActiveFriendSensor, trendingFriendGameSensor, friendNetworkGameCountSensor, topFriendGameSensor,
                    totalBadgesEarnedSensor, totalBadgeXPSensor, latestBadgeSensor, badgeCompletionRateSensor,
                    globalPlaytimePercentileSensor, globalUserCategorySensor, data.ErrorMessage ?? "Unknown error");
                return;
            }

            lock (_sensorLock)
            {
                try
                {
                    // Update Friends Activity sensors
                    totalFriendsCountSensor.Value = data.TotalFriendsCount;
                    recentlyActiveFriendsCountSensor.Value = data.RecentlyActiveFriends;
                    
                    // Format friend activity status based on recently active count
                    var friendActivityText = data.RecentlyActiveFriends switch
                    {
                        0 => "No recent activity",
                        1 => "1 friend recently active",
                        _ => $"{data.RecentlyActiveFriends} friends recently active"
                    };
                    friendActivityStatusSensor.Value = friendActivityText;
                    
                    mostActiveFriendSensor.Value = !string.IsNullOrEmpty(data.MostActiveFriend) ? data.MostActiveFriend : "None";

                    // Update Friend Network Games sensors
                    trendingFriendGameSensor.Value = !string.IsNullOrEmpty(data.TrendingFriendGame) ? data.TrendingFriendGame : "None";
                    
                    // Count of popular games from the list, fallback to calculated overlap percentage
                    var friendGameCount = data.PopularFriendGames?.Count ?? (int)Math.Round(data.FriendsGameOverlapPercentage / 10.0);
                    friendNetworkGameCountSensor.Value = friendGameCount;
                    
                    topFriendGameSensor.Value = !string.IsNullOrEmpty(data.MostOwnedFriendGame) ? data.MostOwnedFriendGame : "None";

                    // Update Community Badge sensors
                    totalBadgesEarnedSensor.Value = data.TotalBadgesEarned;
                    totalBadgeXPSensor.Value = data.TotalBadgeXP;
                    latestBadgeSensor.Value = !string.IsNullOrEmpty(data.NextBadgeProgress) ? data.NextBadgeProgress : "None";
                    
                    // Calculate badge completion rate from available data (placeholder calculation)
                    var badgeCompletionRate = data.TotalBadgesEarned > 0 ? Math.Min(100.0, data.TotalBadgesEarned * 0.5) : 0.0;
                    badgeCompletionRateSensor.Value = (float)Math.Round(badgeCompletionRate, 1);

                    // Update Global Statistics sensors
                    globalPlaytimePercentileSensor.Value = (float)Math.Round(data.GlobalPlaytimePercentile, 1);
                    globalUserCategorySensor.Value = !string.IsNullOrEmpty(data.GlobalUserCategory) ? data.GlobalUserCategory : "Unknown";

                    _logger?.LogDebug("Updated Social & Community Features sensors successfully");
                    _logger?.LogInfo($"Social Features - Friends: {data.TotalFriendsCount} ({data.RecentlyActiveFriends} active), " +
                                   $"Badges: {data.TotalBadgesEarned} ({data.TotalBadgeXP} XP), " +
                                   $"Global Percentile: {data.GlobalPlaytimePercentile:F1}%");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Error updating Social & Community Features sensors", ex);
                    Console.WriteLine($"[SensorManagementService] Error updating social features sensors: {ex.Message}");
                    SetSocialFeaturesSensorsToError(totalFriendsCountSensor, recentlyActiveFriendsCountSensor, friendActivityStatusSensor,
                        mostActiveFriendSensor, trendingFriendGameSensor, friendNetworkGameCountSensor, topFriendGameSensor,
                        totalBadgesEarnedSensor, totalBadgeXPSensor, latestBadgeSensor, badgeCompletionRateSensor,
                        globalPlaytimePercentileSensor, globalUserCategorySensor, ex.Message);
                }
            }
        }

        /// <summary>
        /// Sets all Social & Community Features sensors to error state
        /// </summary>
        private void SetSocialFeaturesSensorsToError(
            PluginSensor totalFriendsCountSensor,
            PluginSensor recentlyActiveFriendsCountSensor,
            PluginText friendActivityStatusSensor,
            PluginText mostActiveFriendSensor,
            PluginText trendingFriendGameSensor,
            PluginSensor friendNetworkGameCountSensor,
            PluginText topFriendGameSensor,
            PluginSensor totalBadgesEarnedSensor,
            PluginSensor totalBadgeXPSensor,
            PluginText latestBadgeSensor,
            PluginSensor badgeCompletionRateSensor,
            PluginSensor globalPlaytimePercentileSensor,
            PluginText globalUserCategorySensor,
            string errorMessage)
        {
            // Friends Activity error values
            totalFriendsCountSensor.Value = 0f;
            recentlyActiveFriendsCountSensor.Value = 0f;
            friendActivityStatusSensor.Value = "Error";
            mostActiveFriendSensor.Value = "Error";
            
            // Friend Network Games error values
            trendingFriendGameSensor.Value = "Error";
            friendNetworkGameCountSensor.Value = 0f;
            topFriendGameSensor.Value = "Error";
            
            // Community Badge error values
            totalBadgesEarnedSensor.Value = 0f;
            totalBadgeXPSensor.Value = 0f;
            latestBadgeSensor.Value = "Error";
            badgeCompletionRateSensor.Value = 0f;
            
            // Global Statistics error values
            globalPlaytimePercentileSensor.Value = 0f;
            globalUserCategorySensor.Value = "Error";
            
            _logger?.LogError($"Set Social & Community Features sensors to error state: {errorMessage}");
        }
        
        #endregion
    }
}
