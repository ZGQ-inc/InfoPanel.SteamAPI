# Changelog

All notable changes to InfoPanel Steam API Plugin will be documented in this file.

## [1.1.0] - 2025-11-09

### 🚀 Major Performance Improvements
- **NEW: Tiered Monitoring System** - Revolutionary 3-tier update architecture for optimal performance
  - **Fast Tier (5s)**: Critical real-time data - game state detection, player status, session tracking
  - **Medium Tier (15s)**: Social data - friends status, friends activity
  - **Slow Tier (60s)**: Static data - library statistics, achievements, news
- **3x Faster Game Detection** - Game start/stop detection now occurs within 5 seconds (previously 15s)
- **Enhanced Responsiveness** - Near real-time updates for critical gaming state changes
- **Smart API Optimization** - Intelligent Steam API usage prioritizing important data

### 🏗️ Major Architecture Refactor
- **Service-Oriented Architecture** - Completely restructured from monolithic service to specialized services
- **PlayerDataService** - Dedicated service for real-time player profile and game state data
- **SocialDataService** - Specialized service for friends lists and community features  
- **LibraryDataService** - Focused service for game library and playtime statistics
- **GameStatsService** - Dedicated service for achievements and detailed game analytics
- **MonitoringService Orchestration** - Refactored to coordinate all specialized services

### ⚙️ Enhanced Configuration
- **NEW: Performance Tuning Settings** - Configurable update intervals for each tier
  - `FastUpdateIntervalSeconds=5` - Control real-time data update frequency
  - `MediumUpdateIntervalSeconds=15` - Control social data update frequency  
  - `SlowUpdateIntervalSeconds=60` - Control static data update frequency
- **Backward Compatibility** - All existing configuration settings preserved

### 🔧 Code Quality Improvements
- **Maintainability** - Split 1600+ line monolithic service into focused 150-300 line services
- **Single Responsibility** - Each service has clear, focused purpose
- **Type Safety** - Dedicated data models (PlayerData, SocialData, LibraryData, GameStatsData)
- **Error Isolation** - Issues in one service don't affect others
- **Testability** - Services can be unit tested in isolation

### 🐛 Bug Fixes
- **Fixed**: Slow game state detection causing delays in showing current game status
- **Fixed**: Friends status showing outdated information due to infrequent updates
- **Fixed**: Session tracking accuracy improved with faster update cycles
- **Improved**: API rate limiting compliance with intelligent request distribution

### 📊 Performance Metrics
- **Game State Detection**: 5s (was ~15-60s depending on timing)
- **Friends Status Updates**: 15s (was 30s)  
- **Library Updates**: 60s (optimized from 30s for non-critical data)
- **API Efficiency**: Reduced unnecessary calls while increasing responsiveness

## [1.0.0] - 2025-11-07

### Added - Phase 1: Basic Steam Data
- Steam profile monitoring (player name, status, Steam level)
- Current game tracking (game name, playtime)
- Library statistics (total games owned, total playtime, recent playtime)
- Basic Steam Data container with 10 sensors
- Thread-safe sensor updates with proper error handling
- Configuration management for Steam API key and settings

### Added - Phase 2: Enhanced Gaming Data
- **Recent Gaming Activity (2-week stats)**
  - Games Played (2w) - Count of games played in last 2 weeks
  - Top Recent Game - Most played game in recent period
  - Gaming Sessions (2w) - Number of gaming sessions
- **Session Time Tracking**
  - Current Session - Current gaming session time in minutes
  - Session Started - When current session began (HH:MM format)
  - Avg Session Length - Average session duration
- **Friends & Social Gaming Monitoring**
  - Friends Online - Number of friends currently online
  - Friends Gaming - Number of friends currently in games
  - Popular Game - Most popular game among online friends
- **Achievement Tracking (for current game)**
  - Achievements - Completion percentage for current game
  - Unlocked - Number of achievements unlocked
  - Total - Total achievements available in current game
  - Latest Achievement - Most recently unlocked achievement with date
- **Recent Games Table**
  - Interactive DataTable displaying 5 most recently played games
  - Game Name, 2-week Hours, Total Hours columns
  - Real-time updates every 30 seconds
  - Sorted by recent activity (most played first)
- Enhanced Gaming Data container with 13 sensors + 1 table
- Extended SteamData model with comprehensive Phase 2 properties

### Added - Phase 3: Advanced Features
- **Detailed Game-Specific Statistics**
  - Primary Game Stats - Comprehensive statistics for currently played game
  - Secondary Game Stats - Statistics for second most recently played game
  - Tertiary Game Stats - Statistics for third most recently played game
- **Multiple Game Monitoring**
  - Monitored Games - Count of games being tracked for detailed statistics
  - Monitored Total Hours - Combined playtime across all monitored games
  - Avg Game Rating - Average user rating across monitored games
