using System.Text.Json;
using System.Collections.Concurrent;
using System.Text;
using Timer = System.Threading.Timer;

namespace InfoPanel.SteamAPI.Services
{
    /// <summary>
    /// Enhanced logging service implementing structured logging with delta detection,
    /// performance optimization, and configurable verbosity levels following InfoPanel patterns
    /// </summary>
    public class EnhancedLoggingService : IDisposable
    {
        private readonly string _logFilePath;
        private readonly ConfigurationService? _configService;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly Timer _flushTimer;
        private readonly object _fileLock = new object();
        private readonly Dictionary<string, LogState> _lastLoggedStates;
        private bool _disposed = false;

        public EnhancedLoggingService(string logFilePath, ConfigurationService? configService)
        {
            _logFilePath = logFilePath;
            _configService = configService;
            _logQueue = new ConcurrentQueue<LogEntry>();
            _lastLoggedStates = new Dictionary<string, LogState>();
            
            // Archive previous session's log file BEFORE starting new logging
            ArchivePreviousSessionLog();
            
            // Initialize flush timer based on configuration
            var flushInterval = _configService?.LogFlushInterval ?? 1000;
            _flushTimer = new Timer(FlushLogs, null, flushInterval, flushInterval);
            
            // Log service initialization
            LogInfo("EnhancedLoggingService", "Enhanced logging service initialized", new
            {
                LogLevel = GetMinimumLogLevel().ToString(),
                DeltaLogging = _configService?.EnableDeltaLogging ?? true,
                StructuredLogging = _configService?.EnableStructuredLogging ?? true,
                FlushInterval = $"{flushInterval}ms",
                PerformanceLogging = _configService?.EnablePerformanceLogging ?? true
            });
        }

        /// <summary>
        /// Log information with delta detection to prevent repetitive logging
        /// </summary>
        public void LogInfo(string source, string message, object? data = null)
        {
            if (GetMinimumLogLevel() > LogLevel.Info) return;
            
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Info,
                Source = source,
                Message = message,
                Data = RedactSensitiveData(data)
            };

            // Delta detection for repetitive data
            if (ShouldUseDeltaLogging() && ShouldSkipDeltaLog(source, data))
            {
                return;
            }

            _logQueue.Enqueue(logEntry);
            UpdateLastLoggedState(source, data);
        }

        /// <summary>
        /// Log debug information with smart throttling following InfoPanel patterns
        /// </summary>
        public void LogDebug(string source, string message, object? data = null)
        {
            if (GetMinimumLogLevel() > LogLevel.Debug) return;
            
            // Special handling for high-frequency debug sources
            if (IsHighFrequencySource(source) && ShouldUseDeltaLogging())
            {
                LogDebugWithDelta(source, message, data);
                return;
            }

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Debug,
                Source = source,
                Message = message,
                Data = RedactSensitiveData(data)
            };

