using System;
using System.Collections.Generic;

namespace InfoPanel.SteamAPI.Models
{
    /// <summary>
    /// Data model for Steam API monitoring data
    /// Contains Steam profile information, game statistics, and playtime data
    /// </summary>
    public class SteamData
    {
        #region Core Properties
        
        /// <summary>
        /// Total playtime in hours for currently displayed game
        /// </summary>
        public double TotalPlaytimeHours { get; set; }
        
        /// <summary>
        /// Total number of games owned
        /// </summary>
        public double TotalGamesOwned { get; set; }
        
        /// <summary>
        /// Current status of the Steam profile
        /// </summary>
        public string? Status { get; set; }
        
        /// <summary>
        /// Detailed information about current activity
        /// </summary>
        public string? Details { get; set; }
        
        /// <summary>
        /// Timestamp when this data was collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Indicates if there's an error in data collection
        /// </summary>
        public bool HasError { get; set; }
        
        /// <summary>
        /// Error message if HasError is true
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        #endregion

        #region Steam Profile Properties
        
        /// <summary>
        /// Steam username/display name
        /// </summary>
        public string? PlayerName { get; set; }
        
        /// <summary>
        /// Steam profile URL
        /// </summary>
        public string? ProfileUrl { get; set; }
        
        /// <summary>
        /// Steam level (XP-based progression)
        /// </summary>
        public int SteamLevel { get; set; }
        
        /// <summary>
        /// Avatar URL (small version)
        /// </summary>
        public string? AvatarUrl { get; set; }
        
        /// <summary>
        /// Online status (Online, Offline, Away, Busy, Snooze, Looking to trade, Looking to play)
        /// </summary>
        public string? OnlineState { get; set; }
        
        /// <summary>
        /// Last logoff timestamp (Unix timestamp)
        /// </summary>
        public long LastLogOff { get; set; }
        
        #endregion

        #region Current Game Properties
        
        /// <summary>
        /// Name of currently playing game
        /// </summary>
        public string? CurrentGameName { get; set; }
        
        /// <summary>
        /// App ID of currently playing game
        /// </summary>
        public int CurrentGameAppId { get; set; }
        
        /// <summary>
        /// Extra info about current game session
        /// </summary>
        public string? CurrentGameExtraInfo { get; set; }
        
        /// <summary>
        /// Server IP for multiplayer games
        /// </summary>
        public string? CurrentGameServerIp { get; set; }
        
        #endregion

        #region Library Statistics
        
        /// <summary>
        /// Total playtime across all games in hours
        /// </summary>
        public double TotalLibraryPlaytimeHours { get; set; }
        
        /// <summary>
        /// Most played game name
        /// </summary>
        public string? MostPlayedGameName { get; set; }
        
        /// <summary>
        /// Playtime for most played game in hours
        /// </summary>
        public double MostPlayedGameHours { get; set; }
        
        /// <summary>
        /// Recent playtime (last 2 weeks) in hours
        /// </summary>
        public double RecentPlaytimeHours { get; set; }
        
        /// <summary>
        /// Number of games played recently (last 2 weeks)
        /// </summary>
        public int RecentGamesCount { get; set; }
        
        #endregion

        #region Achievement Statistics
        
        /// <summary>
        /// Total achievements unlocked across all games
        /// </summary>
        public int TotalAchievements { get; set; }
        
        /// <summary>
        /// Perfect games (100% achievements)
        /// </summary>
        public int PerfectGames { get; set; }
        
        /// <summary>
        /// Average game completion percentage
        /// </summary>
        public double AverageGameCompletion { get; set; }
        
        #endregion

        #region Enhanced Gaming Metrics
        
        // Recent Gaming Activity (2-week stats)
        /// <summary>
        /// Most played game in recent period
        /// </summary>
        public string? MostPlayedRecentGame { get; set; }
        
        /// <summary>
        /// Number of gaming sessions in last 2 weeks
        /// </summary>
        public int RecentGameSessions { get; set; }
        
