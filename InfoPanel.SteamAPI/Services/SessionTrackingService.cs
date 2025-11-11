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
        public string DurationFormatted 
        {
            get
            {
                var totalMinutes = DurationMinutes;
                var hours = totalMinutes / 60;
                var minutes = totalMinutes % 60;
                return hours > 0 ? $"{hours}:{minutes:D2}" : $"{minutes}m";
            }
        }
        public bool IsActive => !EndTime.HasValue;
    }

    /// <summary>
    /// Session history container for persistence
    /// </summary>
    public class SessionHistory
    {
        public string Version { get; set; } = "1.0";
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int TotalSessions => Sessions.Count;
        public double TotalPlaytimeMinutes => Sessions.Where(s => !s.IsActive).Sum(s => s.DurationMinutes);
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
        private readonly EnhancedLoggingService? _enhancedLogger;
        private readonly string _sessionFilePath;
        private SessionHistory _sessionHistory;
        private readonly object _sessionLock = new();
        
        // Session state tracking
        private string? _lastKnownGameName;
        private int _lastKnownAppId;
        private bool _wasInGameLastCheck;
        
        // Session stability tracking to prevent rapid cycling
        private DateTime _lastStateChangeTime = DateTime.Now;
        private bool _pendingGameStart = false;
        private string? _pendingGameName;
        private int _pendingGameAppId;
        
        // Constants for session stability
        private const int MIN_SESSION_DURATION_SECONDS = 30; // Minimum 30 seconds before ending session
        private const int STATE_CHANGE_DEBOUNCE_SECONDS = 10; // Wait 10 seconds before starting new session
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes the session tracking service
        /// </summary>
        /// <param name="logger">Optional logger service (legacy)</param>
        /// <param name="enhancedLogger">Optional enhanced logger service</param>
        /// <param name="sessionFilePath">Path to session persistence file</param>
        public SessionTrackingService(FileLoggingService? logger = null, EnhancedLoggingService? enhancedLogger = null, string? sessionFilePath = null)
        {
            _logger = logger;
            _enhancedLogger = enhancedLogger;
            
            // Use provided path or create default path based on plugin location
            _sessionFilePath = sessionFilePath ?? GetDefaultSessionFilePath();
            
            _sessionHistory = new SessionHistory();
            _wasInGameLastCheck = false;
            
            // Ensure session file exists and load history
            EnsureSessionFileExists();
            LoadSessionHistory();
            CleanupOldSessions();
            
            // Enhanced logging for initialization
            if (_enhancedLogger != null)
            {
                _enhancedLogger.LogInfo("SESSION", "SessionTrackingService initialized", new
                {
                    SessionFilePath = _sessionFilePath,
                    FileExists = File.Exists(_sessionFilePath),
                    HistoryLoaded = true
                });
            }
            else
            {
                _logger?.LogDebug($"SessionTrackingService initialized. Session file: {_sessionFilePath}");
                _logger?.LogDebug($"Session file exists: {File.Exists(_sessionFilePath)}");
            }
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
                    var now = DateTime.Now;
                    
                    _logger?.LogDebug($"[UpdateSessionTracking] IsInGame: {isCurrentlyInGame}, GameName: '{currentGameName}', AppId: {currentAppId}, WasInGameLastCheck: {_wasInGameLastCheck}");
                    
                    // Handle state changes with debouncing to prevent rapid cycling
                    if (isCurrentlyInGame && !_wasInGameLastCheck)
                    {
                        // Game detected - but wait before starting session to avoid false positives
                        _pendingGameStart = true;
                        _pendingGameName = currentGameName;
                        _pendingGameAppId = currentAppId;
                        _lastStateChangeTime = now;
                        _logger?.LogDebug($"[UpdateSessionTracking] Game detected, pending start for '{currentGameName}' - waiting {STATE_CHANGE_DEBOUNCE_SECONDS}s for stability");
                        // Don't update _wasInGameLastCheck yet - keep it false until the session actually starts
                    }
                    else if (!isCurrentlyInGame && _wasInGameLastCheck)
                    {
                        // Game ended - but only end session if it's been running long enough
                        if (_sessionHistory.CurrentSession?.IsActive == true)
                        {
                            var sessionDuration = now - _sessionHistory.CurrentSession.StartTime;
                            if (sessionDuration.TotalSeconds >= MIN_SESSION_DURATION_SECONDS)
                            {
                                _logger?.LogDebug($"[UpdateSessionTracking] Ending session after {sessionDuration.TotalMinutes:F1} minutes");
                                EndCurrentSession();
                                _wasInGameLastCheck = false; // Only update state when actually ending session
                            }
                            else
                            {
                                _logger?.LogDebug($"[UpdateSessionTracking] Game ended too quickly ({sessionDuration.TotalSeconds:F0}s), ignoring - may be API glitch");
                                // Don't update _wasInGameLastCheck, keep session active and ignore this state change
                                return;
                            }
                        }
                        else
                        {
                            // No active session but we thought we were in game - just update state
                            _wasInGameLastCheck = false;
                        }
                        _pendingGameStart = false; // Cancel any pending start
                    }
                    else if (isCurrentlyInGame && _wasInGameLastCheck)
                    {
                        // Still in game - check if it's a different game
                        if (currentAppId != _lastKnownAppId || currentGameName != _lastKnownGameName)
                        {
                            // Different game detected
                            if (_sessionHistory.CurrentSession?.IsActive == true)
                            {
                                var sessionDuration = now - _sessionHistory.CurrentSession.StartTime;
                                if (sessionDuration.TotalSeconds >= MIN_SESSION_DURATION_SECONDS)
                                {
                                    _logger?.LogDebug($"[UpdateSessionTracking] Game changed from '{_lastKnownGameName}' to '{currentGameName}' - switching sessions");
                                    EndCurrentSession();
                                    StartNewSession(currentGameName, currentAppId);
                                    _wasInGameLastCheck = true; // Maintain in-game state
                                    _lastKnownGameName = currentGameName;
                                    _lastKnownAppId = currentAppId;
                                }
                                else
                                {
                                    _logger?.LogDebug($"[UpdateSessionTracking] Game change detected too quickly, ignoring - may be API inconsistency");
                                    return;
                                }
                            }
                        }
                        // Continue current session - update known game info but don't change state
                        _lastKnownGameName = currentGameName;
                        _lastKnownAppId = currentAppId;
                    }
                    else if (_pendingGameStart && (now - _lastStateChangeTime).TotalSeconds >= STATE_CHANGE_DEBOUNCE_SECONDS)
                    {
                        // Pending game start has been stable long enough, start the session
                        if (isCurrentlyInGame && !string.IsNullOrEmpty(_pendingGameName))
                        {
                            _logger?.LogDebug($"[UpdateSessionTracking] Starting stable session for '{_pendingGameName}' after {STATE_CHANGE_DEBOUNCE_SECONDS}s delay");
                            StartNewSession(_pendingGameName, _pendingGameAppId);
                            _pendingGameStart = false;
                            _wasInGameLastCheck = true; // Now update state since session is actually starting
                            _lastKnownGameName = _pendingGameName;
                            _lastKnownAppId = _pendingGameAppId;
                        }
                    }
                    else if (!isCurrentlyInGame && _pendingGameStart)
                    {
                        // Game disappeared while we were waiting for stability - cancel pending start
                        _logger?.LogDebug($"[UpdateSessionTracking] Game disappeared during stability wait, cancelling pending start for '{_pendingGameName}'");
                        _pendingGameStart = false;
                        // Don't change _wasInGameLastCheck since we never actually started a session
                    }
                    
                    // Update SteamData with current session info
                    UpdateSteamDataWithSessionInfo(steamData);
                    
                    // Note: _wasInGameLastCheck, _lastKnownGameName, and _lastKnownAppId are now updated
                    // only in specific branches above to maintain proper state consistency and prevent
                    // rapid switching from corrupting the session tracking logic.
                    
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
        /// Gets current session information
        /// </summary>
        /// <returns>Current session data or null if no active session</returns>
        public (int sessionMinutes, DateTime? sessionStart, bool isActive) GetCurrentSessionInfo()
        {
            lock (_sessionLock)
            {
                if (_sessionHistory.CurrentSession?.IsActive == true)
                {
                    return (_sessionHistory.CurrentSession.DurationMinutes, _sessionHistory.CurrentSession.StartTime, true);
                }
                return (0, null, false);
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
            
            // Save immediately to create sessions.json file
            SaveSessionHistory();
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
                    
                    // Check for empty or whitespace-only content
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger?.LogDebug("Session file exists but is empty, starting with fresh history");
                        _sessionHistory = new SessionHistory();
                        SaveSessionHistory(); // Immediately save to fix the empty file
                        return;
                    }
                    
                    var loaded = JsonSerializer.Deserialize<SessionHistory>(json);
                    
                    if (loaded != null)
                    {
                        _sessionHistory = loaded;
                        _logger?.LogDebug($"Loaded {_sessionHistory.Sessions.Count} historical sessions from file");
                        
                        // If there was a current session when we last saved, it's now incomplete
                        // (plugin was restarted while in game), so end it
                        if (_sessionHistory.CurrentSession?.IsActive == true)
                        {
                            var incompleteSession = _sessionHistory.CurrentSession;
                            incompleteSession.EndTime = DateTime.Now;
                            _sessionHistory.Sessions.Add(incompleteSession);
                            _sessionHistory.CurrentSession = null;
                            _logger?.LogDebug($"Ended incomplete session from previous run: '{incompleteSession.GameName}' ({incompleteSession.DurationMinutes}m)");
                            SaveSessionHistory(); // Save the updated history
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("Session file exists but deserialized to null, starting fresh");
                        _sessionHistory = new SessionHistory();
                        SaveSessionHistory(); // Save to ensure file is properly formatted
                    }
                }
                else
                {
                    _logger?.LogDebug("No existing session history file found, starting fresh");
                    _sessionHistory = new SessionHistory();
                }
            }
            catch (JsonException jsonEx)
            {
                _logger?.LogError($"JSON parsing error loading session history: {jsonEx.Message}");
                _logger?.LogDebug("Starting with fresh session history due to JSON error");
                _sessionHistory = new SessionHistory(); 
                SaveSessionHistory(); // Save to fix the corrupted file
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error loading session history: {ex.Message}");
                _sessionHistory = new SessionHistory(); // Start fresh if loading fails
                SaveSessionHistory(); // Save to ensure file exists
            }
        }
        
        /// <summary>
        /// Saves session history to file
        /// </summary>
        private void SaveSessionHistory()
        {
            try
            {
                // Update metadata before saving
                _sessionHistory.LastUpdated = DateTime.Now;
                
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
                
                _logger?.LogDebug($"Saved session history to {_sessionFilePath} - {_sessionHistory.TotalSessions} total sessions, {_sessionHistory.TotalPlaytimeMinutes:F1} total minutes");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error saving session history: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Ensures the session file exists and creates it if it doesn't
        /// </summary>
        private void EnsureSessionFileExists()
        {
            try
            {
                bool needsCreation = false;
                
                if (!File.Exists(_sessionFilePath))
                {
                    _logger?.LogDebug($"Session file does not exist, creating: {_sessionFilePath}");
                    needsCreation = true;
                }
                else
                {
                    // Check if file is empty or corrupted
                    var fileInfo = new FileInfo(_sessionFilePath);
                    if (fileInfo.Length == 0)
                    {
                        _logger?.LogDebug($"Session file exists but is empty, recreating: {_sessionFilePath}");
                        needsCreation = true;
                    }
                    else
                    {
                        // Try to read and validate JSON structure
                        try
                        {
                            var testContent = File.ReadAllText(_sessionFilePath);
                            if (string.IsNullOrWhiteSpace(testContent))
                            {
                                _logger?.LogDebug($"Session file contains only whitespace, recreating: {_sessionFilePath}");
                                needsCreation = true;
                            }
                            else
                            {
                                JsonSerializer.Deserialize<SessionHistory>(testContent);
                                _logger?.LogDebug($"Session file already exists and is valid: {_sessionFilePath}");
                            }
                        }
                        catch (JsonException)
                        {
                            _logger?.LogDebug($"Session file exists but contains invalid JSON, recreating: {_sessionFilePath}");
                            needsCreation = true;
                        }
                    }
                }
                
                if (needsCreation)
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(_sessionFilePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        _logger?.LogDebug($"Ensured directory exists: {directory}");
                    }
                    
                    // Create empty session history file with proper structure
                    var emptyHistory = new SessionHistory();
                    var json = JsonSerializer.Serialize(emptyHistory, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    // Write with explicit encoding and verify
                    File.WriteAllText(_sessionFilePath, json, System.Text.Encoding.UTF8);
                    
                    // Verify the file was written correctly
                    var verifyContent = File.ReadAllText(_sessionFilePath);
                    if (string.IsNullOrWhiteSpace(verifyContent))
                    {
                        throw new InvalidOperationException("Failed to write session file - content is empty after write");
                    }
                    
                    _logger?.LogDebug($"Created session file with {verifyContent.Length} characters: {_sessionFilePath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error ensuring session file exists: {ex.Message}");
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