- **Achievement Completion Tracking**
  - Overall Achievement % - Achievement completion across entire Steam library
  - Perfect Games - Number of games with 100% achievement completion
  - Total Achievements - Achievements unlocked across all games
  - Achievement Rank - Estimated achievement completion percentile ranking
- **News and Update Monitoring**
  - Latest Game News - Most recent news headline from monitored games
  - Unread News - Count of unread news items for monitored games
  - Most Active News Game - Game with most recent news activity
- **Game Statistics Table**
  - Interactive DataTable displaying detailed statistics for monitored games
  - Game, Total Hours, Recent Hours, Achievements, Status columns
  - Real-time updates with ▶ indicator for currently playing games
  - Achievement completion percentages and unlock progress
  - Game ratings and last played dates
- Advanced Steam Features container with 12 sensors + 1 table
- Extended SteamData model with Phase 3 properties including MonitoredGameStats and SteamNewsItem classes

### Added - Phase 4: Social & Community Features
- **Friends Activity Monitoring**
  - Total Friends - Complete Steam friends count
  - Recently Active - Number of friends active in last 24 hours
  - Friend Activity - Formatted summary of recent friend activity  
  - Most Active Friend - Your most active friend this week
- **Popular Games in Friend Network**
  - Trending Among Friends - Currently trending game in friend network
  - Popular Friend Games - Count of popular games among friends
  - Top Friend Game - Most owned/played game among friends
- **Community Badge Tracking**
  - Badges Earned - Total Steam community badges collected
  - Badge XP - Total experience points from all badges
  - Latest Badge - Most recent badge progress or next badge close to completion
  - Badge Completion - Estimated percentage of available badges earned
- **Global Statistics Comparison**
  - Playtime Percentile - Global ranking compared to all Steam users
  - User Category - Calculated gaming category: "New Player", "Casual Player", "Regular Player", "Dedicated Player", or "Hardcore Gamer"
- **Friends Activity Table**
  - Interactive DataTable displaying friends' current activity and status
  - Friend name with visual status indicators (🟢 Online, 🎮 In-Game, 🟡 Away, etc.)
  - Current online status and game being played
  - Last online timestamps with real-time updates
  - Shows up to 10 most recently active friends sorted by activity
- Social & Community Features container with 13 sensors + 1 table
- Extended SteamData model with Phase 4 properties including SteamFriend, FriendNetworkGame, and SteamBadge classes
- Complete social data collection pipeline with friends activity monitoring
- Global statistics comparison system with user categorization
- Community badge tracking with XP progression analysis

### Added - Advanced Logging & Debugging
- **FileLoggingService** with advanced features:
  - Batched writing for performance optimization
  - Automatic log rotation by date and size
  - Log throttling to prevent spam
  - Thread-safe operation with proper file locking
- **Comprehensive Debug Logging** throughout data collection pipeline:
  - Steam API request/response logging
  - Data collection timing and performance metrics
  - Error tracking with detailed stack traces
  - Table building and update logging
- Debug log file location: `[Plugin Directory]\debug.log`
- Configurable logging levels and retention policies

### Added - SteamID64 Implementation & Validation
- **Explicit SteamID64 Configuration**
  - Primary `SteamId64` property for clear 64-bit format specification
  - Automatic validation of 17-digit format starting with "7656119"
  - Backward compatibility with deprecated `SteamId` property
  - Automatic migration from old `SteamId` to `SteamId64` in configuration files
- **Enhanced Steam API Integration**
  - SteamID64 format validation before all API calls
  - Clear error messages for invalid SteamID64 format
  - Proper Steam Web API compatibility with 64-bit Steam IDs
- **Data Model Updates**
  - Updated `SteamFriend` class to use `SteamId64` property
  - Friends Activity table integration with SteamID64 identifiers
  - Example simulation data with properly formatted SteamID64 placeholders
- **Configuration Security**
  - No hardcoded Steam IDs in source code (only marked examples)
  - Placeholder values in all configuration files: `<your-steam-id64-here>`
  - Example SteamID64s in simulation data clearly marked as examples
- **Comprehensive Validation**
  - `IsValidSteamId64()` method for format verification
  - Enhanced configuration validation with SteamID64 format checking
  - Clear error messaging for configuration issues

### Improved (v1.0.0 Updates) - 2025-11-09

#### User Experience & Clarity Enhancements
- **Enhanced Sensor Descriptions**
  - Improved all sensor names for better clarity and understanding
  - `"Current Game Total Hours"` instead of generic `"Game Playtime"`
  - `"Recent Play Hours (2w)"` instead of ambiguous `"Recent Playtime"`
  - `"Games Played Last 2w"` clarifying the 14-day scope
  - `"Most Played Game (2w)"` specifying recent focus period
  - `"Top Games Being Tracked"` explaining monitored games concept
  - `"All Games Achievement %"` vs game-specific achievements
  - `"Steam Badges Earned"` clarifying community badge context

