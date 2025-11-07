// InfoPanel.SteamAPI v1.0.0 - InfoPanel Plugin Template
using InfoPanel.Plugins;
using InfoPanel.SteamAPI.Services;
using InfoPanel.SteamAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI
{
    /// <summary>
    /// Template plugin for InfoPanel - Get data from SteamAPI
    /// 
    /// This template provides a solid foundation for creating new InfoPanel plugins with:
    /// - Service-based architecture
    /// - Event-driven data flow
    /// - Thread-safe sensor updates
    /// - Proper resource management
    /// - Configuration support
    /// 
    /// TODO: Customize this plugin for your specific monitoring needs
    /// </summary>
    public class SteamAPIMain : BasePlugin
    {
        #region Configuration
        
        // Configuration file path - exposed to InfoPanel for direct file access
        private string? _configFilePath;
        
        /// <summary>
        /// Exposes the configuration file path to InfoPanel for the "Open Config" button
        /// </summary>
        public override string? ConfigFilePath => _configFilePath;
        
        #endregion

        #region Sensors
        
        // Steam Profile Sensors
        private readonly PluginText _playerNameSensor = new("player-name", "Player Name", "Unknown");
        private readonly PluginText _onlineStatusSensor = new("online-status", "Status", "Offline");
        private readonly PluginSensor _steamLevelSensor = new("steam-level", "Steam Level", 0, "");
        
        // Current Game Sensors
        private readonly PluginText _currentGameSensor = new("current-game", "Current Game", "Not Playing");
        private readonly PluginSensor _currentGamePlaytimeSensor = new("current-game-playtime", "Game Playtime", 0, "hrs");
        
        // Library Statistics Sensors
        private readonly PluginSensor _totalGamesSensor = new("total-games", "Games Owned", 0, "");
        private readonly PluginSensor _totalPlaytimeSensor = new("total-playtime", "Total Playtime", 0, "hrs");
        private readonly PluginSensor _recentPlaytimeSensor = new("recent-playtime", "Recent Playtime", 0, "hrs");
        
        // Status and Details
        private readonly PluginText _statusSensor = new("status", "Plugin Status", "Initializing...");
        private readonly PluginText _detailsSensor = new("details", "Details", "Loading Steam data...");
        
        #endregion

        #region Services
        
        private MonitoringService? _monitoringService;
        private SensorManagementService? _sensorService;
        private ConfigurationService? _configService;
        private CancellationTokenSource? _cancellationTokenSource;
        
        #endregion

        #region Constructor & Initialization
        
        public SteamAPIMain() : base("InfoPanel.SteamAPI", "Steam Data", "Get data from SteamAPI")
        {
            try
            {
                // Note: _configFilePath will be set in Initialize()
                // ConfigurationService will be initialized after we have the path
                
                // TODO: Add any additional initialization logic here that doesn't require configuration
                
            }
            catch (Exception ex)
            {
                // Log initialization errors
                Console.WriteLine($"[SteamAPI] Error during initialization: {ex.Message}");
                throw;
            }
        }

        public override void Initialize()
        {
            // This method may be called by InfoPanel framework
            // Our main initialization is in Load() method
        }

        public override void Load(List<IPluginContainer> containers)
        {
            try
            {
                // Set up configuration file path for InfoPanel integration
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string basePath = assembly.ManifestModule.FullyQualifiedName;
                _configFilePath = $"{basePath}.ini";
                
                Console.WriteLine($"[SteamAPI] Config file path: {_configFilePath}");
                
                // Initialize services now that we have the config path
                _configService = new ConfigurationService(_configFilePath);
                _sensorService = new SensorManagementService(_configService);
                _monitoringService = new MonitoringService(_configService);
                
                // Subscribe to events
                _monitoringService.DataUpdated += OnDataUpdated;
                
                // Create sensor container
                var container = new PluginContainer("SteamAPI", "Basic Steam Data");
                
                // Add Steam sensors to container using the Entries collection
                container.Entries.Add(_playerNameSensor);
                container.Entries.Add(_onlineStatusSensor);
                container.Entries.Add(_steamLevelSensor);
                container.Entries.Add(_currentGameSensor);
                container.Entries.Add(_currentGamePlaytimeSensor);
                container.Entries.Add(_totalGamesSensor);
                container.Entries.Add(_totalPlaytimeSensor);
                container.Entries.Add(_recentPlaytimeSensor);
                container.Entries.Add(_statusSensor);
                container.Entries.Add(_detailsSensor);
                
                Console.WriteLine($"[SteamAPI] Added {container.Entries.Count} sensors to container '{container.Name}'");
                
                // Register container with InfoPanel
                containers.Add(container);
                
                // Start monitoring
                _cancellationTokenSource = new CancellationTokenSource();
                _ = StartMonitoringAsync(_cancellationTokenSource.Token);
                
                Console.WriteLine("[SteamAPI] Steam Data plugin loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error during plugin initialization: {ex.Message}");
                throw;
            }
        }

        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(_configService?.UpdateIntervalSeconds ?? 30);

        public override void Update()
        {
            // For synchronous updates if needed
            // Most of our work is done asynchronously in StartMonitoringAsync
        }

        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            // For async updates - the monitoring service handles timing automatically
            await Task.CompletedTask;
        }

        public override void Close()
        {
            try
            {
                // Cancel monitoring
                _cancellationTokenSource?.Cancel();
                
                // Unsubscribe from events
                if (_monitoringService != null)
                {
                    _monitoringService.DataUpdated -= OnDataUpdated;
                }
                
                // Dispose services
                _monitoringService?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Console.WriteLine("[SteamAPI] Plugin closed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error during close: {ex.Message}");
            }
        }
        
        #endregion

        #region Monitoring
        
        private async Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            try
            {
                // TODO: Implement your monitoring logic here
                // This is where you start your data collection process
                
                if (_monitoringService != null)
                {
                    await _monitoringService.StartMonitoringAsync(cancellationToken);
                }
                
                // Example: You might also start additional monitoring tasks
                // _ = MonitorSystemResourcesAsync(cancellationToken);
                // _ = MonitorNetworkConnectivityAsync(cancellationToken);
                
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Console.WriteLine("[SteamAPI] Monitoring cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error in monitoring: {ex.Message}");
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void OnDataUpdated(object? sender, DataUpdatedEventArgs e)
        {
            try
            {
                // Update Steam sensors with data from monitoring service
                if (_sensorService != null && e.Data != null)
                {
                    _sensorService.UpdateSteamSensors(
                        _playerNameSensor,
                        _onlineStatusSensor,
                        _steamLevelSensor,
                        _currentGameSensor,
                        _currentGamePlaytimeSensor,
                        _totalGamesSensor,
                        _totalPlaytimeSensor,
                        _recentPlaytimeSensor,
                        _statusSensor,
                        _detailsSensor,
                        e.Data
                    );
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI] Error updating sensors: {ex.Message}");
                _statusSensor.Value = "Error updating data";
            }
        }
        
        #endregion

        #region TODO: Add Your Custom Methods Here
        
        // TODO: Add any plugin-specific methods you need
        // Examples:
        
        // private async Task MonitorSystemResourcesAsync(CancellationToken cancellationToken)
        // {
        //     // Monitor CPU, memory, disk, etc.
        // }
        
        // private async Task MonitorNetworkConnectivityAsync(CancellationToken cancellationToken)
        // {
        //     // Monitor network connectivity, bandwidth, etc.
        // }
        
        // private void ProcessSpecialEvents(SpecialEventData eventData)
        // {
        //     // Handle special events or conditions
        // }
        
        #endregion
    }
}