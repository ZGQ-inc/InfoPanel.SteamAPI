using System;
using System.Linq;
using InfoPanel.Plugins;
using InfoPanel.SteamAPI.Models;
using InfoPanel.SteamAPI.Services.Monitoring;

namespace InfoPanel.SteamAPI.Services.Sensors
{
    /// <summary>
    /// Player Domain Sensor Service
    /// Updates InfoPanel sensors for player-specific data:
    /// - Profile information (name, status, level)
    /// - Current game information
    /// - Session tracking (current session, average session)
    /// - Profile and game banner images
    /// </summary>
    public class PlayerSensorService : IDisposable
    {
        private const string DOMAIN_NAME = "PLAYER_SENSORS";
        
        // Configuration and services
        private readonly ConfigurationService _configService;
        private readonly EnhancedLoggingService? _enhancedLogger;
        
        // Thread safety
        private readonly object _sensorLock = new();
        
        // Player sensors (injected via constructor)
        private readonly PluginText _playerNameSensor;
        private readonly PluginText _onlineStatusSensor;
        private readonly PluginSensor _steamLevelSensor;
        private readonly PluginText _currentGameSensor;
        private readonly PluginSensor _currentGamePlaytimeSensor;
        private readonly PluginText _statusSensor;
        private readonly PluginText _detailsSensor;
        
        // Session tracking sensors
        private readonly PluginText _currentSessionTimeSensor;
        private readonly PluginText _sessionStartTimeSensor;
        private readonly PluginText _averageSessionTimeSensor;
        
        // Image URL sensors
        private readonly PluginText _profileImageUrlSensor;
        private readonly PluginText _currentGameBannerUrlSensor;
        private readonly PluginText _gameStatusTextSensor;
        
        private bool _disposed = false;
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public PlayerSensorService(
            ConfigurationService configService,
            PluginText playerNameSensor,
            PluginText onlineStatusSensor,
            PluginSensor steamLevelSensor,
            PluginText currentGameSensor,
            PluginSensor currentGamePlaytimeSensor,
            PluginText statusSensor,
            PluginText detailsSensor,
            PluginText currentSessionTimeSensor,
            PluginText sessionStartTimeSensor,
            PluginText averageSessionTimeSensor,
            PluginText profileImageUrlSensor,
            PluginText currentGameBannerUrlSensor,
            PluginText gameStatusTextSensor,
            EnhancedLoggingService? enhancedLogger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _playerNameSensor = playerNameSensor ?? throw new ArgumentNullException(nameof(playerNameSensor));
            _onlineStatusSensor = onlineStatusSensor ?? throw new ArgumentNullException(nameof(onlineStatusSensor));
            _steamLevelSensor = steamLevelSensor ?? throw new ArgumentNullException(nameof(steamLevelSensor));
            _currentGameSensor = currentGameSensor ?? throw new ArgumentNullException(nameof(currentGameSensor));
            _currentGamePlaytimeSensor = currentGamePlaytimeSensor ?? throw new ArgumentNullException(nameof(currentGamePlaytimeSensor));
            _statusSensor = statusSensor ?? throw new ArgumentNullException(nameof(statusSensor));
            _detailsSensor = detailsSensor ?? throw new ArgumentNullException(nameof(detailsSensor));
            _currentSessionTimeSensor = currentSessionTimeSensor ?? throw new ArgumentNullException(nameof(currentSessionTimeSensor));
            _sessionStartTimeSensor = sessionStartTimeSensor ?? throw new ArgumentNullException(nameof(sessionStartTimeSensor));
            _averageSessionTimeSensor = averageSessionTimeSensor ?? throw new ArgumentNullException(nameof(averageSessionTimeSensor));
            _profileImageUrlSensor = profileImageUrlSensor ?? throw new ArgumentNullException(nameof(profileImageUrlSensor));
            _currentGameBannerUrlSensor = currentGameBannerUrlSensor ?? throw new ArgumentNullException(nameof(currentGameBannerUrlSensor));
            _gameStatusTextSensor = gameStatusTextSensor ?? throw new ArgumentNullException(nameof(gameStatusTextSensor));
            _enhancedLogger = enhancedLogger;
            
            Console.WriteLine($"[{DOMAIN_NAME}] PlayerSensorService initialized");
        }
        
        /// <summary>
        /// Subscribe to player monitoring events
        /// </summary>
        public void SubscribeToMonitoring(PlayerMonitoringService playerMonitoring)
        {
            if (playerMonitoring == null)
                throw new ArgumentNullException(nameof(playerMonitoring));
            
            playerMonitoring.PlayerDataUpdated += OnPlayerDataUpdated;
            
            _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.SubscribeToMonitoring", "Subscribed to player monitoring events");
        }
        