        /// <summary>
        /// List of recently played games with details
        /// </summary>
        public List<SteamGame>? RecentGames { get; set; }
        
        // Session Time Tracking
        /// <summary>
        /// Current session time in minutes
        /// </summary>
        public int CurrentSessionTimeMinutes { get; set; }
        
        /// <summary>
        /// When the current gaming session started
        /// </summary>
        public DateTime? SessionStartTime { get; set; }
        
        /// <summary>
        /// Average session length in minutes
        /// </summary>
        public double AverageSessionTimeMinutes { get; set; }
        
        // Friends Online Monitoring
        /// <summary>
        /// Number of friends currently online
        /// </summary>
        public int FriendsOnline { get; set; }
        
        /// <summary>
        /// Number of friends currently in a game
        /// </summary>
        public int FriendsInGame { get; set; }
        
        /// <summary>
        /// Most popular game among online friends
        /// </summary>
        public string? FriendsPopularGame { get; set; }
        
        // Achievement Tracking (for current game)
        /// <summary>
        /// Achievement completion percentage for current game
        /// </summary>
        public double CurrentGameAchievementPercentage { get; set; }
        
        /// <summary>
        /// Number of achievements unlocked in current game
        /// </summary>
        public int CurrentGameAchievementsUnlocked { get; set; }
        
        /// <summary>
        /// Total achievements available in current game
        /// </summary>
        public int CurrentGameAchievementsTotal { get; set; }
        
        /// <summary>
        /// Name of the most recently unlocked achievement
        /// </summary>
        public string? LatestAchievementName { get; set; }
        
        /// <summary>
        /// Date when the latest achievement was unlocked
        /// </summary>
        public DateTime? LatestAchievementDate { get; set; }
        
        #endregion

        #region Advanced Features
        
        // Detailed Game-Specific Statistics
        /// <summary>
        /// List of monitored games with detailed statistics
        /// </summary>
        public List<MonitoredGameStats>? MonitoredGamesStats { get; set; }
        
        /// <summary>
        /// Primary monitored game's detailed statistics (current game)
        /// </summary>
        public string? PrimaryGameStats { get; set; }
        
        /// <summary>
        /// Secondary monitored game's detailed statistics
        /// </summary>
        public string? SecondaryGameStats { get; set; }
        
        /// <summary>
        /// Tertiary monitored game's detailed statistics
        /// </summary>
        public string? TertiaryGameStats { get; set; }
        
        // Multiple Game Monitoring
        /// <summary>
        /// Number of games currently being monitored
        /// </summary>
        public int MonitoredGamesCount { get; set; }
        
        /// <summary>
        /// Total playtime across all monitored games
        /// </summary>
        public double MonitoredGamesTotalHours { get; set; }
        
        // Achievement Completion Tracking
        /// <summary>
        /// Overall achievement completion percentage across all games
        /// </summary>
        public double OverallAchievementCompletion { get; set; }
        
        /// <summary>
        /// Number of games with 100% achievement completion
        /// </summary>
        public int PerfectGamesCount { get; set; }
        
        /// <summary>
        /// Total achievements unlocked across all games
        /// </summary>
        public int TotalAchievementsUnlocked { get; set; }
        
        /// <summary>
        /// Total achievements available across all owned games
        /// </summary>
        public int TotalAchievementsAvailable { get; set; }
        
        /// <summary>
        /// Achievement completion rank (estimated percentile)
        /// </summary>
        public double AchievementCompletionRank { get; set; }
        
        // News and Update Monitoring
        /// <summary>
        /// Latest Steam news headline for monitored games
        /// </summary>
        public string? LatestGameNews { get; set; }
        
        /// <summary>
        /// Date of the latest news item
        /// </summary>
        public DateTime? LatestNewsDate { get; set; }
        
        /// <summary>
        /// Number of unread news items for monitored games
        /// </summary>
        public int UnreadNewsCount { get; set; }
        
