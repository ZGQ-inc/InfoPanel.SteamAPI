# Multi-Timer Architecture Implementation Documentation

## Overview
This document outlines the complete implementation of the new multi-timer architecture for the InfoPanel SteamAPI plugin, designed to fix session tracking issues and eliminate timer race conditions.

## Problem Analysis

### Original Issues
1. **Session Tracking Failure**: `sessions.json` remained empty despite game activity
2. **Image URL Sensors Showing "-"**: Profile and game banner URLs not displaying correctly  
3. **Data Switching**: Sensors switching between correct and incorrect values intermittently

### Root Cause Identified
- **Timer Race Conditions**: Three competing timers (_fastTimer=5s, _mediumTimer=15s, _slowTimer=60s)
- **Data Merging Conflicts**: `ConvertSocialDataToSteamData()` method setting `CurrentGameName=null` every 15 seconds
- **Session State Destruction**: Game data from Player timer (5s) being overwritten by Social timer (15s)

## Solution: Clean Multi-Timer Architecture

### Architecture Overview
```
MonitoringService (Clean Multi-Timer)
├── PlayerTimer (1s) → PlayerDataService → UpdatePlayerSensors + SessionTracking
├── SocialTimer (15s) → SocialDataService → UpdateSocialSensors  
├── LibraryTimer (45s) → LibraryDataService → UpdateLibrarySensors
└── API Semaphore → Rate limiting (SemaphoreSlim(1,1))
```

### Key Design Principles
1. **NO DATA MERGING**: Each timer owns specific data types exclusively
2. **Direct Sensor Updates**: Clean data flow from Steam API to sensors
3. **API Rate Limiting**: Single semaphore ensures one API call at a time
4. **Immediate Session Tracking**: Real-time game detection and session logging

## Implementation Details