#### Session Time Format Clarification
- **Session Duration Display Improvements**
  - Fixed session time format confusion (7:21 = 7 hours 21 minutes, not minutes)
  - Updated sensor descriptions: "Current Session Duration", "Avg Session Duration"
  - Maintained proper hr:min formatting logic (441 minutes → "7:21")
  - Converted PluginSensor to PluginText for better time display control
  - Added `FormatMinutesToHourMin()` helper for consistent time formatting

#### Performance Optimizations
- **Faster Refresh Intervals**
  - Reduced update interval from 30 seconds to 15 seconds for friends activity
  - Improved real-time experience for social features and current session tracking
  - Enhanced responsiveness for PlayingFirst friend sorting
  - Better real-time session time updates

#### Friends Activity Enhancements
- **Improved Friend Sorting Logic**
  - Fixed offline friends incorrectly appearing as "playing" in PlayingFirst sorting
  - Enhanced `IsCurrentlyPlaying()` method to check `OnlineStatus != "Offline"`
  - More accurate friend activity detection combining game status and online state
  - Better separation of online vs playing friend counts

#### Comprehensive Documentation Updates
- **README.md Major Enhancement**
  - Added comprehensive "Key Terms Explained" section
  - **Time Periods & Tracking**: Clear definitions of Recent (2w), Total, Session durations
  - **Monitored Games Concept**: Explained auto-selection of top 3-5 active games
  - **Game Categories**: Distinguished Recent vs All games vs Top games tracking
  - **Session Tracking**: Clarified gaming session counting and persistence
  - **Social Features**: Clear distinction between "Friends Online" vs "Friends Gaming"
  - Updated refresh interval documentation to reflect 15-second updates

#### JSON Session Persistence
- **Session Data Preservation**
  - Confirmed sessions.json file survives InfoPanel restarts
  - Session tracking data persists across plugin disposal/restart cycles
  - Only removes sessions older than 14 days (not the entire file)
  - Automatic session recovery on plugin initialization
  - Proper cleanup on disposal without data loss

### Technical Implementation
- **Service-based Architecture**: MonitoringService, SensorManagementService, ConfigurationService, FileLoggingService
- **Dual Container System**: Organized Basic and Enhanced data separation
- **Table Integration**: Proper InfoPanel DataTable implementation with PluginText formatting
- **Real-time Updates**: Event-driven sensor and table updates every 15 seconds (improved from 30s)
- **Steam Web API Integration**: Complete implementation with rate limiting and error handling
- **Thread-safe Operations**: Comprehensive locking for sensor updates and data collection
- **Professional Build System**: Versioned output with clean distribution packaging
- **Enhanced Data Models**: Complete SteamData model with all 13 Enhanced Gaming properties

### Configuration & Privacy
- Steam API key configuration with validation
- Steam ID (64-bit) configuration support
- Configurable update intervals and monitoring settings
- Multiple debug logging levels (Error, Warning, Info, Debug)
- Steam profile privacy awareness and graceful handling
- Proper Steam Web API rate limiting (1 request/second)

### Total Features Delivered
- **48 sensors** across 4 containers (10 basic + 13 enhanced + 12 advanced + 13 social)
- **3 interactive tables** with real-time gaming activity, detailed game statistics, and friends activity monitoring
- **4 containers**: "Basic Steam Data", "Enhanced Gaming Data", "Advanced Steam Features", and "Social & Community Features"
- **Advanced logging system** with rotation and performance optimization
- **Complete Steam integration** with real-time data collection, news monitoring, and social features
- **Multi-game monitoring** with detailed statistics, achievement tracking, and ratings
- **Social & community tracking** with friends activity, badge progression, and global statistics comparison
- **Comprehensive documentation** with detailed sensor explanations and all Phase 1-4 features
- **Professional debugging tools** for troubleshooting and monitoring across all features

### Documentation & User Experience
- **Comprehensive README** with detailed sensor descriptions and real-world examples for all 48 sensors
- **Understanding Your Data** section explaining gaming patterns, social insights, and community engagement
- **Technical Overview** documenting quad-container architecture and data collection across all phases
- **Privacy & Data Access** guidance for Steam profile configuration including social features
- **Troubleshooting** section with container-specific debugging information for all tables
- **User-friendly explanations** of all 48 sensors and 3 table functionalities
- **Phase 4 Social & Community Features** documentation with friends monitoring and global comparison features

---

## Development Notes

### Architecture Highlights
- Clean service separation with dedicated responsibilities
- Event-driven updates ensuring real-time data freshness
- Comprehensive error handling with graceful degradation
- Professional logging for production monitoring and debugging

### Steam API Integration
- Full Steam Web API implementation with proper authentication
- Recent games data collection for table population
- Friends list monitoring and popular games tracking
- Achievement progress tracking for active games
- Session time calculation and monitoring

### InfoPanel Integration
- Proper plugin lifecycle management (Initialize/Dispose)
- Standard container and sensor registration patterns
- DataTable implementation following InfoPanel table standards
- Thread-safe sensor updates with proper locking
- Configuration file management with automatic creation