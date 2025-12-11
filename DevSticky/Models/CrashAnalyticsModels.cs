using System;
using System.Collections.Generic;

namespace DevSticky.Models;

/// <summary>
/// Recovery attempt record
/// </summary>
public class RecoveryAttempt
{
    /// <summary>
    /// When the recovery attempt was made
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Type of recovery action attempted
    /// </summary>
    public string RecoveryAction { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the recovery was successful
    /// </summary>
    public bool WasSuccessful { get; set; }
    
    /// <summary>
    /// Component that required recovery
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Issue that triggered the recovery
    /// </summary>
    public string TriggeringIssue { get; set; } = string.Empty;
    
    /// <summary>
    /// How long the recovery took
    /// </summary>
    public TimeSpan? Duration { get; set; }
    
    /// <summary>
    /// Error message if recovery failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Additional context about the recovery
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Safe mode usage record
/// </summary>
public class SafeModeUsage
{
    /// <summary>
    /// When safe mode was started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When safe mode was exited (null if still in safe mode)
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Reason for entering safe mode
    /// </summary>
    public string EntryReason { get; set; } = string.Empty;
    
    /// <summary>
    /// How safe mode was exited
    /// </summary>
    public string? ExitReason { get; set; }
    
    /// <summary>
    /// Whether normal startup was attempted after safe mode
    /// </summary>
    public bool AttemptedNormalStartup { get; set; }
    
    /// <summary>
    /// Whether the subsequent normal startup was successful
    /// </summary>
    public bool? NormalStartupSuccessful { get; set; }
    
    /// <summary>
    /// Actions performed while in safe mode
    /// </summary>
    public List<string> ActionsPerformed { get; set; } = new();
    
    /// <summary>
    /// Duration of safe mode session
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
}

/// <summary>
/// Crash frequency statistics
/// </summary>
public class CrashFrequencyStats
{
    /// <summary>
    /// Total number of crashes recorded
    /// </summary>
    public int TotalCrashes { get; set; }
    
    /// <summary>
    /// Number of crashes in the last 24 hours
    /// </summary>
    public int CrashesLast24Hours { get; set; }
    
    /// <summary>
    /// Number of crashes in the last 7 days
    /// </summary>
    public int CrashesLast7Days { get; set; }
    
    /// <summary>
    /// Number of crashes in the last 30 days
    /// </summary>
    public int CrashesLast30Days { get; set; }
    
    /// <summary>
    /// Average crashes per day
    /// </summary>
    public double AverageCrashesPerDay { get; set; }
    
    /// <summary>
    /// When the most recent crash occurred
    /// </summary>
    public DateTime? MostRecentCrash { get; set; }
    
    /// <summary>
    /// Crashes grouped by component
    /// </summary>
    public Dictionary<string, int> CrashesByComponent { get; set; } = new();
    
    /// <summary>
    /// Crashes grouped by exception type
    /// </summary>
    public Dictionary<string, int> CrashesByExceptionType { get; set; } = new();
    
    /// <summary>
    /// Trend of crashes over time
    /// </summary>
    public CrashTrend CrashTrend { get; set; }
}

/// <summary>
/// Crash trend enumeration
/// </summary>
public enum CrashTrend
{
    Decreasing,
    Stable,
    Increasing
}

/// <summary>
/// Failure pattern analysis
/// </summary>
public class FailurePatternAnalysis
{
    /// <summary>
    /// Most common failure patterns
    /// </summary>
    public List<FailurePattern> CommonFailurePatterns { get; set; } = new();
    
    /// <summary>
    /// Failures by time of day (hour -> count)
    /// </summary>
    public Dictionary<int, int> FailuresByTimeOfDay { get; set; } = new();
    
    /// <summary>
    /// Failures by day of week
    /// </summary>
    public Dictionary<DayOfWeek, int> FailuresByDayOfWeek { get; set; } = new();
    
    /// <summary>
    /// Component failure rates (component -> percentage)
    /// </summary>
    public Dictionary<string, double> ComponentFailureRates { get; set; } = new();
    
    /// <summary>
    /// Correlated failures (failures that tend to occur together)
    /// </summary>
    public List<CorrelatedFailure> CorrelatedFailures { get; set; } = new();
    
    /// <summary>
    /// Recurring issues that happen frequently
    /// </summary>
    public List<RecurringIssue> RecurringIssues { get; set; } = new();
    
    /// <summary>
    /// When this analysis was performed
    /// </summary>
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Failure pattern
/// </summary>
public class FailurePattern
{
    /// <summary>
    /// Unique key for this pattern
    /// </summary>
    public string PatternKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Exception type
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Component where failures occur
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// How often this pattern occurs
    /// </summary>
    public int Frequency { get; set; }
    
    /// <summary>
    /// When this pattern was first observed
    /// </summary>
    public DateTime FirstOccurrence { get; set; }
    
    /// <summary>
    /// When this pattern was last observed
    /// </summary>
    public DateTime LastOccurrence { get; set; }
    
    /// <summary>
    /// Example stack traces for this pattern
    /// </summary>
    public List<string> ExampleStackTraces { get; set; } = new();
}

/// <summary>
/// Correlated failure
/// </summary>
public class CorrelatedFailure
{
    /// <summary>
    /// Primary component that fails first
    /// </summary>
    public string PrimaryComponent { get; set; } = string.Empty;
    
    /// <summary>
    /// Secondary component that fails after
    /// </summary>
    public string SecondaryComponent { get; set; } = string.Empty;
    
    /// <summary>
    /// Strength of correlation (higher = more correlated)
    /// </summary>
    public int CorrelationStrength { get; set; }
    
    /// <summary>
    /// Description of the correlation
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Recurring issue
/// </summary>
public class RecurringIssue
{
    /// <summary>
    /// Exception type
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Component where issue occurs
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// How many times this issue has occurred
    /// </summary>
    public int Frequency { get; set; }
    
    /// <summary>
    /// When this issue was first observed
    /// </summary>
    public DateTime FirstOccurrence { get; set; }
    
    /// <summary>
    /// When this issue was last observed
    /// </summary>
    public DateTime LastOccurrence { get; set; }
    
    /// <summary>
    /// Average time between occurrences
    /// </summary>
    public TimeSpan AverageTimeBetweenOccurrences { get; set; }
}

/// <summary>
/// Recovery success statistics
/// </summary>
public class RecoverySuccessStats
{
    /// <summary>
    /// All recovery attempts
    /// </summary>
    public List<RecoveryAttempt> Attempts { get; set; } = new();
    
    /// <summary>
    /// Overall success rate percentage
    /// </summary>
    public double OverallSuccessRate { get; set; }
    
    /// <summary>
    /// Success rate by recovery action type
    /// </summary>
    public Dictionary<string, double> SuccessRateByAction { get; set; } = new();
    
    /// <summary>
    /// Average time for recovery attempts
    /// </summary>
    public TimeSpan AverageRecoveryTime { get; set; }
    
    /// <summary>
    /// Most successful recovery action
    /// </summary>
    public string MostSuccessfulAction { get; set; } = string.Empty;
}

/// <summary>
/// Safe mode usage statistics
/// </summary>
public class SafeModeStats
{
    /// <summary>
    /// All safe mode usage records
    /// </summary>
    public List<SafeModeUsage> UsageHistory { get; set; } = new();
    
    /// <summary>
    /// Total number of times safe mode was used
    /// </summary>
    public int TotalUsages { get; set; }
    
    /// <summary>
    /// Number of safe mode usages in last 30 days
    /// </summary>
    public int UsagesLast30Days { get; set; }
    
    /// <summary>
    /// Average duration of safe mode sessions
    /// </summary>
    public TimeSpan AverageSessionDuration { get; set; }
    
    /// <summary>
    /// Reasons for exiting safe mode
    /// </summary>
    public Dictionary<string, int> ExitReasons { get; set; } = new();
}

/// <summary>
/// Session summary for current application session
/// </summary>
public class SessionSummary
{
    /// <summary>
    /// Number of crashes in this session
    /// </summary>
    public int CrashesThisSession { get; set; }
    
    /// <summary>
    /// Number of recovery attempts in this session
    /// </summary>
    public int RecoveryAttemptsThisSession { get; set; }
    
    /// <summary>
    /// Number of safe mode usages in this session
    /// </summary>
    public int SafeModeUsagesThisSession { get; set; }
    
    /// <summary>
    /// When this session started
    /// </summary>
    public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Comprehensive crash analytics report
/// </summary>
public class CrashAnalyticsReport
{
    /// <summary>
    /// When this report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Crash frequency statistics
    /// </summary>
    public CrashFrequencyStats CrashFrequency { get; set; } = new();
    
    /// <summary>
    /// Failure pattern analysis
    /// </summary>
    public FailurePatternAnalysis FailurePatterns { get; set; } = new();
    
    /// <summary>
    /// Recovery success statistics
    /// </summary>
    public RecoverySuccessStats RecoverySuccess { get; set; } = new();
    
    /// <summary>
    /// Safe mode usage statistics
    /// </summary>
    public SafeModeStats SafeModeUsage { get; set; } = new();
    
    /// <summary>
    /// Current session summary
    /// </summary>
    public SessionSummary SessionSummary { get; set; } = new();
    
    /// <summary>
    /// Get a summary of the report
    /// </summary>
    public string GetSummary()
    {
        var summary = $"Crash Analytics Report - Generated {GeneratedAt:yyyy-MM-dd HH:mm:ss}\n\n";
        
        summary += $"Crash Frequency:\n";
        summary += $"  Total Crashes: {CrashFrequency.TotalCrashes}\n";
        summary += $"  Last 24 Hours: {CrashFrequency.CrashesLast24Hours}\n";
        summary += $"  Last 7 Days: {CrashFrequency.CrashesLast7Days}\n";
        summary += $"  Trend: {CrashFrequency.CrashTrend}\n\n";
        
        summary += $"Recovery Success:\n";
        summary += $"  Overall Success Rate: {RecoverySuccess.OverallSuccessRate:F1}%\n";
        summary += $"  Total Attempts: {RecoverySuccess.Attempts.Count}\n";
        summary += $"  Most Successful Action: {RecoverySuccess.MostSuccessfulAction}\n\n";
        
        summary += $"Safe Mode Usage:\n";
        summary += $"  Total Usages: {SafeModeUsage.TotalUsages}\n";
        summary += $"  Last 30 Days: {SafeModeUsage.UsagesLast30Days}\n";
        summary += $"  Average Session: {SafeModeUsage.AverageSessionDuration.TotalMinutes:F1} minutes\n\n";
        
        summary += $"Current Session:\n";
        summary += $"  Crashes: {SessionSummary.CrashesThisSession}\n";
        summary += $"  Recovery Attempts: {SessionSummary.RecoveryAttemptsThisSession}\n";
        summary += $"  Safe Mode Usages: {SessionSummary.SafeModeUsagesThisSession}\n";
        
        return summary;
    }
}

/// <summary>
/// Failure patterns data for persistence
/// </summary>
public class FailurePatternsData
{
    /// <summary>
    /// List of failure patterns
    /// </summary>
    public List<FailurePattern> Patterns { get; set; } = new();
}