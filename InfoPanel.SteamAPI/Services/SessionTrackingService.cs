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
        public string? BannerUrl { get; set; }  // Store banner URL with session
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
        
        // Last played game tracking (persists across plugin restarts)
        public string? LastPlayedGameName { get; set; }
        public int LastPlayedGameAppId { get; set; }
        public string? LastPlayedGameBannerUrl { get; set; }
        public DateTime? LastPlayedTimestamp { get; set; }
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
        private string? _lastKnownBannerUrl;  // Track banner URL for current session
        private bool _wasInGameLastCheck;
        
        // Session stability tracking to prevent rapid cycling
        private DateTime _lastStateChangeTime = DateTime.Now;
        private bool _pendingGameStart = false;
        private string? _pendingGameName;
        private int _pendingGameAppId;
        private string? _pendingBannerUrl;  // Track pending banner URL
        
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
                _enhancedLogger.LogInfo("SessionTrackingService.Constructor", "SessionTrackingService initialized", new
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

        #region Public Properties
        
        /// <summary>
        /// Gets whether there is currently an active game session
        /// </summary>
        public bool HasActiveSession => _sessionHistory.CurrentSession?.IsActive == true;
        
        /// <summary>
        /// Gets the banner URL for the current active session (persists during alt-tab)
        /// </summary>
        public string? CurrentSessionBannerUrl => _lastKnownBannerUrl;
        
        /// <summary>
        /// Gets the game name for the current active session
        /// </summary>
        public string? CurrentSessionGameName => _lastKnownGameName;
        
        /// <summary>
        /// Gets the app ID for the current active session
        /// </summary>
        public int CurrentSessionAppId => _lastKnownAppId;
        
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
                    var currentBannerUrl = steamData.CurrentGameBannerUrl;
                    var now = DateTime.Now;
                    
                    _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Update session state", new {
                        IsInGame = isCurrentlyInGame,
                        GameName = currentGameName ?? "None",
                        AppId = currentAppId,
                        BannerUrl = currentBannerUrl,
                        WasInGameLastCheck = _wasInGameLastCheck
                    });
                    
                    // Update banner URL if we have one for the current session
                    if (!string.IsNullOrEmpty(currentBannerUrl) && isCurrentlyInGame)
                    {
                        _lastKnownBannerUrl = currentBannerUrl;
                        
                        // Update current session banner if session is active
                        if (_sessionHistory.CurrentSession?.IsActive == true)
                        {
                            _sessionHistory.CurrentSession.BannerUrl = currentBannerUrl;
                        }
                    }
                    
                    // Handle state changes with debouncing to prevent rapid cycling
                    if (isCurrentlyInGame && !_wasInGameLastCheck)
                    {
                        // Game detected - but wait before starting session to avoid false positives
                        _pendingGameStart = true;
                        _pendingGameName = currentGameName;
                        _pendingGameAppId = currentAppId;
                        _pendingBannerUrl = currentBannerUrl;
                        _lastStateChangeTime = now;
                        _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Game detected, pending start - waiting for stability", new {
                            GameName = currentGameName,
                            WaitSeconds = STATE_CHANGE_DEBOUNCE_SECONDS
                        });
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
                                _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Ending session", new {
                                    DurationMinutes = Math.Round(sessionDuration.TotalMinutes, 1)
                                });
                                // Use banner from session (persisted), not from steamData (likely empty when quitting)
                                EndCurrentSession(_sessionHistory.CurrentSession.BannerUrl);
                                _wasInGameLastCheck = false; // Only update state when actually ending session
                            }
                            else
                            {
                                _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Game ended too quickly, ignoring - may be API glitch", new {
                                    DurationSeconds = Math.Round(sessionDuration.TotalSeconds, 0)
                                });
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
                                    _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Game changed - switching sessions", new {
                                        OldGame = _lastKnownGameName,
                                        NewGame = currentGameName
                                    });
                                    // Use banner from session (persisted), not from steamData (may be empty during transition)
                                    EndCurrentSession(_sessionHistory.CurrentSession.BannerUrl);
                                    StartNewSession(currentGameName, currentAppId, currentBannerUrl);
                                    _wasInGameLastCheck = true; // Maintain in-game state
                                    _lastKnownGameName = currentGameName;
                                    _lastKnownAppId = currentAppId;
                                    _lastKnownBannerUrl = currentBannerUrl;
                                }
                                else
                                {
                                    _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Game change detected too quickly, ignoring - may be API inconsistency");
                                    return;
                                }
                            }
                        }
                        // Continue current session - update known game info but don't change state
                        _lastKnownGameName = currentGameName;
                        _lastKnownAppId = currentAppId;
                        if (!string.IsNullOrEmpty(currentBannerUrl))
                        {
                            _lastKnownBannerUrl = currentBannerUrl;
                        }
                    }
                    else if (_pendingGameStart && (now - _lastStateChangeTime).TotalSeconds >= STATE_CHANGE_DEBOUNCE_SECONDS)
                    {
                        // Pending game start has been stable long enough, start the session
                        if (isCurrentlyInGame && !string.IsNullOrEmpty(_pendingGameName))
                        {
                            _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Starting stable session after debounce delay", new {
                                GameName = _pendingGameName,
                                DelaySeconds = STATE_CHANGE_DEBOUNCE_SECONDS
                            });
                            StartNewSession(_pendingGameName, _pendingGameAppId, _pendingBannerUrl);
                            _pendingGameStart = false;
                            _wasInGameLastCheck = true; // Now update state since session is actually starting
                            _lastKnownGameName = _pendingGameName;
                            _lastKnownAppId = _pendingGameAppId;
                            _lastKnownBannerUrl = _pendingBannerUrl;
                        }
                    }
                    else if (!isCurrentlyInGame && _pendingGameStart)
                    {
                        // Game disappeared while we were waiting for stability - cancel pending start
                        _enhancedLogger?.LogDebug("SessionTrackingService.UpdateSessionTracking", "Game disappeared during stability wait, cancelling pending start", new {
                            GameName = _pendingGameName
                        });
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
                    _enhancedLogger?.LogError("SessionTrackingService.UpdateSessionTracking", "Error updating session tracking", ex);
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
                    // Note: banner URL not available during shutdown, but session info will be saved
                    EndCurrentSession(bannerUrl: null);
                    SaveSessionHistory();
                }
            }
        }
        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Starts a new gaming session
        /// </summary>
        private void StartNewSession(string? gameName, int appId, string? bannerUrl = null)
        {
            if (string.IsNullOrEmpty(gameName) || appId <= 0)
                return;
                
            var newSession = new GameSession
            {
                GameName = gameName,
                AppId = appId,
                BannerUrl = bannerUrl,
                StartTime = DateTime.Now
            };
            
            _sessionHistory.CurrentSession = newSession;
            
            // Store banner URL for persistence
            _lastKnownBannerUrl = bannerUrl;
            
            _enhancedLogger?.LogDebug("SessionTrackingService.StartNewSession", "Started new session", new {
                GameName = gameName,
                AppId = appId,
                BannerUrl = bannerUrl,
                StartTime = newSession.StartTime.ToString("HH:mm:ss")
            });
            
            // Save immediately to create sessions.json file
            SaveSessionHistory();
        }
        
        /// <summary>
        /// Ends the current gaming session and stores it as the last played game
        /// </summary>
        private void EndCurrentSession(string? bannerUrl = null)
        {
            if (_sessionHistory.CurrentSession?.IsActive == true)
            {
                _sessionHistory.CurrentSession.EndTime = DateTime.Now;
                var duration = _sessionHistory.CurrentSession.DurationMinutes;
                
                // Store as last played game for display persistence
                _sessionHistory.LastPlayedGameName = _sessionHistory.CurrentSession.GameName;
                _sessionHistory.LastPlayedGameAppId = _sessionHistory.CurrentSession.AppId;
                _sessionHistory.LastPlayedGameBannerUrl = bannerUrl;
                _sessionHistory.LastPlayedTimestamp = DateTime.Now;
                
                // Add to session history
                _sessionHistory.Sessions.Add(_sessionHistory.CurrentSession);
                
                _enhancedLogger?.LogInfo("SessionTrackingService.EndCurrentSession", "Ended session and saved as last played", new {
                    GameName = _sessionHistory.CurrentSession.GameName,
                    AppId = _sessionHistory.CurrentSession.AppId,
                    DurationMinutes = duration,
                    DurationFormatted = _sessionHistory.CurrentSession.DurationFormatted,
                    HasBannerUrl = !string.IsNullOrEmpty(bannerUrl)
                });
                
                _sessionHistory.CurrentSession = null;
            }
        }
        
        /// <summary>
        /// Updates SteamData with current session information and last played game
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
            
            // Update last played game info (persisted from previous session)
            steamData.LastPlayedGameName = _sessionHistory.LastPlayedGameName;
            steamData.LastPlayedGameAppId = _sessionHistory.LastPlayedGameAppId;
            steamData.LastPlayedGameBannerUrl = _sessionHistory.LastPlayedGameBannerUrl;
            steamData.LastPlayedTimestamp = _sessionHistory.LastPlayedTimestamp;
            
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
                        _enhancedLogger?.LogDebug("SessionTrackingService.LoadSessionHistory", "Session file exists but is empty, starting with fresh history");
                        _sessionHistory = new SessionHistory();
                        SaveSessionHistory(); // Immediately save to fix the empty file
                        return;
                    }
                    
                    var loaded = JsonSerializer.Deserialize<SessionHistory>(json);
                    
                    if (loaded != null)
                    {
                        _sessionHistory = loaded;
                        _enhancedLogger?.LogDebug("SessionTrackingService.LoadSessionHistory", "Loaded historical sessions from file", new {
                            SessionCount = _sessionHistory.Sessions.Count,
                            HasCurrentSession = _sessionHistory.CurrentSession != null
                        });
                        
                        // If there was a current session when we last saved, it's now incomplete
                        // (plugin was restarted while in game), so end it
                        if (_sessionHistory.CurrentSession?.IsActive == true)
                        {
                            var incompleteSession = _sessionHistory.CurrentSession;
                            incompleteSession.EndTime = DateTime.Now;
                            _sessionHistory.Sessions.Add(incompleteSession);
                            _sessionHistory.CurrentSession = null;
                            _enhancedLogger?.LogDebug("SessionTrackingService.LoadSessionHistory", "Ended incomplete session from previous run", new {
                                GameName = incompleteSession.GameName,
                                DurationMinutes = incompleteSession.DurationMinutes
                            });
                            SaveSessionHistory(); // Save the updated history
                        }
                    }
                    else
                    {
                        _enhancedLogger?.LogDebug("SessionTrackingService.LoadSessionHistory", "Session file exists but deserialized to null, starting fresh");
                        _sessionHistory = new SessionHistory();
                        SaveSessionHistory(); // Save to ensure file is properly formatted
                    }
                }
                else
                {
                    _enhancedLogger?.LogDebug("SessionTrackingService.LoadSessionHistory", "No existing session history file found, starting fresh");
                    _sessionHistory = new SessionHistory();
                }
            }
            catch (JsonException jsonEx)
            {
                _enhancedLogger?.LogError("SessionTrackingService.LoadSessionHistory", "JSON parsing error loading session history", jsonEx);
                _enhancedLogger?.LogDebug("SessionTrackingService.LoadSessionHistory", "Starting with fresh session history due to JSON error");
                _sessionHistory = new SessionHistory(); 
                SaveSessionHistory(); // Save to fix the corrupted file
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SessionTrackingService.LoadSessionHistory", "Error loading session history", ex);
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
                
                _enhancedLogger?.LogDebug("SessionTrackingService.SaveSessionHistory", "Saved session history", new {
                    FilePath = _sessionFilePath,
                    TotalSessions = _sessionHistory.TotalSessions,
                    TotalPlaytimeMinutes = Math.Round(_sessionHistory.TotalPlaytimeMinutes, 1)
                });
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SessionTrackingService.SaveSessionHistory", "Error saving session history", ex);
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
                    _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Session file does not exist, creating", new {
                        FilePath = _sessionFilePath
                    });
                    needsCreation = true;
                }
                else
                {
                    // Check if file is empty or corrupted
                    var fileInfo = new FileInfo(_sessionFilePath);
                    if (fileInfo.Length == 0)
                    {
                        _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Session file exists but is empty, recreating", new {
                            FilePath = _sessionFilePath
                        });
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
                                _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Session file contains only whitespace, recreating", new {
                                    FilePath = _sessionFilePath
                                });
                                needsCreation = true;
                            }
                            else
                            {
                                JsonSerializer.Deserialize<SessionHistory>(testContent);
                                _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Session file already exists and is valid", new {
                                    FilePath = _sessionFilePath
                                });
                            }
                        }
                        catch (JsonException)
                        {
                            _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Session file exists but contains invalid JSON, recreating", new {
                                FilePath = _sessionFilePath
                            });
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
                        _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Ensured directory exists", new {
                            Directory = directory
                        });
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
                    
                    _enhancedLogger?.LogDebug("SessionTrackingService.EnsureSessionFileExists", "Created session file", new {
                        FilePath = _sessionFilePath,
                        ContentLength = verifyContent.Length
                    });
                }
            }
            catch (Exception ex)
            {
                _enhancedLogger?.LogError("SessionTrackingService.EnsureSessionFileExists", "Error ensuring session file exists", ex);
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
                _enhancedLogger?.LogDebug("SessionTrackingService.CleanupOldSessions", "Cleaned up old sessions", new {
                    RemovedCount = removedCount,
                    OlderThanDays = 30
                });
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
            _enhancedLogger?.LogDebug("SessionTrackingService.Dispose", "SessionTrackingService disposed");
        }
        
        #endregion
    }
}