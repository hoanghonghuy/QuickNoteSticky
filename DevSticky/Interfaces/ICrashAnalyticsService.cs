using System;
using System.Threading.Tasks;
using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for crash reporting and analytics service
/// </summary>
public interface ICrashAnalyticsService : IDisposable
{
    /// <summary>
    /// Record a crash occurrence
    /// </summary>
    /// <param name="crashReport">The crash report to record</param>
    Task RecordCrashAsync(CrashReport crashReport);
    
    /// <summary>
    /// Record a recovery attempt
    /// </summary>
    /// <param name="recoveryAttempt">The recovery attempt to record</param>
    Task RecordRecoveryAttemptAsync(RecoveryAttempt recoveryAttempt);
    
    /// <summary>
    /// Record safe mode usage
    /// </summary>
    /// <param name="safeModeUsage">The safe mode usage to record</param>
    Task RecordSafeModeUsageAsync(SafeModeUsage safeModeUsage);
    
    /// <summary>
    /// Get crash frequency statistics
    /// </summary>
    /// <returns>Crash frequency statistics</returns>
    Task<CrashFrequencyStats> GetCrashFrequencyStatsAsync();
    
    /// <summary>
    /// Analyze failure patterns
    /// </summary>
    /// <returns>Failure pattern analysis</returns>
    Task<FailurePatternAnalysis> AnalyzeFailurePatternsAsync();
    
    /// <summary>
    /// Get recovery success rate statistics
    /// </summary>
    /// <returns>Recovery success statistics</returns>
    Task<RecoverySuccessStats> GetRecoverySuccessStatsAsync();
    
    /// <summary>
    /// Get safe mode usage statistics
    /// </summary>
    /// <returns>Safe mode usage statistics</returns>
    Task<SafeModeStats> GetSafeModeStatsAsync();
    
    /// <summary>
    /// Generate comprehensive analytics report
    /// </summary>
    /// <returns>Complete analytics report</returns>
    Task<CrashAnalyticsReport> GenerateAnalyticsReportAsync();
    
    /// <summary>
    /// Clean up old analytics data
    /// </summary>
    /// <param name="maxAge">Maximum age of data to keep</param>
    Task CleanupOldDataAsync(TimeSpan maxAge);
}