        /// <summary>
        /// List of recent news items for monitored games
        /// </summary>
        public List<SteamNewsItem>? RecentNews { get; set; }
        
        /// <summary>
        /// Game with the most recent news update
        /// </summary>
        public string? MostActiveNewsGame { get; set; }
        
        #endregion

        #region Social & Community Features
        
        // Friends Activity Monitoring
        /// <summary>
        /// List of Steam friends with their current activity
        /// </summary>
        public List<SteamFriend>? FriendsList { get; set; }
        
        /// <summary>
        /// Total number of Steam friends
        /// </summary>
        public int TotalFriendsCount { get; set; }
        
        /// <summary>
        /// Number of friends who have been active in the last 24 hours
        /// </summary>
        public int RecentlyActiveFriends { get; set; }
        
        /// <summary>
        /// Most active friend this week
        /// </summary>
        public string? MostActiveFriend { get; set; }
        
        // Popular Games in Friend Network
        /// <summary>
        /// Most trending game in friend network (biggest increase in players)
        /// </summary>
        public string? TrendingFriendGame { get; set; }
        
        /// <summary>
        /// Game with highest ownership among your friends
        /// </summary>
        public string? MostOwnedFriendGame { get; set; }
        
        // Community Badge Tracking
        /// <summary>
        /// List of Steam badges and their progress
        /// </summary>
        public List<SteamBadge>? SteamBadges { get; set; }
        
        /// <summary>
        /// Total number of badges earned
        /// </summary>
        public int TotalBadgesEarned { get; set; }
        
        /// <summary>
        /// Current badge XP total
        /// </summary>
        public int TotalBadgeXP { get; set; }
        
        /// <summary>
        /// Next badge close to completion
        /// </summary>
        public string? NextBadgeProgress { get; set; }
        
        /// <summary>
        /// Rarest badge owned (by community percentage)
        /// </summary>
        public string? RarestBadge { get; set; }
        
        // Global Statistics Comparison
        /// <summary>
        /// Your percentile ranking in total playtime compared to Steam users
        /// </summary>
        public double GlobalPlaytimePercentile { get; set; }
        
        /// <summary>
        /// Your percentile ranking in number of games owned
        /// </summary>
        public double GlobalGamesOwnedPercentile { get; set; }
        
        /// <summary>
        /// Your percentile ranking in Steam level
        /// </summary>
        public double GlobalSteamLevelPercentile { get; set; }
        
        /// <summary>
        /// Your percentile ranking in achievement completion
        /// </summary>
        public double GlobalAchievementPercentile { get; set; }
        
        /// <summary>
        /// Estimated Steam user category (Casual, Regular, Hardcore, Elite)
        /// </summary>
        public string? GlobalUserCategory { get; set; }
        
        #endregion

        #region Constructors
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public SteamData()
        {
        }
        
        /// <summary>
        /// Constructor with basic data
        /// </summary>
        public SteamData(double totalPlaytimeHours, double totalGamesOwned, string? status = null)
        {
            TotalPlaytimeHours = totalPlaytimeHours;
            TotalGamesOwned = totalGamesOwned;
            Status = status;
        }
        
        /// <summary>
        /// Constructor for error states
        /// </summary>
        public SteamData(string errorMessage)
        {
            HasError = true;
            ErrorMessage = errorMessage;
            Status = "Error";
            Details = errorMessage;
        }
        
        #endregion

        #region Validation
        
