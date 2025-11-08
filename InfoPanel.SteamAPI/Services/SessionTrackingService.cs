using InfoPanel.SteamAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Represents a single gaming session
    /// </summary>
    public class GameSession
    {
        public string GameName { get; set; } = "";
        public int AppId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationMinutes => EndTime.HasValue 
            ? (int)(EndTime.Value - StartTime).TotalMinutes 
            : (int)(DateTime.Now - StartTime).TotalMinutes;
        public bool IsActive => !EndTime.HasValue;
    }

    /// <summary>
    /// Session history container for persistence
    /// </summary>
    public class SessionHistory
    {
        public List<GameSession> Sessions { get; set; } = new();
        public GameSession? CurrentSession { get; set; }
    }

    /// <summary>
    /// Service for tracking real gaming sessions
    /// Monitors when games start/stop and calculates actual session times
    /// </summary>
    public class SessionTrackingService
    {
        #region Fields
        
        private readonly FileLoggingService? _logger;
        private readonly string _sessionFilePath;
        private SessionHistory _sessionHistory;
        private readonly object _sessionLock = new();
        
        // Session state tracking
        private string? _lastKnownGameName;
        private int _lastKnownAppId;
        private bool _wasInGameLastCheck;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes the session tracking service
        /// </summary>
        /// <param name="logger">Optional logger service</param>
        /// <param name="sessionFilePath">Path to session persistence file</param>
        public SessionTrackingService(FileLoggingService? logger = null, string? sessionFilePath = null)
        {
            _logger = logger;
            
            // Use provided path or create default path based on plugin location
            _sessionFilePath = sessionFilePath ?? GetDefaultSessionFilePath();
            
            _sessionHistory = new SessionHistory();
            _wasInGameLastCheck = false;
            
            LoadSessionHistory();
            CleanupOldSessions();
            
            _logger?.LogDebug($"SessionTrackingService initialized. Session file: {_sessionFilePath}");
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Updates session tracking based on current Steam state
        /// Call this method regularly (e.g., every monitoring cycle)
        /// </summary>
        /// <param name="steamData">Current Steam data</param>
        public void UpdateSessionTracking(SteamData steamData)
        {
            lock (_sessionLock)
            {
                try
                {
                    var isCurrentlyInGame = steamData.IsInGame();
                    var currentGameName = steamData.CurrentGameName;
                    var currentAppId = steamData.CurrentGameAppId;
                    
                    // Check for session state changes
                    if (isCurrentlyInGame && !_wasInGameLastCheck)
                    {
                        // Game session started
                        StartNewSession(currentGameName, currentAppId);
                    }
                    else if (!isCurrentlyInGame && _wasInGameLastCheck)
                    {
                        // Game session ended
                        EndCurrentSession();
                    }
                    else if (isCurrentlyInGame && _wasInGameLastCheck)
                    {
                        // Still in game - check if it's a different game
                        if (currentAppId != _lastKnownAppId || currentGameName != _lastKnownGameName)
                        {
                            // Different game - end previous session and start new one
                            EndCurrentSession();
                            StartNewSession(currentGameName, currentAppId);
                        }
                        // Otherwise continue current session
                    }
                    
                    // Update SteamData with current session info
                    UpdateSteamDataWithSessionInfo(steamData);
                    
                    // Update tracking state
                    _wasInGameLastCheck = isCurrentlyInGame;
                    _lastKnownGameName = currentGameName;
                    _lastKnownAppId = currentAppId;
                    
                    // Periodically save session history (every few updates to avoid excessive I/O)
                    if (DateTime.Now.Second % 30 == 0) // Save every 30 seconds
                    {
                        SaveSessionHistory();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Error updating session tracking", ex);
                }
            }
        }
        
        /// <summary>
        /// Gets recent session statistics
        /// </summary>
        /// <param name="daysBack">Number of days to look back</param>
        /// <returns>Session statistics</returns>
        public (int sessionCount, double averageMinutes, double totalHours) GetRecentSessionStats(int daysBack = 7)
        {
            lock (_sessionLock)
            {
                var cutoffDate = DateTime.Now.AddDays(-daysBack);
                var recentSessions = _sessionHistory.Sessions
                    .Where(s => s.StartTime >= cutoffDate && s.EndTime.HasValue)
                    .ToList();
                
                if (!recentSessions.Any())
                    return (0, 0, 0);
                
                var sessionCount = recentSessions.Count;
                var totalMinutes = recentSessions.Sum(s => s.DurationMinutes);
                var averageMinutes = totalMinutes / (double)sessionCount;
                var totalHours = totalMinutes / 60.0;
                
                return (sessionCount, averageMinutes, totalHours);
            }
        }
        
        /// <summary>
        /// Forces end of current session (useful when plugin is shutting down)
        /// </summary>
        public void EndCurrentSessionIfActive()
        {
            lock (_sessionLock)
            {
                if (_sessionHistory.CurrentSession?.IsActive == true)
                {
                    EndCurrentSession();
                    SaveSessionHistory();
                }
            }
        }
        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Starts a new gaming session
        /// </summary>
        private void StartNewSession(string? gameName, int appId)
        {
            if (string.IsNullOrEmpty(gameName) || appId <= 0)
                return;
                
            var newSession = new GameSession
            {
                GameName = gameName,
                AppId = appId,
                StartTime = DateTime.Now
            };
            
            _sessionHistory.CurrentSession = newSession;
            
            _logger?.LogDebug($"Started new session: {gameName} (AppID: {appId}) at {newSession.StartTime:HH:mm:ss}");
        }
        
        /// <summary>
        /// Ends the current gaming session
        /// </summary>
        private void EndCurrentSession()
        {
            if (_sessionHistory.CurrentSession?.IsActive == true)
            {
                _sessionHistory.CurrentSession.EndTime = DateTime.Now;
                var duration = _sessionHistory.CurrentSession.DurationMinutes;
                
                // Add to session history
                _sessionHistory.Sessions.Add(_sessionHistory.CurrentSession);
                
                _logger?.LogDebug($"Ended session: {_sessionHistory.CurrentSession.GameName} - Duration: {duration} minutes");
                
                _sessionHistory.CurrentSession = null;
            }
        }
        
        /// <summary>
        /// Updates SteamData with current session information
        /// </summary>
        private void UpdateSteamDataWithSessionInfo(SteamData steamData)
        {
            // Update current session info
            if (_sessionHistory.CurrentSession?.IsActive == true)
            {
                steamData.SessionStartTime = _sessionHistory.CurrentSession.StartTime;
                steamData.CurrentSessionTimeMinutes = _sessionHistory.CurrentSession.DurationMinutes;
            }
            else
            {
                steamData.SessionStartTime = null;
                steamData.CurrentSessionTimeMinutes = 0;
            }
            
            // Update recent sessions and average
            var recentStats = GetRecentSessionStats(7); // Last 7 days
            steamData.RecentGameSessions = recentStats.sessionCount;
            steamData.AverageSessionTimeMinutes = recentStats.averageMinutes;
        }
        
        /// <summary>
        /// Loads session history from file
        /// </summary>
        private void LoadSessionHistory()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    var json = File.ReadAllText(_sessionFilePath);
                    var loaded = JsonSerializer.Deserialize<SessionHistory>(json);
                    
                    if (loaded != null)
                    {
                        _sessionHistory = loaded;
                        _logger?.LogDebug($"Loaded {_sessionHistory.Sessions.Count} historical sessions");
                        
                        // If there was a current session when we last saved, it's now incomplete
                        // (plugin was restarted while in game), so end it
                        if (_sessionHistory.CurrentSession?.IsActive == true)
                        {
                            _sessionHistory.CurrentSession.EndTime = DateTime.Now;
                            _sessionHistory.Sessions.Add(_sessionHistory.CurrentSession);
                            _sessionHistory.CurrentSession = null;
                            _logger?.LogDebug("Ended incomplete session from previous run");
                        }
                    }
                }
                else
                {
                    _logger?.LogDebug("No existing session history file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error loading session history: {ex.Message}");
                _sessionHistory = new SessionHistory(); // Start fresh if loading fails
            }
        }
        
        /// <summary>
        /// Saves session history to file
        /// </summary>
        private void SaveSessionHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(_sessionHistory, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_sessionFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(_sessionFilePath, json);
                
                _logger?.LogDebug($"Saved session history to {_sessionFilePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error saving session history: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cleans up old sessions to prevent file from growing indefinitely
        /// </summary>
        private void CleanupOldSessions()
        {
            var cutoffDate = DateTime.Now.AddDays(-30); // Keep last 30 days
            var originalCount = _sessionHistory.Sessions.Count;
            
            _sessionHistory.Sessions = _sessionHistory.Sessions
                .Where(s => s.StartTime >= cutoffDate)
                .ToList();
                
            var removedCount = originalCount - _sessionHistory.Sessions.Count;
            if (removedCount > 0)
            {
                _logger?.LogDebug($"Cleaned up {removedCount} old sessions (older than 30 days)");
            }
        }
        
        /// <summary>
        /// Gets the default path for session persistence file
        /// </summary>
        private string GetDefaultSessionFilePath()
        {
            try
            {
                // Try to get the plugin's directory
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var pluginDir = Path.GetDirectoryName(assembly.ManifestModule.FullyQualifiedName);
                
                if (!string.IsNullOrEmpty(pluginDir))
                {
                    return Path.Combine(pluginDir, "sessions.json");
                }
            }
            catch
            {
                // Fallback if we can't determine plugin directory
            }
            
            // Fallback to a temporary directory
            var tempDir = Path.GetTempPath();
            return Path.Combine(tempDir, "InfoPanel.SteamAPI.sessions.json");
        }
        
        #endregion

        #region Disposal
        
        /// <summary>
        /// Cleanup when service is disposed
        /// </summary>
        public void Dispose()
        {
            EndCurrentSessionIfActive();
            _logger?.LogDebug("SessionTrackingService disposed");
        }
        
        #endregion
    }
}