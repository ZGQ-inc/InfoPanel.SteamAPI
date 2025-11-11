using InfoPanel.Plugins;
using InfoPanel.SteamAPI.Models;
using System;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Constants for sensor management and data formatting
    /// </summary>
    public static class SensorManagementConstants
    {
        #region Rounding Precision
        public const int DECIMAL_PRECISION = 1;
        #endregion
        
        #region Default Sensor Values
        public const float DEFAULT_NUMERIC_SENSOR_VALUE = 0f;
        public const int DEFAULT_STEAM_LEVEL = 0;
        public const string DEFAULT_PLAYER_NAME_ERROR = "Error";
        public const string DEFAULT_PLAYER_NAME_LOADING = "Loading...";
        public const string DEFAULT_OFFLINE_STATUS = "Offline";
        public const string DEFAULT_NOT_PLAYING = "Not Playing";
        public const string DEFAULT_STATUS_ERROR = "Error";
        public const string DEFAULT_STATUS_INITIALIZING = "Initializing...";
        public const string DEFAULT_ACHIEVEMENT_NONE = "None";
        public const string DEFAULT_ACHIEVEMENT_NONE_RECENT = "None recent";
        #endregion
        
        #region Time Thresholds
        public const int RECENT_ACHIEVEMENT_DAYS = 7;
        #endregion
        
        #region Calculation Constants
        /// <summary>Time conversion: minutes per hour</summary>
        public const int MINUTES_PER_HOUR = 60;
        #endregion
        
        #region Validation Thresholds  
        public const int MINIMUM_GAMES_OWNED = 0;
        public const double MINIMUM_PLAYTIME_HOURS = 0.0;
        #endregion
    }

    /// <summary>
    /// Manages Steam sensor updates with thread safety and proper data formatting
    /// Implements thread-safe sensor update patterns for InfoPanel Steam monitoring
    /// </summary>
    public class SensorManagementService
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService? _logger;
        private readonly EnhancedLoggingService? _enhancedLogger;
        private readonly object _sensorLock = new();
        
        #endregion

        #region Constructor
        
        public SensorManagementService(ConfigurationService configService, FileLoggingService? logger = null, EnhancedLoggingService? enhancedLogger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            _enhancedLogger = enhancedLogger;
            
            // Enhanced logging for initialization
            _enhancedLogger?.LogInfo("SensorManagementService", "Service initialized", new
            {
                HasConfigService = _configService != null,
                EnhancedLoggingEnabled = _enhancedLogger != null
            });
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
            PluginText profileImageUrlSensor,
            PluginText currentGameBannerUrlSensor,
            SteamData data)
        {
            if (data == null) 
            {
                _enhancedLogger?.LogWarning("SensorManagementService.UpdateSteamSensors", "Called with null data");
                return;
            }
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateSteamSensors", "Updating Steam sensors", new
            {
                PlayerName = data.PlayerName,
                Status = data.Status,
                HasError = data.HasError
            });
            
            lock (_sensorLock)
            {
                try
                {
                    if (data.HasError)
                    {
                        _enhancedLogger?.LogError("SensorManagementService.UpdateSteamSensors", "SteamData contains error", null, new
                        {
                            ErrorMessage = data.ErrorMessage
                        });
                        SetErrorState(playerNameSensor, onlineStatusSensor, steamLevelSensor,
                            currentGameSensor, currentGamePlaytimeSensor, totalGamesSensor,
                            totalPlaytimeSensor, recentPlaytimeSensor, statusSensor, detailsSensor,
                            profileImageUrlSensor, currentGameBannerUrlSensor,
                            data.ErrorMessage ?? "Unknown error");
                        return;
                    }
                    
                    // Update profile sensors
                    _enhancedLogger?.LogDebug("SensorManagementService.UpdateSteamSensors", "Updating profile sensors");
                    UpdateProfileSensors(playerNameSensor, onlineStatusSensor, steamLevelSensor, data);
                    
                    // Update image URL sensors
                    _enhancedLogger?.LogDebug("SensorManagementService.UpdateSteamSensors", "Updating image URL sensors");
                    UpdateImageUrlSensors(profileImageUrlSensor, currentGameBannerUrlSensor, data);
                    
                    // Update current game sensors
                    _enhancedLogger?.LogDebug("SensorManagementService.UpdateSteamSensors", "Updating current game sensors");
                    UpdateCurrentGameSensors(currentGameSensor, currentGamePlaytimeSensor, data);
                    
                    // Update library statistics sensors
                    _enhancedLogger?.LogDebug("SensorManagementService.UpdateSteamSensors", "Updating library sensors");
                    UpdateLibrarySensors(totalGamesSensor, totalPlaytimeSensor, recentPlaytimeSensor, data);
                    
                    // Update status sensors
                    _enhancedLogger?.LogDebug("SensorManagementService.UpdateSteamSensors", "Updating status sensors");
                    UpdateStatusSensors(statusSensor, detailsSensor, data);
                    
                    _enhancedLogger?.LogInfo("SensorManagementService.UpdateSteamSensors", "Successfully updated all Steam sensors", new
                    {
                        PlayerName = data.PlayerName,
                        SensorsUpdated = 12
                    });
                }
                catch (Exception ex)
                {
                    _enhancedLogger?.LogError("SensorManagementService.UpdateSteamSensors", "Error updating Steam sensors", ex);
                    Console.WriteLine($"[SensorManagementService] Error updating Steam sensors: {ex.Message}");
                    
                    // Set error state for all sensors
                    SetErrorState(playerNameSensor, onlineStatusSensor, steamLevelSensor,
                        currentGameSensor, currentGamePlaytimeSensor, totalGamesSensor,
                        totalPlaytimeSensor, recentPlaytimeSensor, statusSensor, detailsSensor,
                        profileImageUrlSensor, currentGameBannerUrlSensor,
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
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateProfileSensors", "Player name sensor updated", new { PlayerName = playerName });
            
            // Update online status
            var onlineStatus = data.GetDisplayStatus();
            onlineStatusSensor.Value = onlineStatus;
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateProfileSensors", "Online status sensor updated", new { OnlineStatus = onlineStatus });
            
            // Update Steam level
            steamLevelSensor.Value = data.SteamLevel;
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateProfileSensors", "Steam level sensor updated", new { SteamLevel = data.SteamLevel });
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
                currentGamePlaytimeSensor.Value = (float)Math.Round(data.TotalPlaytimeHours, SensorManagementConstants.DECIMAL_PRECISION);
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateCurrentGameSensors", "Current game sensors updated", new
                {
                    GameName = data.CurrentGameName,
                    PlaytimeHours = Math.Round(data.TotalPlaytimeHours, 1)
                });
            }
            else
            {
                currentGameSensor.Value = SensorManagementConstants.DEFAULT_NOT_PLAYING;
                currentGamePlaytimeSensor.Value = SensorManagementConstants.DEFAULT_NUMERIC_SENSOR_VALUE;
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateCurrentGameSensors", "Not playing - sensors set to default", new
                {
                    GameName = SensorManagementConstants.DEFAULT_NOT_PLAYING,
                    PlaytimeHours = 0
                });
            }
        }
        
        /// <summary>
        /// Updates library statistics sensors
        /// </summary>
        public void UpdateLibrarySensors(
            PluginSensor totalGamesSensor,
            PluginSensor totalPlaytimeSensor,
            PluginSensor recentPlaytimeSensor,
            SteamData data)
        {
            // Only update library sensors when we have actual library data
            // If TotalGamesOwned is 0, this is likely player data without library info
            if (data.TotalGamesOwned > 0 || data.TotalLibraryPlaytimeHours > 0)
            {
                // Update total games owned
                totalGamesSensor.Value = (float)data.TotalGamesOwned;
                
                // Update total playtime
                var totalPlaytime = (float)Math.Round(data.TotalLibraryPlaytimeHours, SensorManagementConstants.DECIMAL_PRECISION);
                totalPlaytimeSensor.Value = totalPlaytime;
                
                // Update recent playtime
                var recentPlaytime = (float)Math.Round(data.RecentPlaytimeHours, SensorManagementConstants.DECIMAL_PRECISION);
                recentPlaytimeSensor.Value = recentPlaytime;
                
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateLibrarySensors", "Library sensors updated", new
                {
                    TotalGames = data.TotalGamesOwned,
                    TotalPlaytimeHours = totalPlaytime,
                    RecentPlaytimeHours = recentPlaytime
                });
            }
            else
            {
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateLibrarySensors", "Skipped - no library data available");
            }
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
            
            // Update details
            var details = FormatSteamDetails(data);
            detailsSensor.Value = details;
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateStatusSensors", "Status sensors updated", new
            {
                Status = status,
                Details = details
            });
        }
        
        /// <summary>
        /// Updates image URL sensors with profile image and game banner URLs
        /// </summary>
        private void UpdateImageUrlSensors(
            PluginText profileImageUrlSensor,
            PluginText currentGameBannerUrlSensor,
            SteamData data)
        {
            // Update profile image URL
            var profileImageUrl = data.ProfileImageUrl ?? "-";
            profileImageUrlSensor.Value = profileImageUrl;
            
            // Update current game banner URL
            var gameBannerUrl = data.CurrentGameBannerUrl ?? "-";
            currentGameBannerUrlSensor.Value = gameBannerUrl;
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateImageUrlSensors", "Image URL sensors updated", new
            {
                ProfileImageUrl = profileImageUrl,
                GameBannerUrl = gameBannerUrl
            });
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
            PluginText profileImageUrlSensor,
            PluginText currentGameBannerUrlSensor,
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
                profileImageUrlSensor.Value = "-";
                currentGameBannerUrlSensor.Value = "-";
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
        /// Updates Enhanced Gaming sensors with new data in a thread-safe manner
        /// </summary>
        public void UpdateEnhancedGamingSensors(
            // Recent Gaming Activity sensors
            PluginSensor recentGamesCountSensor,
            PluginText mostPlayedRecentSensor,
            PluginSensor recentSessionsSensor,
            // Session Time Tracking sensors
            PluginText currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginText averageSessionTimeSensor,
            // Friends Online Monitoring sensors
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
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
                            currentGameAchievementsSensor, currentGameAchievementsUnlockedSensor,
                            currentGameAchievementsTotalSensor, latestAchievementSensor,
                            data.ErrorMessage ?? "Unknown error");
                        return;
                    }
                    
                    // Update recent gaming activity
                    UpdateRecentGamingActivitySensors(recentGamesCountSensor, mostPlayedRecentSensor, recentSessionsSensor, data);
                    
                    // Update session time tracking
                    UpdateSessionTimeSensors(currentSessionTimeSensor, sessionStartTimeSensor, averageSessionTimeSensor, data);
                    
                    // Update achievement tracking
                    UpdateAchievementTrackingSensors(currentGameAchievementsSensor, currentGameAchievementsUnlockedSensor,
                        currentGameAchievementsTotalSensor, latestAchievementSensor, data);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SensorManagementService] Error updating Enhanced Gaming sensors: {ex.Message}");
                    
                    // Set error state for all Enhanced Gaming sensors
                    SetEnhancedGamingSensorsErrorState(
                        recentGamesCountSensor, mostPlayedRecentSensor, recentSessionsSensor,
                        currentSessionTimeSensor, sessionStartTimeSensor, averageSessionTimeSensor,
                        currentGameAchievementsSensor, currentGameAchievementsUnlockedSensor,
                        currentGameAchievementsTotalSensor, latestAchievementSensor,
                        ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Updates recent gaming activity sensors
        /// </summary>
        public void UpdateRecentGamingActivitySensors(
            PluginSensor recentGamesCountSensor,
            PluginText mostPlayedRecentSensor,
            PluginSensor recentSessionsSensor,
            SteamData data)
        {
            // Only update recent gaming sensors when we have actual recent games data
            // If RecentGamesCount is 0, this is likely player data without recent gaming info
            if (data.RecentGamesCount > 0 || !string.IsNullOrEmpty(data.MostPlayedRecentGame))
            {
                recentGamesCountSensor.Value = (float)data.RecentGamesCount;
                
                var mostPlayedRecent = data.MostPlayedRecentGame ?? "None";
                mostPlayedRecentSensor.Value = mostPlayedRecent;
                
                recentSessionsSensor.Value = (float)data.RecentGameSessions;
                
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateRecentGamingActivitySensors", "Recent gaming activity sensors updated", new
                {
                    RecentGamesCount = data.RecentGamesCount,
                    MostPlayedRecent = mostPlayedRecent,
                    RecentSessions = data.RecentGameSessions
                });
            }
            else
            {
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateRecentGamingActivitySensors", "Skipped - no recent games data available");
            }
        }
        
        /// <summary>
        /// Updates session time tracking sensors
        /// </summary>
        private void UpdateSessionTimeSensors(
            PluginText currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginText averageSessionTimeSensor,
            SteamData data)
        {
            // Format current session time
            var currentSessionFormatted = FormatMinutesToHourMin(data.CurrentSessionTimeMinutes);
            currentSessionTimeSensor.Value = currentSessionFormatted;
            
            var sessionStartTime = data.SessionStartTime?.ToString("HH:mm") ?? "Not in game";
            sessionStartTimeSensor.Value = sessionStartTime;
            
            // Format average session time
            var avgSessionFormatted = FormatMinutesToHourMin((int)Math.Round(data.AverageSessionTimeMinutes));
            averageSessionTimeSensor.Value = avgSessionFormatted;
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateSessionTimeSensors", "Session time sensors updated", new
            {
                CurrentSessionTime = currentSessionFormatted,
                CurrentSessionMinutes = data.CurrentSessionTimeMinutes,
                SessionStartTime = sessionStartTime,
                AverageSessionTime = avgSessionFormatted,
                AverageSessionMinutes = Math.Round(data.AverageSessionTimeMinutes, 1)
            });
        }
        
        /// <summary>
        /// Updates friends monitoring sensors
        /// </summary>
        private void UpdateFriendsMonitoringSensors(
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            SteamData data)
        {
            friendsOnlineSensor.Value = (float)data.FriendsOnline;
            friendsInGameSensor.Value = (float)data.FriendsInGame;
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateFriendsMonitoringSensors", "Friends monitoring sensors updated", new
            {
                FriendsOnline = data.FriendsOnline,
                FriendsInGame = data.FriendsInGame
            });
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
            // Handle unavailable achievement data (marked as -1)
            if (data.CurrentGameAchievementPercentage < 0 || data.CurrentGameAchievementsTotal < 0)
            {
                currentGameAchievementsSensor.Value = 0f;
                currentGameAchievementsUnlockedSensor.Value = 0f;  
                currentGameAchievementsTotalSensor.Value = 0f;
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateAchievementTrackingSensors", "Achievement data unavailable");
            }
            else
            {
                var achievementPercentage = (float)Math.Round(data.CurrentGameAchievementPercentage, 1);
                currentGameAchievementsSensor.Value = achievementPercentage;
                
                currentGameAchievementsUnlockedSensor.Value = (float)data.CurrentGameAchievementsUnlocked;
                currentGameAchievementsTotalSensor.Value = (float)data.CurrentGameAchievementsTotal;
                
                _enhancedLogger?.LogDebug("SensorManagementService.UpdateAchievementTrackingSensors", "Achievement sensors updated", new
                {
                    Percentage = achievementPercentage,
                    Unlocked = data.CurrentGameAchievementsUnlocked,
                    Total = data.CurrentGameAchievementsTotal
                });
            }
            
            string latestAchievement;
            if (data.LatestAchievementDate.HasValue && data.LatestAchievementDate.Value > DateTime.Now.AddDays(-SensorManagementConstants.RECENT_ACHIEVEMENT_DAYS))
            {
                latestAchievement = data.LatestAchievementName ?? "None";
            }
            else
            {
                latestAchievement = "None recent";
            }
            latestAchievementSensor.Value = latestAchievement;
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateAchievementTrackingSensors", "Latest achievement sensor updated", new
            {
                LatestAchievement = latestAchievement,
                HasRecentAchievement = data.LatestAchievementDate.HasValue && data.LatestAchievementDate.Value > DateTime.Now.AddDays(-SensorManagementConstants.RECENT_ACHIEVEMENT_DAYS)
            });
        }
        
        /// <summary>
        /// Sets error state for all Enhanced Gaming sensors
        /// </summary>
        private void SetEnhancedGamingSensorsErrorState(
            PluginSensor recentGamesCountSensor,
            PluginText mostPlayedRecentSensor,
            PluginSensor recentSessionsSensor,
            PluginText currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginText averageSessionTimeSensor,
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
                currentSessionTimeSensor.Value = "Error";
                sessionStartTimeSensor.Value = "Error";
                averageSessionTimeSensor.Value = "Error";
                
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
        /// Updates Advanced Features sensors with new data in a thread-safe manner
        /// </summary>
        public void UpdateAdvancedFeaturesSensors(
            // Detailed Game-Specific Statistics sensors
            PluginText primaryGameStatsSensor,
            PluginText secondaryGameStatsSensor,
            PluginText tertiaryGameStatsSensor,
            // Multiple Game Monitoring sensors
            PluginSensor monitoredGamesCountSensor,
            PluginSensor monitoredGamesTotalHoursSensor,
            // Removed artificial Achievement Completion Tracking sensors
            // These depend on data not available via Steam Web API
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
                            monitoredGamesCountSensor, monitoredGamesTotalHoursSensor,
                            // Removed artificial achievement completion sensors
                            latestGameNewsSensor, unreadNewsCountSensor,
                            mostActiveNewsGameSensor, data.ErrorMessage ?? "Unknown error");
                        return;
                    }
                    
                    // Update detailed game-specific statistics
                    UpdateDetailedGameStatsSensors(primaryGameStatsSensor, secondaryGameStatsSensor, tertiaryGameStatsSensor, data);
                    
                    // Update multiple game monitoring
                    UpdateMultipleGameMonitoringSensors(monitoredGamesCountSensor, monitoredGamesTotalHoursSensor, data);
                    
                    // Removed artificial achievement completion tracking - Steam API doesn't provide overall achievement statistics
                    
                    // Update news and update monitoring
                    UpdateNewsMonitoringSensors(latestGameNewsSensor, unreadNewsCountSensor, mostActiveNewsGameSensor, data);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SensorManagementService] Error updating Advanced Features sensors: {ex.Message}");
                    
                    // Set error state for all Advanced Features sensors
                    SetAdvancedFeaturesSensorsErrorState(
                        primaryGameStatsSensor, secondaryGameStatsSensor, tertiaryGameStatsSensor,
                        monitoredGamesCountSensor, monitoredGamesTotalHoursSensor,
                        // Removed artificial achievement completion sensors
                        latestGameNewsSensor, unreadNewsCountSensor,
                        mostActiveNewsGameSensor, ex.Message);
                }
            }
        }
        
        #endregion

        #region Advanced Features Sensor Updates
        
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
            secondaryGameStatsSensor.Value = data.SecondaryGameStats ?? "No data";
            tertiaryGameStatsSensor.Value = data.TertiaryGameStats ?? "No data";
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateDetailedGameStatsSensors", "Detailed game stats sensors updated", new
            {
                PrimaryGameStats = data.PrimaryGameStats ?? "None",
                SecondaryGameStats = data.SecondaryGameStats ?? "None",
                TertiaryGameStats = data.TertiaryGameStats ?? "None"
            });
        }
        
        /// <summary>
        /// Updates multiple game monitoring sensors
        /// </summary>
        private void UpdateMultipleGameMonitoringSensors(
            PluginSensor monitoredGamesCountSensor,
            PluginSensor monitoredGamesTotalHoursSensor,
            SteamData data)
        {
            // Use TotalGamesOwned instead of MonitoredGamesCount for library data
            var gamesCount = data.TotalGamesOwned > 0 ? data.TotalGamesOwned : data.MonitoredGamesCount;
            monitoredGamesCountSensor.Value = (float)gamesCount;
            
            // Use TotalLibraryPlaytimeHours instead of MonitoredGamesTotalHours for library data  
            var libraryHours = data.TotalLibraryPlaytimeHours > 0 ? data.TotalLibraryPlaytimeHours : data.MonitoredGamesTotalHours;
            var totalHours = (float)Math.Round(libraryHours, 1);
            monitoredGamesTotalHoursSensor.Value = totalHours;
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateMultipleGameMonitoringSensors", "Multiple game monitoring sensors updated", new
            {
                GamesCount = gamesCount,
                TotalHours = totalHours
            });
        }
        
        
        /// <summary>
        /// Removed UpdateAchievementCompletionTrackingSensors method
        /// These sensors depended on artificial data not available via Steam Web API
        /// </summary>
        
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
            unreadNewsCountSensor.Value = (float)data.UnreadNewsCount;
            mostActiveNewsGameSensor.Value = data.MostActiveNewsGame ?? "None";
            
            _enhancedLogger?.LogDebug("SensorManagementService.UpdateNewsMonitoringSensors", "News monitoring sensors updated", new
            {
                LatestGameNews = data.LatestGameNews ?? "None",
                UnreadNewsCount = data.UnreadNewsCount,
                MostActiveNewsGame = data.MostActiveNewsGame ?? "None"
            });
        }
        
        /// <summary>
        /// Sets error state for all Advanced Features sensors
        /// </summary>
        private void SetAdvancedFeaturesSensorsErrorState(
            PluginText primaryGameStatsSensor,
            PluginText secondaryGameStatsSensor,
            PluginText tertiaryGameStatsSensor,
            PluginSensor monitoredGamesCountSensor,
            PluginSensor monitoredGamesTotalHoursSensor,
            // Removed artificial achievement completion sensors
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
            // Removed artificial achievement completion sensors
            unreadNewsCountSensor.Value = 0f;
            
            _enhancedLogger?.LogError("SensorManagementService.SetAdvancedFeaturesSensorsErrorState", "Set Advanced Features sensors to error state", null, new
            {
                ErrorMessage = errorMessage
            });
        }

        /// <summary>
        /// Updates Social & Community Features sensors with social data
        /// </summary>
        public void UpdateSocialFeaturesSensors(
            // Friends Activity sensors
            PluginSensor totalFriendsCountSensor,
            PluginText friendActivityStatusSensor,
            // Friends Monitoring sensors
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            // Community Badge sensors
            PluginSensor totalBadgesEarnedSensor,
            PluginSensor totalBadgeXPSensor,
            PluginText latestBadgeSensor,
            SteamData data)
        {
            if (data == null)
            {
                _enhancedLogger?.LogWarning("SensorManagementService.UpdateSocialFeaturesSensors", "SteamData is null");
                SetSocialFeaturesSensorsToError(totalFriendsCountSensor, friendActivityStatusSensor,
                    friendsOnlineSensor, friendsInGameSensor,
                    totalBadgesEarnedSensor, totalBadgeXPSensor, latestBadgeSensor,
                    "No data available");
                return;
            }

            if (data.HasError)
            {
                _enhancedLogger?.LogWarning("SensorManagementService.UpdateSocialFeaturesSensors", "SteamData has error", new
                {
                    ErrorMessage = data.ErrorMessage
                });
                SetSocialFeaturesSensorsToError(totalFriendsCountSensor, friendActivityStatusSensor,
                    friendsOnlineSensor, friendsInGameSensor,
                    totalBadgesEarnedSensor, totalBadgeXPSensor, latestBadgeSensor,
                    data.ErrorMessage ?? "Unknown error");
                return;
            }

            lock (_sensorLock)
            {
                try
                {
                    // Update Friends Activity sensors
                    totalFriendsCountSensor.Value = data.TotalFriendsCount;
                    
                    // Format friend activity status based on online friends count
                    var friendActivityText = data.FriendsOnline switch
                    {
                        0 => "No friends online",
                        1 => "1 friend online",
                        _ => $"{data.FriendsOnline} friends online"
                    };
                    friendActivityStatusSensor.Value = friendActivityText;
                    
                    // Update Friends Monitoring sensors with correct social data
                    UpdateFriendsMonitoringSensors(friendsOnlineSensor, friendsInGameSensor, data);
                    
                    // Update Community Badge sensors
                    totalBadgesEarnedSensor.Value = data.TotalBadgesEarned;
                    totalBadgeXPSensor.Value = data.TotalBadgeXP;
                    latestBadgeSensor.Value = !string.IsNullOrEmpty(data.NextBadgeProgress) ? data.NextBadgeProgress : "None";

                    _enhancedLogger?.LogDebug("SensorManagementService.UpdateSocialFeaturesSensors", "Social & Community Features sensors updated successfully");
                    _enhancedLogger?.LogInfo("SensorManagementService.UpdateSocialFeaturesSensors", "Social features summary", new
                    {
                        TotalFriends = data.TotalFriendsCount,
                        FriendsOnline = data.FriendsOnline,
                        TotalBadges = data.TotalBadgesEarned,
                        TotalBadgeXP = data.TotalBadgeXP,
                        GlobalPlaytimePercentile = Math.Round(data.GlobalPlaytimePercentile, 1)
                    });
                }
                catch (Exception ex)
                {
                    _enhancedLogger?.LogError("SensorManagementService.UpdateSocialFeaturesSensors", "Error updating Social & Community Features sensors", ex);
                    Console.WriteLine($"[SensorManagementService] Error updating social features sensors: {ex.Message}");
                    SetSocialFeaturesSensorsToError(totalFriendsCountSensor, friendActivityStatusSensor,
                        friendsOnlineSensor, friendsInGameSensor,
                        totalBadgesEarnedSensor, totalBadgeXPSensor, latestBadgeSensor,
                        ex.Message);
                }
            }
        }

        /// <summary>
        /// Sets all Social & Community Features sensors to error state
        /// </summary>
        private void SetSocialFeaturesSensorsToError(
            PluginSensor totalFriendsCountSensor,
            PluginText friendActivityStatusSensor,
            PluginSensor friendsOnlineSensor,
            PluginSensor friendsInGameSensor,
            PluginSensor totalBadgesEarnedSensor,
            PluginSensor totalBadgeXPSensor,
            PluginText latestBadgeSensor,
            string errorMessage)
        {
            // Friends Activity error values
            totalFriendsCountSensor.Value = 0f;
            friendActivityStatusSensor.Value = "Error";
            
            // Friends Monitoring error values
            friendsOnlineSensor.Value = 0f;
            friendsInGameSensor.Value = 0f;
            
            // Community Badge error values
            totalBadgesEarnedSensor.Value = 0f;
            totalBadgeXPSensor.Value = 0f;
            latestBadgeSensor.Value = "Error";
            
            _logger?.LogError($"Set Social & Community Features sensors to error state: {errorMessage}");
        }
        
        /// <summary>
        /// Formats minutes to hour:minute format (e.g., 90 -> "1:30", 30 -> "30m")
        /// </summary>
        private static string FormatMinutesToHourMin(int totalMinutes)
        {
            if (totalMinutes <= 0) return "0m";
            
            var hours = totalMinutes / SensorManagementConstants.MINUTES_PER_HOUR;
            var minutes = totalMinutes % SensorManagementConstants.MINUTES_PER_HOUR;
            
            return hours > 0 ? $"{hours}:{minutes:D2}" : $"{minutes}m";
        }
        
        #endregion
    }
}
