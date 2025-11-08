using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Advanced file-based logging service with batching, throttling, and log rotation
    /// Based on InfoPanel.RTSS implementation
    /// </summary>
    public class FileLoggingService : IDisposable
    {
        #region Fields
        
        private readonly ConfigurationService _configService;
        private readonly string _logFilePath;
        private readonly object _logLock = new();
        private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
        private readonly System.Threading.Timer _flushTimer;
        private readonly Dictionary<string, DateTime> _lastLogTimes = new();
        private readonly Dictionary<string, int> _suppressionCounts = new();
        private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(500);
        
        private StreamWriter? _logWriter;
        private bool _disposed = false;
        private const int MAX_BUFFER_SIZE = 20;
        private const long MAX_LOG_SIZE_BYTES = 5 * 1024 * 1024; // 5MB
        private const int MAX_BACKUP_FILES = 3;
        private const int DEFAULT_BURST_ALLOWANCE = 5;
        
        #endregion

        #region LogEntry Class
        
        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Category { get; set; } = "DEFAULT";
        }
        
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }
        
        #endregion

        #region Constructor
        
        public FileLoggingService(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            
            // Create log file path in plugin directory
            var pluginDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
            _logFilePath = Path.Combine(pluginDirectory, "InfoPanel.SteamAPI-debug.log");
            
            // Initialize timer for batched writing
            _flushTimer = new System.Threading.Timer(FlushLogBuffer, null, _flushInterval, _flushInterval);
            
            InitializeLogging();
        }
        
        #endregion

        #region Initialization
        
        private void InitializeLogging()
        {
            try
            {
                if (_configService.IsDebugLoggingEnabled)
                {
                    // Ensure directory exists
                    var logDirectory = Path.GetDirectoryName(_logFilePath);
                    if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                    
                    // Check for log rotation
                    RotateLogIfNeeded();
                    
                    // Create or append to log file
                    _logWriter = new StreamWriter(_logFilePath, append: true);
                    _logWriter.AutoFlush = true;
                    
                    // Add startup entries to buffer
                    AddLogEntry(LogLevel.Info, "=== SteamAPI Debug Session Started ===");
                    AddLogEntry(LogLevel.Info, $"Plugin Version: 1.0.0");
                    AddLogEntry(LogLevel.Info, $"Log Level: {_configService.DebugLogLevel}");
                    AddLogEntry(LogLevel.Info, $"Log File: {_logFilePath}");
                    
                    // Force immediate flush for startup messages
                    FlushLogBuffer(null);
                }
                else
                {
                    // Debug logging disabled
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileLoggingService] Error initializing logging: {ex.Message}");
                Console.WriteLine($"[FileLoggingService] Stack trace: {ex.StackTrace}");
            }
        }
        
        private void RotateLogIfNeeded()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > MAX_LOG_SIZE_BYTES)
                    {
                        // Rotate existing backups
                        for (int i = MAX_BACKUP_FILES; i > 0; i--)
                        {
                            string oldBackup = $"{_logFilePath}.{i}";
                            string newBackup = $"{_logFilePath}.{i + 1}";
                            
                            if (File.Exists(oldBackup))
                            {
                                if (i == MAX_BACKUP_FILES)
                                    File.Delete(oldBackup); // Delete oldest
                                else
                                    File.Move(oldBackup, newBackup);
                            }
                        }
                        
                        // Move current log to backup
                        File.Move(_logFilePath, $"{_logFilePath}.1");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileLoggingService] Error rotating log: {ex.Message}");
            }
        }
        
        #endregion

        #region Core Logging Methods
        
        private void AddLogEntry(LogLevel level, string message, string category = "DEFAULT")
        {
            if (!_configService.IsDebugLoggingEnabled || _disposed)
                return;
                
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Category = category
            };
            
            _logBuffer.Enqueue(entry);
            
            // Flush immediately if buffer is too large or it's an error
            if (_logBuffer.Count >= MAX_BUFFER_SIZE || level == LogLevel.Error)
            {
                FlushLogBuffer(null);
            }
        }
        
        private void FlushLogBuffer(object? state)
        {
            if (_disposed || _logWriter == null)
                return;
                
            lock (_logLock)
            {
                try
                {
                    var entriesToWrite = new List<LogEntry>();
                    
                    // Dequeue all pending entries
                    while (_logBuffer.TryDequeue(out var entry))
                    {
                        entriesToWrite.Add(entry);
                    }
                    
                    if (entriesToWrite.Count == 0)
                        return;
                    
                    // Write all entries to file
                    foreach (var entry in entriesToWrite)
                    {
                        var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        var logLine = $"[{timestamp}] [SteamAPI] [{entry.Level}] {entry.Message}";
                        _logWriter.WriteLine(logLine);
                    }
                    
                    _logWriter.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileLoggingService] Error flushing log buffer: {ex.Message}");
                }
            }
        }
        
        #endregion

        #region Public Logging Methods
        
        public void LogInfo(string message)
        {
            AddLogEntry(LogLevel.Info, message);
        }
        
        public void LogDebug(string message)
        {
            AddLogEntry(LogLevel.Debug, message);
        }
        
        public void LogWarning(string message)
        {
            AddLogEntry(LogLevel.Warning, message);
        }
        
        public void LogError(string message)
        {
            AddLogEntry(LogLevel.Error, message);
        }
        
        public void LogError(string message, Exception exception)
        {
            AddLogEntry(LogLevel.Error, $"{message}: {exception.Message}");
            AddLogEntry(LogLevel.Error, $"Stack Trace: {exception.StackTrace}");
        }
        
        #endregion

        #region Disposal
        
        public void Dispose()
        {
            if (_disposed)
                return;
                
            try
            {
                AddLogEntry(LogLevel.Info, "SteamAPI logging service disposing...");
                
                // Final flush
                FlushLogBuffer(null);
                
                // Stop timer
                _flushTimer?.Dispose();
                
                // Close writer
                _logWriter?.Close();
                _logWriter?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileLoggingService] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
        
        #endregion
    }
}