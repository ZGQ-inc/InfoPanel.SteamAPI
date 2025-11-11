# InfoPanel.SteamAPI

**Version:** 1.2.0  
**Author:** F3NN3X  
**Website:** https://myurl.com

## Description

Comprehensive Steam API integration for InfoPanel, providing **real-time** Steam profile and gaming activity monitoring with **reliable multi-timer architecture**. This plugin now features **1-second game state detection** for instant responsiveness and **guaranteed session tracking** that actually works.

## 🚀 **Major Update (v1.2.0) - CRITICAL SESSION TRACKING FIX**

### **🔥 BREAKING: Complete Architecture Rewrite**
**Session tracking was completely broken** - `sessions.json` stayed empty due to fatal timer race conditions. We've completely rewritten the monitoring architecture:

### **NEW Multi-Timer Architecture - NO DATA MERGING**
- **⚡ Player Timer (1s)**: Game detection, profile data, **immediate session tracking**  
- **� Social Timer (15s)**: Friends status only - **cannot interfere with game data**
- **� Library Timer (45s)**: Library statistics only - **cannot interfere with game data**

### **Critical Fixes**
- ✅ **Session tracking works reliably** - `sessions.json` now populates immediately
- ✅ **Image URL sensors populate correctly** - no more "-" fallbacks due to data corruption
- ✅ **Eliminated data switching** - no more correct/incorrect state oscillations every 15 seconds
- ✅ **1-second game detection** - immediate response when starting/stopping games

### **Technical Improvements**
- **API Rate Limiting**: `SemaphoreSlim(1,1)` prevents concurrent Steam API calls
- **Direct Sensor Updates**: Clean data flow with no timer interference
- **Timer Verification**: Built-in interval checking with deviation logging
- **Staggered Startup**: 0s/2s/5s timer offsets distribute API load

## 🏗️ Architecture

### **Service-Oriented Design**
- **PlayerDataService**: Real-time player status and game state **(1s updates)**
- **SocialDataService**: Friends and community features **(15s updates)** 
- **LibraryDataService**: Game library and playtime statistics **(45s updates)**
- **GameStatsService**: Detailed achievements and game analytics **(45s updates)**
- **MonitoringService**: Orchestrates all services with **separated timer responsibilities**
- **SessionTrackingService**: **Reliable session tracking with immediate game state updates**

## Data Collection Overview

This plugin provides **48 sensors and 3 tables** organized into **4 containers** for comprehensive Steam monitoring:

### Basic Steam Data Container (10 sensors)

**Core Profile Information:**
- **Player Name** - Your Steam display name (e.g., "F3NN3X")
- **Online Status** - Current Steam status: "Online", "Playing [Game]", "Away", "Busy", "Snooze", "Looking to trade", "Looking to play"
- **Steam Level** - Your Steam account level based on XP earned (e.g., Level 28)