        /// <summary>
        /// Validates that the data is in a consistent state
        /// </summary>
        public bool IsValid()
        {
            try
            {
                // Basic validation rules
                if (HasError && string.IsNullOrWhiteSpace(ErrorMessage))
                    return false;
                
                if (!HasError && (double.IsNaN(TotalPlaytimeHours) || double.IsInfinity(TotalPlaytimeHours)))
                    return false;
                
                if (!HasError && (double.IsNaN(TotalGamesOwned) || double.IsInfinity(TotalGamesOwned)))
                    return false;
                
                // Steam-specific validation
                if (TotalPlaytimeHours < 0) return false;
                if (TotalGamesOwned < 0) return false;
                if (SteamLevel < 0) return false;
                if (CurrentGameAppId < 0) return false;
                if (RecentPlaytimeHours < 0) return false;
                if (TotalAchievements < 0) return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion

        #region Data Formatting
        
        /// <summary>
        /// Returns a formatted string representation of total playtime
        /// </summary>
        public string GetFormattedPlaytime(int decimalPlaces = 1, string unit = "hrs")
        {
            if (HasError) return "Error";
            
            var formatted = Math.Round(TotalPlaytimeHours, decimalPlaces).ToString($"F{decimalPlaces}");
            return string.IsNullOrEmpty(unit) ? formatted : $"{formatted} {unit}";
        }
        
        /// <summary>
        /// Returns a formatted string representation of games owned
        /// </summary>
        public string GetFormattedGamesOwned()
        {
            if (HasError) return "Error";
            
            return TotalGamesOwned.ToString("F0");
        }
        
        /// <summary>
        /// Returns a formatted timestamp string
        /// </summary>
        public string GetFormattedTimestamp(string format = "HH:mm:ss")
        {
            return Timestamp.ToString(format);
        }
        
        /// <summary>
        /// Returns a formatted string for last logoff time
        /// </summary>
        public string GetFormattedLastLogOff()
        {
            if (LastLogOff == 0) return "Unknown";
            
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(LastLogOff).DateTime;
            var timeSpan = DateTime.Now - dateTime;
            
            if (timeSpan.TotalMinutes < 1) return "Just now";
            if (timeSpan.TotalHours < 1) return $"{(int)timeSpan.TotalMinutes} minutes ago";
            if (timeSpan.TotalDays < 1) return $"{(int)timeSpan.TotalHours} hours ago";
            if (timeSpan.TotalDays < 30) return $"{(int)timeSpan.TotalDays} days ago";
            
            return dateTime.ToString("MMM dd, yyyy");
        }
        
        #endregion

        #region Steam-Specific Methods
        
        /// <summary>
        /// Calculates average playtime per game
        /// </summary>
        public double CalculateAveragePlaytimePerGame()
        {
            if (TotalGamesOwned <= 0) return 0;
            return TotalLibraryPlaytimeHours / TotalGamesOwned;
        }
        
        /// <summary>
        /// Determines if the player is currently online
        /// </summary>
        public bool IsOnline()
        {
            return OnlineState == "Online" || !string.IsNullOrEmpty(CurrentGameName);
        }
        
        /// <summary>
        /// Determines if the player is currently in a game
        /// </summary>
        public bool IsInGame()
        {
            return !string.IsNullOrEmpty(CurrentGameName) && CurrentGameAppId > 0;
        }
        
        /// <summary>
        /// Gets a simplified status for display
        /// </summary>
        public string GetDisplayStatus()
        {
            if (HasError) return "Error";
            if (IsInGame()) return $"Playing {CurrentGameName}";
            if (IsOnline()) return OnlineState ?? "Online";
            return "Offline";
        }
        
        /// <summary>
        /// Gets a health status color based on data availability
        /// </summary>
        public string GetHealthStatusColor()
        {
            if (HasError) return "Red";
            if (string.IsNullOrEmpty(PlayerName)) return "Orange";
            if (IsOnline()) return "Green";
            return "Yellow";
        }
        
        /// <summary>
        /// Calculates gaming activity level based on recent playtime
        /// </summary>
        public string GetActivityLevel()
        {
            if (HasError) return "Unknown";
            if (RecentPlaytimeHours > 20) return "Very Active";
            if (RecentPlaytimeHours > 10) return "Active";
            if (RecentPlaytimeHours > 2) return "Casual";
            if (RecentPlaytimeHours > 0) return "Light";
            return "Inactive";
        }
        
        #endregion

        #region Equality and Comparison
        
        /// <summary>
        /// Determines if this data is significantly different from another instance
        /// </summary>
        public bool HasSignificantChange(SteamData? other, double threshold = 0.1)
        {
            if (other == null) return true;
            if (HasError != other.HasError) return true;
            if (HasError && other.HasError) return ErrorMessage != other.ErrorMessage;
            
            var playtimeDiff = Math.Abs(TotalPlaytimeHours - other.TotalPlaytimeHours);
            var gamesDiff = Math.Abs(TotalGamesOwned - other.TotalGamesOwned);
            
            return playtimeDiff > threshold || 
                   gamesDiff > threshold || 
                   Status != other.Status ||
                   CurrentGameName != other.CurrentGameName ||
                   OnlineState != other.OnlineState;
        }
        
        #endregion

        #region String Representation
        
        /// <summary>
        /// Returns a string representation of the data
        /// </summary>
        public override string ToString()
        {
            if (HasError)
            {
                return $"SteamData[Error: {ErrorMessage}]";
            }
            
            var currentGame = IsInGame() ? $", Playing: {CurrentGameName}" : "";
            return $"SteamData[{PlayerName}, Games: {TotalGamesOwned:F0}, Playtime: {TotalPlaytimeHours:F1}hrs{currentGame}, Status: {OnlineState}, Time: {Timestamp:HH:mm:ss}]";
        }
        
        #endregion
    }

    /// <summary>
    /// Represents detailed statistics for a monitored game
    /// </summary>
    public class MonitoredGameStats
    {
        /// <summary>
        /// Steam App ID of the game
        /// </summary>
        public uint AppId { get; set; }
        
        /// <summary>
        /// Name of the game
        /// </summary>
        public string? GameName { get; set; }
        
        /// <summary>
        /// Total playtime in hours
        /// </summary>
        public double TotalHours { get; set; }
        
        /// <summary>
        /// Playtime in the last 2 weeks in hours
        /// </summary>
        public double RecentHours { get; set; }
        
        /// <summary>
        /// Achievement completion percentage
        /// </summary>
        public double AchievementCompletion { get; set; }
        
        /// <summary>
        /// Number of achievements unlocked
        /// </summary>
        public int AchievementsUnlocked { get; set; }
        
        /// <summary>
        /// Total number of achievements available
        /// </summary>
        public int AchievementsTotal { get; set; }
        
        /// <summary>
        /// Game-specific statistics (JSON string or formatted text)
        /// </summary>
        public string? GameSpecificStats { get; set; }
        
        /// <summary>
        /// Last time this game was played
        /// </summary>
        public DateTime? LastPlayed { get; set; }
        
        /// <summary>
        
        /// <summary>
        /// Whether this game is currently being played
        /// </summary>
        public bool IsCurrentlyPlaying { get; set; }
    }

    /// <summary>
    /// Represents a Steam news item for a game
    /// </summary>
    public class SteamNewsItem
    {
        /// <summary>
        /// Steam App ID of the game this news is for
        /// </summary>
        public uint AppId { get; set; }
        
        /// <summary>
        /// Name of the game
        /// </summary>
        public string? GameName { get; set; }
        
        /// <summary>
        /// News item title/headline
        /// </summary>
        public string? Title { get; set; }
        
        /// <summary>
        /// Brief content excerpt
        /// </summary>
        public string? Content { get; set; }
        
        /// <summary>
        /// Date when the news was published
        /// </summary>
        public DateTime PublishDate { get; set; }
        
        /// <summary>
        /// URL to the full news article
        /// </summary>
        public string? Url { get; set; }
        
        /// <summary>
        /// Author of the news item
        /// </summary>
        public string? Author { get; set; }
        
        /// <summary>
        /// Whether this news item has been read by the user
        /// </summary>
        public bool IsRead { get; set; }
        
        /// <summary>
        /// Type of news (update, announcement, event, etc.)
        /// </summary>
        public string? NewsType { get; set; }
    }
}