### 1. Timer Configuration
- **PlayerTimer**: 1 second intervals for game state, sessions, profile data
- **SocialTimer**: 15 second intervals for friends status  
- **LibraryTimer**: 45 second intervals for games owned, achievements
- **Total API Calls**: ~65/minute (well under Steam's 200/minute limit)

### 2. Core Components Modified

#### MonitoringService.cs - Complete Rewrite
**Fields Added:**
```csharp
// Multi-timer architecture
private readonly System.Threading.Timer _playerTimer;   // 1 second
private readonly System.Threading.Timer _socialTimer;   // 15 seconds  
private readonly System.Threading.Timer _libraryTimer;  // 45 seconds

// API rate limiting
private readonly SemaphoreSlim _apiSemaphore = new(1, 1);

// Cycle tracking
private volatile int _playerCycleCount = 0;
private volatile int _socialCycleCount = 0;  
private volatile int _libraryCycleCount = 0;
```

**Timer Callbacks Implemented:**
```csharp
private void OnPlayerTimerElapsed(object? state)  // Every 1s
private void OnSocialTimerElapsed(object? state)  // Every 15s  
private void OnLibraryTimerElapsed(object? state) // Every 45s
```

**Direct Sensor Update Methods:**
```csharp
private void UpdatePlayerSensors(PlayerData playerData)
private void UpdateSocialSensors(SocialData socialData)
private void UpdateLibrarySensors(LibraryData? libraryData)
```

### 3. Session Tracking Flow
```
OnPlayerTimerElapsed() 
  → CollectPlayerData() 
  → UpdateSessionTracking() (IMMEDIATE)
  → UpdatePlayerSensors()
  → DataUpdated event
```

### 4. Initial Data Population
Enhanced `CollectInitialDataAsync()` with:
- Sequential data collection (Player → Social → Library)
- API semaphore protection for each step
- 1.1 second delays between API calls
- Immediate sensor population on startup

### 5. Image URL Handling
**ProfileImageUrl Logic:**
```csharp
ProfileImageUrl = !string.IsNullOrEmpty(playerData.ProfileImageUrl) 
    ? playerData.ProfileImageUrl 
    : "-"
```

**CurrentGameBannerUrl Logic:**
```csharp
CurrentGameBannerUrl = !string.IsNullOrEmpty(playerData.CurrentGameBannerUrl)
    ? playerData.CurrentGameBannerUrl
    : "-"
```

## Code Changes Summary

### Files Modified
1. **MonitoringService.cs** - Complete architecture rewrite
2. **MonitoringConstants.cs** - Cleaned up unused constants

### Files Removed/Cleaned
- Removed unused `MILLISECONDS_PER_SECOND` constant
- Eliminated all references to old timer fields (_fastTimer, _mediumTimer, _slowTimer)
- Removed legacy data merging methods (ConvertSocialDataToSteamData, CombinedSteamData)

### Legacy Code Cleanup
- ✅ No legacy timer references remain
- ✅ No data merging logic remains  
- ✅ Clean multi-timer architecture implemented
- ✅ API rate limiting implemented throughout

## Implementation Phases Completed

### Phase 1: Analysis ✅
- Identified timer race conditions as root cause
- Confirmed data merging was destroying game state
- Traced session tracking failure to 15-second overwrites

### Phase 2: Architecture Design ✅  
- Designed clean multi-timer separation
- Planned API rate limiting strategy
- Defined direct sensor update pattern

### Phase 3: Core Implementation ✅
- Replaced legacy timer system
- Implemented SemaphoreSlim rate limiting
- Created direct sensor update methods
- Added immediate session tracking

### Phase 4: Enhancement ✅
- Enhanced initial data population
- Implemented proper image URL handling  
- Added comprehensive error handling
- Cleaned up all legacy code

### Phase 5: Validation (Next)
- Test multi-timer intervals and coordination
- Verify session tracking functionality
- Validate image URL sensor updates
- End-to-end testing with real Steam games

## Expected Benefits

### Immediate Improvements
- **Session Tracking**: Real-time game detection within 1 second
- **Image URLs**: Proper Steam avatar and game banner display
- **Data Stability**: No more intermittent sensor value switching

### Performance Benefits  
- **Rate Limit Compliance**: 65 calls/minute vs Steam's 200 limit
- **Reduced Conflicts**: No competing timer interference
- **Faster Response**: 1-second game detection (down from 5 seconds)

### Maintainability Benefits
- **Clean Architecture**: Separation of concerns by data type
- **Easy Debugging**: Clear timer ownership and responsibilities  
- **Extensible Design**: Simple to add new data types or timers

## Testing Strategy

### Unit Testing
1. Verify timer intervals (1s/15s/45s)
2. Confirm API semaphore prevents concurrent calls
3. Test sensor update methods with mock data

### Integration Testing  
1. Test initial data population sequence
2. Verify session tracking with game state changes
3. Validate image URL handling with real Steam data

### End-to-End Testing
1. Deploy plugin without active game → verify sensor population
2. Start Steam game → verify immediate session creation  
3. Play for 3+ minutes → verify session duration tracking
4. Stop game → verify session end and JSON storage

## Configuration

### Timer Intervals (Configurable via Steam Settings)
- Player monitoring: 1 second (critical for session tracking)
- Social monitoring: 15 seconds (adequate for friends status)
- Library monitoring: 45 seconds (sufficient for owned games)

### API Rate Limits
- Single semaphore ensures sequential API calls
- 1.1 second minimum delay between calls
- Conservative usage well below Steam limits

## Troubleshooting

### Common Issues and Solutions

**Sessions still not tracking:**
- Verify Steam API key and SteamID64 configuration
- Check console logs for PlayerTimer errors
- Confirm `sessions.json` file permissions

**Image URLs still showing "-":**
- Verify Steam profile is public
- Check Steam API responses for image URL data
- Confirm no network connectivity issues

**High API usage warnings:**
- Verify semaphore is preventing concurrent calls
- Check timer intervals are not too aggressive
- Review console logs for excessive API calls

## Monitoring and Debugging

### Console Log Patterns
```
[MonitoringService] Multi-timer architecture initialized - Player:1s, Social:15s, Library:45s
[MonitoringService] Collecting initial data sequentially with API rate limiting...
[MonitoringService] Initial player data: PlayerName, Game: 'GameName'
[MonitoringService] Player sensors updated - Game: 'GameName', Profile URL: true, Banner URL: true
```

### Session Tracking Verification
Check `sessions.json` for entries like:
```json
{
  "sessionId": "unique-id",
  "gameAppId": 123456,
  "gameName": "Game Name", 
  "startTime": "2025-11-10T23:15:00",
  "endTime": null,
  "isActive": true
}
```

## Future Enhancements

### Potential Improvements
1. **Configurable Timer Intervals**: Allow user customization via settings
2. **Adaptive Rate Limiting**: Adjust timing based on Steam API response times
3. **Enhanced Error Recovery**: Automatic retry logic for failed API calls
4. **Performance Metrics**: Built-in monitoring of timer performance

### Extension Points
- Additional data services can be added easily
- New timer intervals can be configured
- Sensor update patterns can be extended
- Event system supports multiple listeners

---

**Implementation Date**: November 10, 2025  
**Version**: 1.1.0  
**Status**: Core implementation complete, testing phase ready  
**Next Phase**: Multi-timer architecture testing and validation