**Current Gaming Session:**
- **Current Game** - Name of currently running game or "Not Playing" when offline/idle
- **Game Playtime** - Total hours played in the current game (e.g., "186.2h" for No Man's Sky)

**Steam Library Overview:**
- **Games Owned** - Total number of games in your Steam library (e.g., 320 games)
- **Total Playtime** - Lifetime gaming hours across your entire Steam library (e.g., "4292.6h")
- **Recent Playtime** - Gaming hours in the last 2 weeks across all games (e.g., "71.5h")

**Status Information:**
- **Plugin Status** - Current plugin state: "Online", "Playing [Game]", "Offline", "Error"
- **Details** - Comprehensive status line with key stats (e.g., "Level 28 • 320 games • 4293h total • 71.5h recent • Updated: 00:55:44")

### Enhanced Gaming Data Container (13 sensors + 1 table)

**Recent Gaming Activity (2-week statistics):**
- **Games Played (2w)** - Number of different games played in the last 2 weeks (e.g., 5 games)
- **Top Recent Game** - Game with most playtime in recent 2-week period (e.g., "ARC Raiders" with 58.0h)
- **Gaming Sessions (2w)** - Estimated number of separate gaming sessions (calculated: ~10 sessions)

**Current Session Tracking:**
- **Current Session** - Duration of current gaming session in minutes (e.g., 30 minutes when in-game, 0 when not playing)
- **Session Started** - Time when current session began in HH:MM format (e.g., "00:26" or "Not in game")
- **Avg Session Length** - Average session duration across recent gaming activity (e.g., 428.7 minutes = ~7 hours)

**Friends & Social Gaming:**
- **Friends Online** - Number of Steam friends currently online (e.g., 12 friends online)
- **Friends Gaming** - Number of online friends currently playing games (e.g., 3 friends in games)
- **Popular Game** - Most popular game among your online friends (e.g., "Counter-Strike 2")

**Current Game Achievement Progress:**
- **Achievements** - Achievement completion percentage for currently played game (e.g., 48.9% for ARC Raiders)
- **Unlocked** - Number of achievements earned in current game (e.g., 23 achievements unlocked)
- **Total** - Total achievements available in current game (e.g., 47 total achievements)
- **Latest Achievement** - Name of most recently unlocked achievement (e.g., "Explorer" unlocked on 11/06, or "None recent")

**Recent Games Table:**
- **Interactive table** displaying your 5 most recently played games with detailed statistics:
  - **Game Name** - Full game title (e.g., "ARC Raiders", "No Man's Sky", "Battlefield™ 6")
  - **2w Hours** - Hours played in the last 2 weeks (e.g., "58.0h", "6.3h", "4.4h")
  - **Total Hours** - Total lifetime hours in each game (e.g., "58.0h", "186.2h", "89.1h")
  - Games are sorted by recent activity (most played in 2 weeks first)
  - Updates every 30 seconds with real-time data

### Advanced Steam Features Container (12 sensors + 1 table)

**Detailed Game-Specific Statistics:**
- **Primary Game Stats** - Comprehensive statistics for your currently played game (e.g., "ARC Raiders: 58.0h total, 48.9% achievements")
- **Secondary Game Stats** - Statistics for your second most recently played game (e.g., "No Man's Sky: 186.2h total, 6.3h recent")
- **Tertiary Game Stats** - Statistics for your third most recently played game, providing a complete picture of your top gaming activity

**Multiple Game Monitoring:**
- **Monitored Games** - Number of games currently being tracked for detailed statistics (e.g., 3 games actively monitored)
- **Monitored Total Hours** - Combined playtime across all monitored games (e.g., 250.5h across tracked games)
- **Avg Game Rating** - Average user rating across your monitored games (e.g., 8.2★ average rating)

**Achievement Completion Tracking:**
- **Overall Achievement %** - Your achievement completion percentage across your entire Steam library (e.g., 35.2% overall completion)
- **Perfect Games** - Number of games where you've achieved 100% completion (e.g., 12 games with perfect achievement completion)
- **Total Achievements** - Number of achievements you've unlocked across all your games (e.g., 1,247 achievements earned)
- **Achievement Rank** - Your estimated achievement completion percentile compared to other Steam users (e.g., 65th percentile)

**News and Update Monitoring:**
- **Latest Game News** - Most recent news headline from your monitored games (e.g., "ARC Raiders - Latest Update Available")
- **Unread News** - Count of unread news items for your monitored games (e.g., 3 unread news items)
- **Most Active News Game** - Game with the most recent news activity (e.g., "ARC Raiders" for most news updates)

**Game Statistics Table:**
- **Interactive table** displaying detailed statistics for your monitored games:
  - **Game** - Game name with ▶ indicator for currently playing (e.g., "▶ ARC Raiders", "No Man's Sky", "Battlefield™ 6")
  - **Total Hours** - Lifetime playtime in each game (e.g., "58.0h", "186.2h", "89.1h")
  - **Recent Hours** - Recent activity hours for each game (e.g., "45.2h", "6.3h", "2.1h")
  - **Achievements** - Completion percentage and unlock progress (e.g., "49% (23/47)", "72% (156/217)", "15% (8/52)")
  - **Status** - Current status with ratings and last played dates (e.g., "Playing", "Nov 06 (8.5★)", "Nov 04 (7.8★)")
  - Shows up to 5 monitored games with comprehensive per-game statistics
  - Real-time updates reflecting current gaming activity

### Social & Community Features Container (13 sensors + 1 table)

**Friends Activity Monitoring:**
- **Total Friends** - Your complete Steam friends count (e.g., 42 friends total)
- **Recently Active** - Number of friends active in the last 24 hours (e.g., 8 recently active friends)
- **Friend Activity** - Formatted summary of recent friend activity (e.g., "8 friends recently active", "No recent activity")
- **Most Active Friend** - Your most active friend this week (e.g., "PlayerOne" for highest activity)

**Popular Games in Friend Network:**
- **Trending Among Friends** - Currently trending game in your friend network (e.g., "Counter-Strike 2" gaining popularity)
- **Popular Friend Games** - Count of popular games among your friends (e.g., 5 trending games in network)
- **Top Friend Game** - Most owned/played game among your friends (e.g., "Counter-Strike 2" most popular overall)

**Community Badge Tracking:**
- **Badges Earned** - Total Steam community badges you've earned (e.g., 156 badges collected)
- **Badge XP** - Total experience points from all your badges (e.g., 3,240 XP accumulated)
- **Latest Badge** - Your most recent badge progress or next badge close to completion (e.g., "Community Ambassador" in progress)
- **Badge Completion** - Estimated percentage of available badges earned (e.g., 78.5% completion rate)

**Global Statistics Comparison:**
- **Playtime Percentile** - Your global ranking compared to all Steam users (e.g., 85th percentile means you play more than 85% of users)
- **User Category** - Calculated gaming category based on activity: "New Player", "Casual Player", "Regular Player", "Dedicated Player", or "Hardcore Gamer"

**Friends Activity Table:**
- **Interactive table** displaying your friends' current activity and status:
  - **Friend** - Friend name with visual status indicator (🟢 Online, 🎮 In-Game, 🟡 Away, 🔴 Busy, ⚫ Offline)
  - **Status** - Current online status (e.g., "Online", "In-Game", "Away", "Offline")
  - **Playing** - Current game being played or "Not in game" (e.g., "Counter-Strike 2", "Dota 2", "Not in game")
  - **Last Online** - When friend was last active (e.g., "Nov 08 14:30", "Oct 15 09:45")
  - Shows up to 10 most recently active friends with real-time status updates
  - Friends sorted by online status first, then by most recent activity

## Key Terms Explained

### Time Periods & Tracking
- **Recent Playtime (2w)**: Hours played across all games in the last 14 days
- **Total Playtime**: Lifetime hours across your entire Steam library since account creation
- **Current Session Duration**: Time spent in current gaming session (e.g., "2:30" = 2 hours 30 minutes)
- **Average Session Duration**: Average length of your gaming sessions (e.g., "7:21" = 7 hours 21 minutes)

### Monitored Games Concept
- **Monitored Games**: Your top 3-5 most actively played games that are tracked for detailed statistics
- **Selection**: Automatically chosen based on recent playtime and achievement activity
- **Purpose**: Provides in-depth tracking without overwhelming data from entire 300+ game library
- **Configurable**: Maximum monitored games can be adjusted in configuration (default: 5 games)
- **Top Games Being Tracked**: Shows how many games are currently monitored for detailed stats

### Game Categories
- **Games Played Last 2w**: Any game launched in the last 14 days (even 1 minute counts)
- **Recent Games**: Your 5 most recently played games, sorted by recent activity
- **All Games Total Hours**: Lifetime playtime across every game you own
- **Top Games Total Hours**: Combined playtime for just your monitored games

### Session Tracking
- **Gaming Sessions (2w)**: Estimated number of separate gaming periods in last 14 days
- **Session Started At**: Clock time when current gaming session began
- **Session Persistence**: Session data survives InfoPanel restarts and is saved to disk

### Social Features
- **Friends Online**: Steam friends currently online (any status except offline)
- **Friends Gaming**: Friends currently playing games (not just online/idle)
- **Popular Game**: Most commonly played game among your online friends right now

## Understanding Your Data

### Gaming Activity Patterns
The plugin helps you understand your gaming habits through several key metrics:

**Session Analysis:**
- **Average Session Length** shows how long you typically play (e.g., 428 minutes ≈ 7 hours suggests long gaming sessions)
- **Recent Sessions** estimates how many separate gaming periods you've had (useful for tracking gaming frequency)
- **Current Session** tracks your active playtime in real-time

**Game Preference Insights:**
- **Recent vs Total Playtime** comparison reveals current gaming interests vs. long-term favorites
- **Top Recent Game** shows what you're currently focused on playing
- **Recent Games Table** provides a clear overview of your gaming variety and time investment

**Social Gaming Context:**
- **Friends Online/Gaming** gives insight into your gaming community's activity
- **Popular Game** among friends shows trending games in your social circle
- **Friends Activity Table** provides real-time view of what your gaming network is playing
- **Trending Among Friends** reveals games gaining popularity in your social circle
- **Playtime Percentile** shows how your gaming activity compares globally

**Community Engagement:**
- **Badge Progress** tracks your Steam community participation and achievements
- **User Category** classification helps understand your gaming intensity level
- **Global Statistics** provide context for your gaming habits compared to the broader Steam community

### Data Refresh & Accuracy
- All data updates **every 15 seconds** automatically for faster friends activity updates
- **Steam API Integration** ensures data accuracy and real-time updates
- **Session tracking** begins when InfoPanel detects you've started playing a game
- **Achievement progress** updates only for games you're currently playing
- **Recent activity** covers the last 14 days of Steam gaming data

### Privacy & Data Access
- All data comes directly from **your Steam profile** via Steam's official Web API
- **Profile visibility** must be set to "Public" in Steam for most features to work
- **Friends data** requires your friends list to be publicly visible
- No personal information is stored locally - all data is fetched fresh from Steam
- The plugin respects Steam's **API rate limits** (1 request per second) for responsible usage

## Technical Overview

This plugin follows InfoPanel's service-based architecture with comprehensive Steam monitoring:

### Architecture Components
- **Main Plugin Class**: `InfoPanel.SteamAPI.cs` - Entry point with quad container management
- **Service Layer**: Clean separation with dedicated services
  - `MonitoringService`: Real-time Steam API data collection with advanced features and social monitoring
  - `ConfigurationService`: Thread-safe INI configuration management
  - `SensorManagementService`: Centralized sensor updates with data validation across all phases
  - `FileLoggingService`: Advanced logging with batching and rotation
- **Data Models**: Enhanced `SteamData` with comprehensive gaming metrics, advanced features, and social community data
- **Triple Table Integration**: Recent Games, Game Statistics, and Friends Activity tables with real-time updates and proper InfoPanel formatting

### Container Organization
- **Basic Steam Data**: Core profile and gaming statistics (10 sensors)
- **Enhanced Gaming Data**: Advanced metrics with session tracking and social features (13 sensors + 1 table)
- **Advanced Steam Features**: Detailed game statistics, multi-game monitoring, achievement completion tracking, and news monitoring (12 sensors + 1 table)
- **Social & Community Features**: Friends activity, community badges, popular friend games, and global statistics comparison (13 sensors + 1 table)

### Data Collection
- Steam Web API integration with proper rate limiting and social features
- Real-time session monitoring and achievement tracking across multiple games
- Multi-game statistics collection with detailed per-game metrics and friend network analysis
- Steam news monitoring with unread tracking and community updates
- Overall achievement completion analysis across entire game library
- Friends activity monitoring with real-time status and game tracking
- Community badge tracking with XP progression and completion analysis
- Global statistics comparison for playtime and user categorization
- Thread-safe updates every 30 seconds across all monitoring phases
- Comprehensive error handling with fallback states for all data sources
- Debug logging for troubleshooting and monitoring across social and community features

## Documentation

This plugin includes comprehensive InfoPanel plugin development documentation:

- **[InfoPanel Plugin Development Guide](docs/InfoPanel_PluginDocumentation.md)** - Complete guide to InfoPanel plugin development, including:
  - Plugin architecture overview
  - Component descriptions and lifecycle
  - Code examples and best practices
  - Debugging and deployment instructions
  - API reference and data types

## Features

- **Quad Container Architecture**: Organized Basic, Enhanced, Advanced, and Social Steam data containers
- **Triple Interactive Tables**: Recent Games, Game Statistics, and Friends Activity tables with real-time updates
- **Real-time Updates**: All sensors and tables refresh every 30 seconds automatically
- **Comprehensive Gaming Metrics**: 48 sensors covering profile, achievements, friends, session tracking, advanced statistics, and social community features
- **Advanced Game Monitoring**: Detailed statistics for multiple games with achievement tracking and news monitoring
- **Social & Community Integration**: Friends activity monitoring, community badge tracking, and global statistics comparison
- **Service-based Architecture**: Clean separation of concerns with dedicated services for all monitoring phases
- **Event-driven Data Updates**: Thread-safe sensor management with real-time Steam API integration across all features
- **Advanced Logging**: Comprehensive debug logging for troubleshooting and monitoring all data sources
- **INI-based Configuration**: Easy configuration following InfoPanel standards with social features support
- **Steam Privacy Aware**: Respects Steam profile privacy settings and API rate limits for all data sources
- **Session Tracking**: Real-time monitoring of current gaming sessions with time tracking
- **Achievement Integration**: Live achievement progress for currently played games plus overall library completion tracking
- **Social Gaming**: Friends online monitoring, popular games tracking, and friend network analysis
- **Community Features**: Badge progression tracking with XP monitoring and completion analysis
- **Global Comparison**: Percentile ranking system with user categorization based on gaming activity
- **Steam News Integration**: Real-time news monitoring for your monitored games with unread tracking
- **Multi-Game Statistics**: Comprehensive tracking of multiple games with ratings, playtime, and achievement progress
- **Friends Activity Monitoring**: Real-time friend status tracking with current games and activity timestamps
- **Professional Build System**: Automatic versioning with clean distribution packaging

## Installation

1. Build the plugin in Release mode:
   ```powershell
   dotnet build -c Release
   ```

2. The plugin will be built to:
   ```
   bin\Release\net8.0-windows\InfoPanel.SteamAPI-v1.0.0\InfoPanel.SteamAPI\
   ```

3. A distribution ZIP file will also be created:
   ```
   bin\Release\net8.0-windows\InfoPanel.SteamAPI-v1.0.0.zip
   ```

4. Extract the ZIP file to your InfoPanel plugins directory, or copy the plugin folder manually

5. Restart InfoPanel to load the plugin

## Configuration

After first run, the plugin creates a configuration file:
```
InfoPanel.SteamAPI.dll.ini
```

### Required Configuration
To use this plugin, you'll need to configure:

```ini
[Steam Settings]
SteamApiKey=YOUR_STEAM_WEB_API_KEY
SteamId=YOUR_64BIT_STEAM_ID
```

**Getting Your Steam API Key:**
1. Visit https://steamcommunity.com/dev/apikey
2. Sign in with your Steam account
3. Enter a domain name (can be localhost for personal use)
4. Copy the generated API key

**Finding Your Steam ID:**
1. Visit https://steamid.io/
2. Enter your Steam profile URL or username
3. Copy the "steamID64" value

### Optional Configuration
The configuration file also includes:

**Performance Tuning (New in v1.1.0):**
```ini
[Performance Settings]
FastUpdateIntervalSeconds=5      # Real-time data (game state, player status)
MediumUpdateIntervalSeconds=15   # Social data (friends, activity)
SlowUpdateIntervalSeconds=60     # Static data (library, achievements)
```

**Display Customization (New in v1.2.0):**
```ini
[Display Settings]
CurrentlyPlayingText=Currently Playing  # Text shown when actively playing a game
LastPlayedGameText=Last Played Game     # Text shown when viewing last played game
```

**Localization Examples:**
- German: `CurrentlyPlayingText=Spielt gerade`, `LastPlayedGameText=Zuletzt gespielt`
- French: `CurrentlyPlayingText=En train de jouer`, `LastPlayedGameText=Dernier jeu joué`
- Spanish: `CurrentlyPlayingText=Jugando ahora`, `LastPlayedGameText=Último juego jugado`

**Other Settings:**
- **Debug Settings**: Enable comprehensive logging for troubleshooting
- **Monitoring Settings**: Control which Steam features to monitor
- **Display Settings**: Control how information is displayed
- **Privacy Settings**: Control which data to display publicly

Use InfoPanel's "Open Config" button to easily access and edit the configuration file.

## Building from Source

### Prerequisites
- .NET 8.0 SDK or later
- Visual Studio 2022 or JetBrains Rider (optional)
- InfoPanel installed with InfoPanel.Plugins.dll available

### Build Commands

**Debug Build:**
```powershell
dotnet build -c Debug
```

**Release Build:**
```powershell
dotnet build -c Release
```

**Clean Build:**
```powershell
dotnet clean
dotnet build -c Release
```

## Development

### Architecture

The plugin follows a service-based architecture:

```
InfoPanel.SteamAPI.cs    # Main plugin class
├── Services/
│   ├── MonitoringService.cs         # Steam API data collection
│   ├── SensorManagementService.cs   # Thread-safe sensor updates
│   ├── ConfigurationService.cs      # INI configuration management
│   └── FileLoggingService.cs        # Debug logging
├── Models/
│   └── SteamData.cs                 # Steam data structure with social & community features
└── PluginInfo.ini                   # Plugin metadata
```

### Key Components

- **Main Plugin Class**: Manages 4 containers with 48 sensors and 3 tables total
- **Monitoring Service**: Handles Steam Web API calls and data collection for all phases including social features
- **Sensor Management Service**: Thread-safe updates for Basic, Enhanced, Advanced, and Social sensors
- **Configuration Service**: Manages Steam API key, Steam ID, and monitoring settings for all features
- **File Logging Service**: Provides debug logging for Steam API interactions across all monitoring phases

### Steam Web API Integration

The plugin is designed to work with Steam's Web API endpoints:
- **ISteamUser/GetPlayerSummaries** - Basic profile data and online status
- **IPlayerService/GetOwnedGames** - Game library statistics and playtime data
- **IPlayerService/GetRecentlyPlayedGames** - Recent gaming activity tracking
- **ISteamUserStats/GetPlayerAchievements** - Achievement data and completion tracking
- **ISteamUser/GetFriendList** - Friends monitoring and social network analysis
- **ISteamUserStats/GetUserStatsForGame** - Detailed game statistics and metrics
- **IStorefrontService/GetNewsForApp** - Game news and update monitoring
- **ISteamUser/GetPlayerBadges** - Community badge tracking and progression
- **ISteamCommunity** - Global statistics and user comparison data

### Adding New Features

1. **New Sensors**: Define in main class, add to appropriate container, update sensor management service
2. **New Configuration**: Add Steam-related settings to ConfigurationService
3. **New Data Properties**: Extend SteamData model with Steam API response fields
4. **New Steam API Endpoints**: Add endpoint calls to MonitoringService with proper error handling

## Troubleshooting

### Enable Debug Logging

The plugin provides comprehensive debug logging for troubleshooting and monitoring. Edit the configuration file to enable logging:

```ini
[Debug Settings]
EnableDebugLogging=true
```

**When enabled, all debug information is logged including:**
- Major operations and results (Steam API calls, sensor updates)
- Detailed step-by-step operations (data collection phases, timing)
- Complete Steam Web API request/response data for troubleshooting
- Raw JSON responses from Steam API (up to 15KB each)
- API call timing and performance data
- Complete error details for failed requests

**Example Log Output:**
```
[Info] Steam data collection completed successfully - Status: Online, Player: F3NN3X
[Debug] Collecting player summary data...
[Debug] Getting Steam level...
[Error] === STEAM API RESPONSE === GetPlayerSummaryAsync
[Error] {"response":{"players":[{"steamid":"76561198011676644"...}]}}
```

**Log File Location:**
```
C:\ProgramData\InfoPanel\plugins\InfoPanel.SteamAPI\InfoPanel.SteamAPI-debug.log
```

**Log File Features:**
- Automatic rotation to prevent excessive size
- Thread-safe writing for concurrent operations  
- Timestamped entries with service identification
- File-only output (doesn't flood InfoPanel console)

### Common Issues

**Plugin Not Loading:**
- Ensure InfoPanel.Plugins.dll reference is correct
- Verify all dependencies are in the plugin directory
- Check that .NET 8.0 runtime is installed

**No Data Appearing:**
- Verify Steam API key is valid and properly configured
- Check Steam ID (64-bit) is correct in configuration
- Enable debug logging and check for Steam API errors
- Verify Steam profile is set to public (required for most data)
- For Recent Games table: Ensure you have recent gaming activity (last 2 weeks)

**Table Not Showing Games:**
- Check if you've played games in the last 2 weeks (Recent Games table shows only recent activity)
- Verify Steam profile privacy allows "Game details" to be public
- Look for "Built Recent Games table with X games" messages in debug log
- Recent Games table requires at least 1 game played in the last 14 days

**Sensor Data Missing or Incorrect:**
- **Achievement data**: Only shows for currently running games
- **Session tracking**: Only active when you're playing a game
- **Friends data**: Requires public friends list in Steam privacy settings
- **Recent activity**: Requires public "Game details" in Steam privacy settings

**Steam API Errors:**
- Check Steam Web API status at https://steamstat.us/
- Verify API key hasn't exceeded rate limits
- Ensure Steam profile privacy settings allow API access
- Review debug logs for specific API error codes
- Recent Games table logs show "Built Recent Games table with X games" when successful

**Configuration Not Saving:**
- Verify file permissions in plugin directory
- Check INI file syntax (section headers and key=value format)
- Review debug logs for file access errors

## Version History

See [CHANGELOG.md](CHANGELOG.md) for detailed version history.

## License

See [LICENSE](LICENSE) for license information.

## Support

For issues, questions, or contributions:
- Check debug logs first
- Review InfoPanel documentation
- Contact: F3NN3X (https://myurl.com)

## Acknowledgments

Built using InfoPanel Plugin development framework and Steam Web API integration.

## Steam API Data Sources

This plugin utilizes the following Steam Web API endpoints:
- **Player Summaries** - Profile information and online status monitoring
- **Owned Games** - Game library statistics and playtime data collection
- **Recently Played Games** - Recent gaming activity tracking and session analysis
- **Player Achievements** - Achievement progress for current games and overall completion
- **Friend Lists** - Online friends, social gaming data, and network analysis
- **User Stats for Games** - Detailed game-specific statistics and metrics
- **News for Apps** - Game news monitoring and update tracking
- **Player Badges** - Community badge progression and XP tracking
- **Community Features** - Global statistics comparison and user categorization

All Steam data collection respects Steam's privacy settings and API rate limits across all monitoring phases.