        /// <summary>
        /// Unsubscribe from player monitoring events
        /// </summary>
        public void UnsubscribeFromMonitoring(PlayerMonitoringService playerMonitoring)
        {
            if (playerMonitoring == null)
                return;
            
            playerMonitoring.PlayerDataUpdated -= OnPlayerDataUpdated;
            
            _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.UnsubscribeFromMonitoring", "Unsubscribed from player monitoring events");
        }
        
        /// <summary>
        /// Event handler for player data updates
        /// </summary>
        private void OnPlayerDataUpdated(object? sender, PlayerDataEventArgs e)
        {
            if (e?.Data == null)
            {
                _enhancedLogger?.LogWarning($"{DOMAIN_NAME}.OnPlayerDataUpdated", "Received null player data");
                return;
            }
            
            try
            {
                UpdatePlayerSensors(e.Data, e.SessionCache);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DOMAIN_NAME}] Error updating player sensors: {ex.Message}");
                
                _enhancedLogger?.LogError($"{DOMAIN_NAME}.OnPlayerDataUpdated", "Failed to update sensors", ex);
            }
        }
        
        /// <summary>
        /// Update all player sensors with data from monitoring service
        /// </summary>
        private void UpdatePlayerSensors(PlayerData playerData, SessionDataCache sessionCache)
        {
            lock (_sensorLock)
            {
                try
                {
                    _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdatePlayerSensors", "Updating player sensors", new
                    {
                        PlayerName = playerData.PlayerName,
                        IsInGame = playerData.IsInGame(),
                        CurrentSessionMinutes = sessionCache.CurrentSessionMinutes
                    });
                    
                    // Update profile sensors
                    UpdateProfileSensors(playerData);
                    
                    // Update current game sensors
                    UpdateCurrentGameSensors(playerData);
                    
                    // Update session tracking sensors
                    UpdateSessionSensors(sessionCache);
                    
                    // Update image URL sensors
                    UpdateImageUrlSensors(playerData, sessionCache);
                    
                    // Update status sensors
                    UpdateStatusSensors(playerData);
                    
                    _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.UpdatePlayerSensors", "Player sensors updated successfully", new
                    {
                        PlayerName = playerData.PlayerName,
                        CurrentGame = playerData.CurrentGameName ?? "Not Playing",
                        CurrentSessionMinutes = sessionCache.CurrentSessionMinutes
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DOMAIN_NAME}] Error updating sensors: {ex.Message}");
                    
                    _enhancedLogger?.LogError($"{DOMAIN_NAME}.UpdatePlayerSensors", "Sensor update failed", ex);
                    
                    SetErrorState(ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Update profile sensors (name, status, level)
        /// </summary>
        private void UpdateProfileSensors(PlayerData playerData)
        {
            // Update player name
            var playerName = !string.IsNullOrEmpty(playerData.PlayerName) ? playerData.PlayerName : "Unknown Player";
            _playerNameSensor.Value = playerName;
            
            // Update online status
            var onlineStatus = playerData.OnlineState ?? "Offline";
            _onlineStatusSensor.Value = onlineStatus;
            
            // Update Steam level
            _steamLevelSensor.Value = playerData.SteamLevel;
            
            _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdateProfileSensors", "Profile sensors updated", new
            {
                PlayerName = playerName,
                OnlineStatus = onlineStatus,
                SteamLevel = playerData.SteamLevel
            });
        }
        
        /// <summary>
        /// Update current game sensors
        /// </summary>
        private void UpdateCurrentGameSensors(PlayerData playerData)
        {
            if (playerData.IsInGame() && !string.IsNullOrEmpty(playerData.CurrentGameName))
            {
                _currentGameSensor.Value = playerData.CurrentGameName;
                _currentGamePlaytimeSensor.Value = (float)Math.Round(playerData.CurrentGameTotalPlaytimeHours, 1);
                
                _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdateCurrentGameSensors", "Current game sensors updated", new
                {
                    GameName = playerData.CurrentGameName,
                    TotalPlaytimeHours = Math.Round(playerData.CurrentGameTotalPlaytimeHours, 1)
                });
            }
            else
            {
                _currentGameSensor.Value = "Not Playing";
                _currentGamePlaytimeSensor.Value = 0f;
                
                _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdateCurrentGameSensors", "Not playing - sensors set to default");
            }
        }
        
        /// <summary>
        /// Update session tracking sensors
        /// </summary>
        private void UpdateSessionSensors(SessionDataCache sessionCache)
        {
            // Format current session time
            var currentSessionFormatted = FormatMinutesToHourMin(sessionCache.CurrentSessionMinutes);
            _currentSessionTimeSensor.Value = currentSessionFormatted;
            
            // Format session start time
            var sessionStartTime = sessionCache.SessionStartTime?.ToString("HH:mm") ?? "Not in game";
            _sessionStartTimeSensor.Value = sessionStartTime;
            
            // Format average session time
            var avgSessionFormatted = FormatMinutesToHourMin((int)Math.Round(sessionCache.AverageSessionMinutes));
            _averageSessionTimeSensor.Value = avgSessionFormatted;
            
            _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.UpdateSessionSensors", "Session sensors updated", new
            {
                CurrentSessionTime = currentSessionFormatted,
                CurrentSessionMinutes = sessionCache.CurrentSessionMinutes,
                SessionStartTime = sessionStartTime,
                AverageSessionTime = avgSessionFormatted,
                AverageSessionMinutes = Math.Round(sessionCache.AverageSessionMinutes, 1)
            });
        }
        
        /// <summary>
        /// Update image URL sensors (profile, banner)
        /// </summary>
        private void UpdateImageUrlSensors(PlayerData playerData, SessionDataCache sessionCache)
        {
            // Update profile image URL
            var profileImageUrl = playerData.ProfileImageUrl ?? "-";
            _profileImageUrlSensor.Value = profileImageUrl;
            
            // Determine if user is currently playing a game
            bool isCurrentlyPlaying = playerData.IsInGame() && 
                                      !string.IsNullOrEmpty(playerData.CurrentGameName) &&
                                      playerData.CurrentGameAppId > 0;
            
            if (isCurrentlyPlaying)
            {
                // User is actively playing - show current game banner
                var currentBanner = playerData.CurrentGameBannerUrl ?? "-";
                _currentGameBannerUrlSensor.Value = currentBanner;
                _gameStatusTextSensor.Value = _configService.CurrentlyPlayingText;
                
                _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdateImageUrlSensors", "Currently playing - showing current game", new
                {
                    GameName = playerData.CurrentGameName,
                    AppId = playerData.CurrentGameAppId,
                    BannerUrl = currentBanner,
                    StatusText = _configService.CurrentlyPlayingText
                });
            }
            else
            {
                // User is not playing - show last played game from cache
                var lastPlayedBannerUrl = sessionCache.LastPlayedGameBannerUrl ?? "-";
                _currentGameBannerUrlSensor.Value = lastPlayedBannerUrl;
                _gameStatusTextSensor.Value = _configService.LastPlayedGameText;
                
                _enhancedLogger?.LogInfo($"{DOMAIN_NAME}.UpdateImageUrlSensors", "Not playing - showing last played game", new
                {
                    LastPlayedGameName = sessionCache.LastPlayedGameName,
                    LastPlayedBannerUrl = lastPlayedBannerUrl,
                    StatusText = _configService.LastPlayedGameText
                });
            }
        }
        
        /// <summary>
        /// Update status sensors
        /// </summary>
        private void UpdateStatusSensors(PlayerData playerData)
        {
            // Format status text
            string status;
            if (playerData.IsInGame() && !string.IsNullOrEmpty(playerData.CurrentGameName))
            {
                status = $"Playing {playerData.CurrentGameName}";
            }
            else if (!string.IsNullOrEmpty(playerData.OnlineState))
            {
                status = playerData.OnlineState;
            }
            else
            {
                status = "Offline";
            }
            _statusSensor.Value = status;
            
            // Format details text
            var details = $"Level {playerData.SteamLevel}";
            _detailsSensor.Value = details;
            
            _enhancedLogger?.LogDebug($"{DOMAIN_NAME}.UpdateStatusSensors", "Status sensors updated", new
            {
                Status = status,
                Details = details
            });
        }
        
        /// <summary>
        /// Set all player sensors to error state
        /// </summary>
        private void SetErrorState(string errorMessage)
        {
            try
            {
                _playerNameSensor.Value = "Error";
                _onlineStatusSensor.Value = "Error";
                _steamLevelSensor.Value = 0;
                _currentGameSensor.Value = "Error";
                _currentGamePlaytimeSensor.Value = 0;
                _statusSensor.Value = $"Error: {errorMessage}";
                _detailsSensor.Value = $"Error: {errorMessage}";
                _currentSessionTimeSensor.Value = "Error";
                _sessionStartTimeSensor.Value = "Error";
                _averageSessionTimeSensor.Value = "Error";
                _profileImageUrlSensor.Value = "-";
                _currentGameBannerUrlSensor.Value = "-";
                _gameStatusTextSensor.Value = "Error";
                
                _enhancedLogger?.LogError($"{DOMAIN_NAME}.SetErrorState", "Player sensors set to error state", null, new
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
        /// Formats minutes to hour:minute format (e.g., 90 -> "1:30", 30 -> "30m")
        /// </summary>
        private static string FormatMinutesToHourMin(int totalMinutes)
        {
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            
            // Always format as HH:mm (e.g., 00:05, 01:30, 06:45)
            return $"{hours:D2}:{minutes:D2}";
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
                Console.WriteLine($"[{DOMAIN_NAME}] PlayerSensorService disposed");
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