            _logQueue.Enqueue(logEntry);
        }

        /// <summary>
        /// Log warnings that may require investigation
        /// </summary>
        public void LogWarning(string source, string message, object? data = null, Exception? exception = null)
        {
            if (GetMinimumLogLevel() > LogLevel.Warning) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Warning,
                Source = source,
                Message = message,
                Data = RedactSensitiveData(data),
                Exception = exception?.ToString()
            };

            _logQueue.Enqueue(logEntry);
        }

        /// <summary>
        /// Log errors that require investigation and potential fixes
        /// </summary>
        public void LogError(string source, string message, Exception? exception = null, object? context = null)
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Error,
                Source = source,
                Message = message,
                Data = RedactSensitiveData(context),
                Exception = exception?.ToString(),
                StackTrace = exception?.StackTrace
            };

            _logQueue.Enqueue(logEntry);
            
            // Force immediate flush for errors following InfoPanel error handling patterns
            FlushLogs(null);
        }

        /// <summary>
        /// Log operation start with correlation ID for pairing - InfoPanel monitoring pattern
        /// </summary>
        public string? LogOperationStart(string source, string operation, object? parameters = null)
        {
            if (!(_configService?.EnableOperationPairing ?? true)) return null;
            
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            
            LogInfo(source, $"OPERATION_START: {operation}", new
            {
                CorrelationId = correlationId,
                Operation = operation,
                Parameters = RedactSensitiveData(parameters),
                StartTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
            });

            return correlationId;
        }

        /// <summary>
        /// Log operation end with timing information - InfoPanel performance pattern
        /// </summary>
        public void LogOperationEnd(string source, string operation, string? correlationId, 
            TimeSpan duration, bool success = true, object? result = null)
        {
            if (!(_configService?.EnableOperationPairing ?? true) || string.IsNullOrEmpty(correlationId)) return;
            
            var level = success ? LogLevel.Info : LogLevel.Warning;
            var enablePerfLogging = _configService?.EnablePerformanceLogging ?? true;
            
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Source = source,
                Message = $"OPERATION_END: {operation}",
                Data = new
                {
                    CorrelationId = correlationId,
                    Operation = operation,
                    Duration = enablePerfLogging ? $"{duration.TotalMilliseconds:F1}ms" : "N/A",
                    Success = success,
                    Result = RedactSensitiveData(result),
                    EndTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }
            };

            _logQueue.Enqueue(logEntry);
        }

        private void LogDebugWithDelta(string source, string message, object? data)
        {
            var stateKey = $"{source}_{message.GetHashCode()}";
            
            // Check if data has changed since last log
            if (HasDataChanged(stateKey, data))
            {
                // Log the change with comparison
                var previousState = _lastLoggedStates.ContainsKey(stateKey) ? _lastLoggedStates[stateKey] : null;
                
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = LogLevel.Debug,
                    Source = source,
                    Message = $"DELTA: {message}",
                    Data = new
                    {
                        Current = RedactSensitiveData(data),
                        Previous = previousState?.Data,
                        Changes = GetChangeSummary(previousState?.Data, data),
                        DeltaType = "DataChanged"
                    }
                };

                _logQueue.Enqueue(logEntry);
                UpdateLastLoggedState(stateKey, data);
            }
            else
            {
                // Periodically log "stable state" status (every 5 minutes) - InfoPanel monitoring pattern
                LogPeriodicStatus(stateKey, source, message, data);
            }
        }

        private bool ShouldSkipDeltaLog(string source, object? data)
        {
            if (!ShouldUseDeltaLogging()) return false;
            
            return !HasDataChanged(source, data);
        }

        private bool HasDataChanged(string stateKey, object? data)
        {
            if (!_lastLoggedStates.ContainsKey(stateKey)) return true;
            
            var lastState = _lastLoggedStates[stateKey];
            var currentJson = JsonSerializer.Serialize(RedactSensitiveData(data) ?? new { });
            var lastJson = JsonSerializer.Serialize(lastState.Data ?? new { });
            
            return currentJson != lastJson;
        }

        private object GetChangeSummary(object? previous, object? current)
        {
            // Simple change detection for InfoPanel debugging
            if (previous == null) return new { Type = "Initial", Description = "First time logging this data" };
            
            try
            {
                var prevJson = JsonSerializer.Serialize(previous);
                var currJson = JsonSerializer.Serialize(RedactSensitiveData(current));
                
                return new
                {
                    Type = "Modified",
                    PreviousSize = prevJson.Length,
                    CurrentSize = currJson.Length,
                    SizeChange = currJson.Length - prevJson.Length,
                    Changed = prevJson != currJson
                };
            }
            catch
            {
                return new { Type = "Unknown", Description = "Unable to compare data" };
            }
        }

        private void UpdateLastLoggedState(string stateKey, object? data)
        {
            _lastLoggedStates[stateKey] = new LogState
            {
                Data = RedactSensitiveData(data),
                LastLogged = DateTime.UtcNow
            };
        }

        private bool IsHighFrequencySource(string source)
        {
            // InfoPanel plugin patterns - identify high frequency monitoring sources
            var highFrequencySources = new[] { "SOCIAL", "LIBRARY", "PLAYER", "TIMER", "MonitoringService", "PlayerDataService", "SocialDataService", "LibraryDataService" };
            return highFrequencySources.Any(hf => source.Contains(hf, StringComparison.OrdinalIgnoreCase));
        }

        private void LogPeriodicStatus(string stateKey, string source, string message, object? data)
        {
            var statusKey = $"{stateKey}_status";
            var now = DateTime.UtcNow;
            
            if (!_lastLoggedStates.ContainsKey(statusKey) || 
                _lastLoggedStates[statusKey].LastLogged < now.AddMinutes(-5))
            {
                var logEntry = new LogEntry
                {
                    Timestamp = now,
                    Level = LogLevel.Debug,
                    Source = source,
                    Message = $"STATUS: {message} (stable)",
                    Data = new 
                    { 
                        Status = "NoChange", 
                        StableSince = _lastLoggedStates.ContainsKey(stateKey) ? _lastLoggedStates[stateKey].LastLogged : now,
                        LastData = RedactSensitiveData(data)
                    }
                };

                _logQueue.Enqueue(logEntry);
                _lastLoggedStates[statusKey] = new LogState { LastLogged = now };
            }
        }

        private LogLevel GetMinimumLogLevel()
        {
            var levelStr = _configService?.MinimumLogLevel ?? "Info";
            return Enum.TryParse<LogLevel>(levelStr, true, out var level) ? level : LogLevel.Info;
        }

        private bool ShouldUseDeltaLogging() => _configService?.EnableDeltaLogging ?? true;

        private object? RedactSensitiveData(object? data)
        {
            if (!(_configService?.EnableSensitiveDataRedaction ?? true) || data == null) return data;

            try
            {
                var json = JsonSerializer.Serialize(data);
                
                // Redact common sensitive patterns following InfoPanel security practices
                json = System.Text.RegularExpressions.Regex.Replace(json, @"""[Aa]pi[Kk]ey""\s*:\s*""[^""]+""", @"""apiKey"":""[REDACTED]""");
                json = System.Text.RegularExpressions.Regex.Replace(json, @"""[Tt]oken""\s*:\s*""[^""]+""", @"""token"":""[REDACTED]""");
                json = System.Text.RegularExpressions.Regex.Replace(json, @"""[Pp]assword""\s*:\s*""[^""]+""", @"""password"":""[REDACTED]""");
                
                return JsonSerializer.Deserialize<object>(json);
            }
            catch
            {
                return data; // Return original if redaction fails
            }
        }

        private void FlushLogs(object? state)
        {
            if (_logQueue.IsEmpty) return;

            var entriesToFlush = new List<LogEntry>();
            
            // Dequeue all pending entries
            while (_logQueue.TryDequeue(out var entry))
            {
                entriesToFlush.Add(entry);
            }

            if (entriesToFlush.Count == 0) return;

            try
            {
                lock (_fileLock)
                {
                    using var writer = new StreamWriter(_logFilePath, true, System.Text.Encoding.UTF8);
                    
                    foreach (var entry in entriesToFlush)
                    {
                        var logLine = (_configService?.EnableStructuredLogging ?? true) ? 
                            FormatStructuredLog(entry) : 
                            FormatTraditionalLog(entry);
                            
                        writer.WriteLine(logLine);
                    }
                }

                // Manage log file size following InfoPanel patterns
                ManageLogFileSize();
            }
            catch (Exception ex)
            {
                // Fallback logging to console following InfoPanel error patterns
                Console.WriteLine($"[EnhancedLoggingService] Failed to write logs: {ex.Message}");
            }
        }

        private string FormatStructuredLog(LogEntry entry)
        {
            var logObject = new
            {
                timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = entry.Level.ToString().ToUpper(),
                source = entry.Source,
                message = entry.Message,
                data = entry.Data,
                exception = entry.Exception,
                stackTrace = entry.StackTrace
            };

            return JsonSerializer.Serialize(logObject, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        private string FormatTraditionalLog(LogEntry entry)
        {
            var level = entry.Level.ToString().ToUpper().PadRight(7);
            var dataStr = entry.Data != null ? $" | Data: {JsonSerializer.Serialize(entry.Data)}" : "";
            var exceptionStr = !string.IsNullOrEmpty(entry.Exception) ? $" | Exception: {entry.Exception}" : "";
            
            return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{entry.Source}] {entry.Message}{dataStr}{exceptionStr}";
        }

        private void ManageLogFileSize()
        {
            try
            {
                var fileInfo = new FileInfo(_logFilePath);
                var maxSizeMB = _configService?.LogRotationSizeMB ?? 5;
                
                if (fileInfo.Exists && fileInfo.Length > maxSizeMB * 1024 * 1024)
                {
                    // Rotate log files following InfoPanel patterns
                    var directory = Path.GetDirectoryName(_logFilePath);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
                    var extension = Path.GetExtension(_logFilePath);
                    
                    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    var archivedPath = Path.Combine(directory, $"{fileNameWithoutExt}-{timestamp}{extension}");
                    
                    File.Move(_logFilePath, archivedPath);
                    
                    // Clean up old archived files
                    CleanupArchivedLogs(directory, fileNameWithoutExt, extension);
                }
            }
            catch
            {
                // Ignore rotation errors - continue with current file following InfoPanel resilience patterns
            }
        }

        private void CleanupArchivedLogs(string directory, string baseFileName, string extension)
        {
            try
            {
                var maxArchived = _configService?.MaxArchivedLogs ?? 5;
                var pattern = $"{baseFileName}-*{extension}";
                var archivedFiles = Directory.GetFiles(directory, pattern)
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Skip(maxArchived);

                foreach (var file in archivedFiles)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors following InfoPanel patterns
            }
        }
        
        /// <summary>
        /// Archives the previous session's log file on plugin startup, ensuring fresh logs each session
        /// </summary>
        private void ArchivePreviousSessionLog()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var directory = Path.GetDirectoryName(_logFilePath) ?? "";
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
                    var extension = Path.GetExtension(_logFilePath);
                    
                    // Create archive filename with timestamp
                    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    var archivedPath = Path.Combine(directory, $"debug-{timestamp}{extension}");
                    
                    Console.WriteLine($"[EnhancedLoggingService] Archiving previous session log: {_logFilePath} -> {archivedPath}");
                    
                    // Move previous session's log file to archived name
                    File.Move(_logFilePath, archivedPath);
                    
                    Console.WriteLine($"[EnhancedLoggingService] Previous session log archived successfully");
                    
                    // Clean up old archived debug files
                    CleanupDebugArchives(directory, extension);
                }
                else
                {
                    Console.WriteLine($"[EnhancedLoggingService] No previous session log file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                // Log to console if archiving fails - not fatal, just continue with fresh log
                Console.WriteLine($"[EnhancedLoggingService] Failed to archive previous session log: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                LogInfo("EnhancedLoggingService", "Shutting down enhanced logging service");
                _flushTimer?.Dispose();
                FlushLogs(null); // Final flush
                
                // Small delay to ensure file handles are fully released
                System.Threading.Thread.Sleep(100);
                
                // Archive the log file with timestamp
                ArchiveLogFileOnClose();
            }
            catch
            {
                // Ignore disposal errors following InfoPanel patterns
            }
            finally
            {
                _disposed = true;
            }
        }
        
        /// <summary>
        /// Archives the current log file with a timestamp and deletes the original,
        /// ensuring a fresh start for the next plugin run. Also archives any rotated files.
        /// </summary>
        private void ArchiveLogFileOnClose()
        {
            try
            {
                Console.WriteLine($"[EnhancedLoggingService] Attempting to archive log file: {_logFilePath}");
                
                lock (_fileLock)
                {
                    if (File.Exists(_logFilePath))
                    {
                        var directory = Path.GetDirectoryName(_logFilePath);
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
                        var extension = Path.GetExtension(_logFilePath);
                        
                        // Create archive filename with timestamp
                        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        var archivedPath = Path.Combine(directory ?? "", $"debug-{timestamp}{extension}");
                        
                        Console.WriteLine($"[EnhancedLoggingService] Moving {_logFilePath} to {archivedPath}");
                        
                        // Move current log file to archived name
                        File.Move(_logFilePath, archivedPath);
                        
                        Console.WriteLine($"[EnhancedLoggingService] Successfully archived log file");
                        
                        // Also rename any rotated files from this session (with matching timestamp pattern)
                        ArchiveRotatedFiles(directory, fileNameWithoutExt, extension, timestamp);
                        
                        // Clean up old archived debug files
                        CleanupDebugArchives(directory, extension);
                    }
                    else
                    {
                        Console.WriteLine($"[EnhancedLoggingService] Log file does not exist: {_logFilePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log to console if archiving fails
                Console.WriteLine($"[EnhancedLoggingService] Failed to archive log file: {ex.Message}");
                Console.WriteLine($"[EnhancedLoggingService] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Archives any rotated log files from the current session
        /// </summary>
        private void ArchiveRotatedFiles(string directory, string baseFileName, string extension, string sessionTimestamp)
        {
            try
            {
                // Find rotated files (e.g., InfoPanel.SteamAPI.dll_enhanced-20250111-184523.json)
                var rotatedPattern = $"{baseFileName}-*{extension}";
                var rotatedFiles = Directory.GetFiles(directory, rotatedPattern);
                
                foreach (var rotatedFile in rotatedFiles)
                {
                    var rotatedFileName = Path.GetFileNameWithoutExtension(rotatedFile);
                    
                    // Skip if already renamed to debug-* format
                    if (rotatedFileName.StartsWith("debug-"))
                        continue;
                    
                    // Extract the rotation timestamp from the filename
                    var parts = rotatedFileName.Split('-');
                    if (parts.Length >= 2)
                    {
                        var rotationTimestamp = string.Join("-", parts.Skip(parts.Length - 2));
                        var newRotatedPath = Path.Combine(directory, $"debug-{rotationTimestamp}{extension}");
                        
                        // Only move if the destination doesn't exist
                        if (!File.Exists(newRotatedPath))
                        {
                            File.Move(rotatedFile, newRotatedPath);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors renaming rotated files
            }
        }
        
        /// <summary>
        /// Cleans up old debug archive files, keeping only the most recent ones
        /// </summary>
        private void CleanupDebugArchives(string directory, string extension)
        {
            try
            {
                var maxArchived = _configService?.MaxArchivedLogs ?? 5;
                var pattern = $"debug-*{extension}";
                var archivedFiles = Directory.GetFiles(directory, pattern)
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Skip(maxArchived);

                foreach (var file in archivedFiles)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors following InfoPanel patterns
            }
        }
    }

    /// <summary>
    /// Structured log entry for enhanced logging
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? Exception { get; set; }
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// Log state for delta detection
    /// </summary>
    public class LogState
    {
        public object? Data { get; set; }
        public DateTime LastLogged { get; set; }
    }

    /// <summary>
    /// Logging levels following C# best practices
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,   // Function calls and returns (rarely used)
        Debug = 1,   // Low-level diagnostic information
        Info = 2,    // Normal operation information
        Warning = 3, // Potential issues that may need investigation
        Error = 4,   // Failures requiring investigation and fixes
        Critical = 5 // System-threatening failures requiring immediate attention
    }
}