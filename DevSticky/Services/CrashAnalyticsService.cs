using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for crash reporting and analytics
/// Tracks crash frequency, failure patterns, recovery success rates, and safe mode usage
/// </summary>
public class CrashAnalyticsService : ICrashAnalyticsService
{
    private readonly IFileSystem _fileSystem;
    private readonly IErrorHandler _errorHandler;
    private readonly string _analyticsDirectory;
    private readonly string _crashHistoryFile;
    private readonly string _recoveryStatsFile;
    private readonly string _safeModeStatsFile;
    private readonly string _failurePatternsFile;
    
    private readonly List<CrashReport> _sessionCrashes = new();
    private readonly List<RecoveryAttempt> _sessionRecoveries = new();
    private readonly List<SafeModeUsage> _sessionSafeModeUsages = new();
    
    public CrashAnalyticsService(IFileSystem fileSystem, IErrorHandler errorHandler)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        
        _analyticsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevSticky", "Analytics");
        _crashHistoryFile = Path.Combine(_analyticsDirectory, "crash_history.json");
        _recoveryStatsFile = Path.Combine(_analyticsDirectory, "recovery_stats.json");
        _safeModeStatsFile = Path.Combine(_analyticsDirectory, "safe_mode_stats.json");
        _failurePatternsFile = Path.Combine(_analyticsDirectory, "failure_patterns.json");
        
        EnsureAnalyticsDirectory();
    }

    /// <summary>
    /// Record a crash occurrence
    /// </summary>
    public async Task RecordCrashAsync(CrashReport crashReport)
    {
        if (crashReport == null) throw new ArgumentNullException(nameof(crashReport));

        try
        {
            // Add to session crashes
            _sessionCrashes.Add(crashReport);
            
            // Load existing crash history
            var crashHistory = await LoadCrashHistoryAsync();
            crashHistory.Add(crashReport);
            
            // Keep only last 1000 crashes to prevent file from growing too large
            if (crashHistory.Count > 1000)
            {
                crashHistory = crashHistory.OrderByDescending(c => c.Timestamp).Take(1000).ToList();
            }
            
            // Save updated history
            await SaveCrashHistoryAsync(crashHistory);
            
            // Update failure patterns
            await UpdateFailurePatternsAsync(crashReport);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.RecordCrashAsync");
        }
    }

    /// <summary>
    /// Record a recovery attempt
    /// </summary>
    public async Task RecordRecoveryAttemptAsync(RecoveryAttempt recoveryAttempt)
    {
        if (recoveryAttempt == null) throw new ArgumentNullException(nameof(recoveryAttempt));

        try
        {
            // Add to session recoveries
            _sessionRecoveries.Add(recoveryAttempt);
            
            // Load existing recovery stats
            var recoveryStats = await LoadRecoveryStatsAsync();
            recoveryStats.Attempts.Add(recoveryAttempt);
            
            // Keep only last 500 recovery attempts
            if (recoveryStats.Attempts.Count > 500)
            {
                recoveryStats.Attempts = recoveryStats.Attempts.OrderByDescending(r => r.Timestamp).Take(500).ToList();
            }
            
            // Update aggregated statistics
            UpdateRecoveryStatistics(recoveryStats);
            
            // Save updated stats
            await SaveRecoveryStatsAsync(recoveryStats);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.RecordRecoveryAttemptAsync");
        }
    }

    /// <summary>
    /// Record safe mode usage
    /// </summary>
    public async Task RecordSafeModeUsageAsync(SafeModeUsage safeModeUsage)
    {
        if (safeModeUsage == null) throw new ArgumentNullException(nameof(safeModeUsage));

        try
        {
            // Add to session safe mode usages
            _sessionSafeModeUsages.Add(safeModeUsage);
            
            // Load existing safe mode stats
            var safeModeStats = await LoadSafeModeStatsAsync();
            safeModeStats.UsageHistory.Add(safeModeUsage);
            
            // Keep only last 200 safe mode usages
            if (safeModeStats.UsageHistory.Count > 200)
            {
                safeModeStats.UsageHistory = safeModeStats.UsageHistory.OrderByDescending(s => s.StartTime).Take(200).ToList();
            }
            
            // Update aggregated statistics
            UpdateSafeModeStatistics(safeModeStats);
            
            // Save updated stats
            await SaveSafeModeStatsAsync(safeModeStats);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.RecordSafeModeUsageAsync");
        }
    }

    /// <summary>
    /// Get crash frequency statistics
    /// </summary>
    public async Task<CrashFrequencyStats> GetCrashFrequencyStatsAsync()
    {
        try
        {
            var crashHistory = await LoadCrashHistoryAsync();
            var now = DateTime.UtcNow;
            
            var stats = new CrashFrequencyStats
            {
                TotalCrashes = crashHistory.Count,
                CrashesLast24Hours = crashHistory.Count(c => c.Timestamp >= now.AddDays(-1)),
                CrashesLast7Days = crashHistory.Count(c => c.Timestamp >= now.AddDays(-7)),
                CrashesLast30Days = crashHistory.Count(c => c.Timestamp >= now.AddDays(-30)),
                AverageCrashesPerDay = CalculateAverageCrashesPerDay(crashHistory),
                MostRecentCrash = crashHistory.OrderByDescending(c => c.Timestamp).FirstOrDefault()?.Timestamp,
                CrashesByComponent = crashHistory.GroupBy(c => c.Component)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CrashesByExceptionType = crashHistory.GroupBy(c => c.ExceptionType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CrashTrend = CalculateCrashTrend(crashHistory)
            };
            
            return stats;
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.GetCrashFrequencyStatsAsync");
            return new CrashFrequencyStats();
        }
    }

    /// <summary>
    /// Analyze failure patterns
    /// </summary>
    public async Task<FailurePatternAnalysis> AnalyzeFailurePatternsAsync()
    {
        try
        {
            var failurePatterns = await LoadFailurePatternsAsync();
            var crashHistory = await LoadCrashHistoryAsync();
            
            var analysis = new FailurePatternAnalysis
            {
                CommonFailurePatterns = failurePatterns.Patterns.OrderByDescending(p => p.Frequency).Take(10).ToList(),
                FailuresByTimeOfDay = AnalyzeFailuresByTimeOfDay(crashHistory),
                FailuresByDayOfWeek = AnalyzeFailuresByDayOfWeek(crashHistory),
                ComponentFailureRates = CalculateComponentFailureRates(crashHistory),
                CorrelatedFailures = FindCorrelatedFailures(crashHistory),
                RecurringIssues = IdentifyRecurringIssues(crashHistory),
                AnalysisTimestamp = DateTime.UtcNow
            };
            
            return analysis;
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.AnalyzeFailurePatternsAsync");
            return new FailurePatternAnalysis();
        }
    }

    /// <summary>
    /// Get recovery success rate statistics
    /// </summary>
    public async Task<RecoverySuccessStats> GetRecoverySuccessStatsAsync()
    {
        try
        {
            var recoveryStats = await LoadRecoveryStatsAsync();
            return recoveryStats;
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.GetRecoverySuccessStatsAsync");
            return new RecoverySuccessStats();
        }
    }

    /// <summary>
    /// Get safe mode usage statistics
    /// </summary>
    public async Task<SafeModeStats> GetSafeModeStatsAsync()
    {
        try
        {
            var safeModeStats = await LoadSafeModeStatsAsync();
            return safeModeStats;
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.GetSafeModeStatsAsync");
            return new SafeModeStats();
        }
    }

    /// <summary>
    /// Export comprehensive analytics report
    /// </summary>
    public async Task<CrashAnalyticsReport> GenerateAnalyticsReportAsync()
    {
        try
        {
            var report = new CrashAnalyticsReport
            {
                GeneratedAt = DateTime.UtcNow,
                CrashFrequency = await GetCrashFrequencyStatsAsync(),
                FailurePatterns = await AnalyzeFailurePatternsAsync(),
                RecoverySuccess = await GetRecoverySuccessStatsAsync(),
                SafeModeUsage = await GetSafeModeStatsAsync(),
                SessionSummary = new SessionSummary
                {
                    CrashesThisSession = _sessionCrashes.Count,
                    RecoveryAttemptsThisSession = _sessionRecoveries.Count,
                    SafeModeUsagesThisSession = _sessionSafeModeUsages.Count,
                    SessionStartTime = DateTime.UtcNow // This would be set when service is initialized
                }
            };
            
            return report;
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.GenerateAnalyticsReportAsync");
            return new CrashAnalyticsReport { GeneratedAt = DateTime.UtcNow };
        }
    }

    /// <summary>
    /// Clear old analytics data
    /// </summary>
    public async Task CleanupOldDataAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            
            // Clean crash history
            var crashHistory = await LoadCrashHistoryAsync();
            var filteredCrashes = crashHistory.Where(c => c.Timestamp >= cutoffDate).ToList();
            await SaveCrashHistoryAsync(filteredCrashes);
            
            // Clean recovery stats
            var recoveryStats = await LoadRecoveryStatsAsync();
            recoveryStats.Attempts = recoveryStats.Attempts.Where(r => r.Timestamp >= cutoffDate).ToList();
            UpdateRecoveryStatistics(recoveryStats);
            await SaveRecoveryStatsAsync(recoveryStats);
            
            // Clean safe mode stats
            var safeModeStats = await LoadSafeModeStatsAsync();
            safeModeStats.UsageHistory = safeModeStats.UsageHistory.Where(s => s.StartTime >= cutoffDate).ToList();
            UpdateSafeModeStatistics(safeModeStats);
            await SaveSafeModeStatsAsync(safeModeStats);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.CleanupOldDataAsync");
        }
    }

    #region Private Helper Methods

    private void EnsureAnalyticsDirectory()
    {
        try
        {
            if (!_fileSystem.DirectoryExists(_analyticsDirectory))
            {
                _fileSystem.CreateDirectory(_analyticsDirectory);
            }
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.EnsureAnalyticsDirectory");
        }
    }

    private async Task<List<CrashReport>> LoadCrashHistoryAsync()
    {
        try
        {
            if (!_fileSystem.FileExists(_crashHistoryFile))
                return new List<CrashReport>();

            var json = await _fileSystem.ReadAllTextAsync(_crashHistoryFile);
            return JsonSerializer.Deserialize<List<CrashReport>>(json) ?? new List<CrashReport>();
        }
        catch
        {
            return new List<CrashReport>();
        }
    }

    private async Task SaveCrashHistoryAsync(List<CrashReport> crashHistory)
    {
        try
        {
            var json = JsonSerializer.Serialize(crashHistory, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(_crashHistoryFile, json);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.SaveCrashHistoryAsync");
        }
    }

    private async Task<RecoverySuccessStats> LoadRecoveryStatsAsync()
    {
        try
        {
            if (!_fileSystem.FileExists(_recoveryStatsFile))
                return new RecoverySuccessStats();

            var json = await _fileSystem.ReadAllTextAsync(_recoveryStatsFile);
            return JsonSerializer.Deserialize<RecoverySuccessStats>(json) ?? new RecoverySuccessStats();
        }
        catch
        {
            return new RecoverySuccessStats();
        }
    }

    private async Task SaveRecoveryStatsAsync(RecoverySuccessStats stats)
    {
        try
        {
            var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(_recoveryStatsFile, json);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.SaveRecoveryStatsAsync");
        }
    }

    private async Task<SafeModeStats> LoadSafeModeStatsAsync()
    {
        try
        {
            if (!_fileSystem.FileExists(_safeModeStatsFile))
                return new SafeModeStats();

            var json = await _fileSystem.ReadAllTextAsync(_safeModeStatsFile);
            return JsonSerializer.Deserialize<SafeModeStats>(json) ?? new SafeModeStats();
        }
        catch
        {
            return new SafeModeStats();
        }
    }

    private async Task SaveSafeModeStatsAsync(SafeModeStats stats)
    {
        try
        {
            var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(_safeModeStatsFile, json);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.SaveSafeModeStatsAsync");
        }
    }

    private async Task<FailurePatternsData> LoadFailurePatternsAsync()
    {
        try
        {
            if (!_fileSystem.FileExists(_failurePatternsFile))
                return new FailurePatternsData();

            var json = await _fileSystem.ReadAllTextAsync(_failurePatternsFile);
            return JsonSerializer.Deserialize<FailurePatternsData>(json) ?? new FailurePatternsData();
        }
        catch
        {
            return new FailurePatternsData();
        }
    }

    private async Task SaveFailurePatternsAsync(FailurePatternsData patterns)
    {
        try
        {
            var json = JsonSerializer.Serialize(patterns, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(_failurePatternsFile, json);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.SaveFailurePatternsAsync");
        }
    }

    private async Task UpdateFailurePatternsAsync(CrashReport crashReport)
    {
        try
        {
            var patterns = await LoadFailurePatternsAsync();
            
            // Create pattern key from exception type and component
            var patternKey = $"{crashReport.ExceptionType}:{crashReport.Component}";
            
            var existingPattern = patterns.Patterns.FirstOrDefault(p => p.PatternKey == patternKey);
            if (existingPattern != null)
            {
                existingPattern.Frequency++;
                existingPattern.LastOccurrence = crashReport.Timestamp;
                existingPattern.ExampleStackTraces.Add(crashReport.StackTrace);
                
                // Keep only last 5 stack traces
                if (existingPattern.ExampleStackTraces.Count > 5)
                {
                    existingPattern.ExampleStackTraces = existingPattern.ExampleStackTraces.TakeLast(5).ToList();
                }
            }
            else
            {
                patterns.Patterns.Add(new FailurePattern
                {
                    PatternKey = patternKey,
                    ExceptionType = crashReport.ExceptionType,
                    Component = crashReport.Component,
                    Frequency = 1,
                    FirstOccurrence = crashReport.Timestamp,
                    LastOccurrence = crashReport.Timestamp,
                    ExampleStackTraces = new List<string> { crashReport.StackTrace }
                });
            }
            
            await SaveFailurePatternsAsync(patterns);
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "CrashAnalyticsService.UpdateFailurePatternsAsync");
        }
    }

    private void UpdateRecoveryStatistics(RecoverySuccessStats stats)
    {
        if (stats.Attempts.Count == 0)
        {
            stats.OverallSuccessRate = 0;
            stats.SuccessRateByAction = new Dictionary<string, double>();
            return;
        }

        var totalAttempts = stats.Attempts.Count;
        var successfulAttempts = stats.Attempts.Count(a => a.WasSuccessful);
        
        stats.OverallSuccessRate = (double)successfulAttempts / totalAttempts * 100;
        
        stats.SuccessRateByAction = stats.Attempts
            .GroupBy(a => a.RecoveryAction)
            .ToDictionary(
                g => g.Key,
                g => (double)g.Count(a => a.WasSuccessful) / g.Count() * 100
            );
        
        stats.AverageRecoveryTime = TimeSpan.FromMilliseconds(
            stats.Attempts.Where(a => a.Duration.HasValue)
                         .Average(a => a.Duration!.Value.TotalMilliseconds)
        );
        
        stats.MostSuccessfulAction = stats.SuccessRateByAction
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault().Key ?? "None";
    }

    private void UpdateSafeModeStatistics(SafeModeStats stats)
    {
        if (stats.UsageHistory.Count == 0)
        {
            stats.TotalUsages = 0;
            stats.AverageSessionDuration = TimeSpan.Zero;
            return;
        }

        stats.TotalUsages = stats.UsageHistory.Count;
        
        var completedSessions = stats.UsageHistory.Where(u => u.EndTime.HasValue).ToList();
        if (completedSessions.Count > 0)
        {
            stats.AverageSessionDuration = TimeSpan.FromMilliseconds(
                completedSessions.Average(s => (s.EndTime!.Value - s.StartTime).TotalMilliseconds)
            );
        }
        
        var now = DateTime.UtcNow;
        stats.UsagesLast30Days = stats.UsageHistory.Count(u => u.StartTime >= now.AddDays(-30));
        
        stats.ExitReasons = stats.UsageHistory
            .Where(u => !string.IsNullOrEmpty(u.ExitReason))
            .GroupBy(u => u.ExitReason!)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private double CalculateAverageCrashesPerDay(List<CrashReport> crashHistory)
    {
        if (crashHistory.Count == 0) return 0;
        
        var oldestCrash = crashHistory.Min(c => c.Timestamp);
        var daysSinceOldest = (DateTime.UtcNow - oldestCrash).TotalDays;
        
        return daysSinceOldest > 0 ? crashHistory.Count / daysSinceOldest : 0;
    }

    private CrashTrend CalculateCrashTrend(List<CrashReport> crashHistory)
    {
        if (crashHistory.Count < 2) return CrashTrend.Stable;
        
        var now = DateTime.UtcNow;
        var recentCrashes = crashHistory.Count(c => c.Timestamp >= now.AddDays(-7));
        var previousWeekCrashes = crashHistory.Count(c => c.Timestamp >= now.AddDays(-14) && c.Timestamp < now.AddDays(-7));
        
        if (recentCrashes > previousWeekCrashes * 1.2) return CrashTrend.Increasing;
        if (recentCrashes < previousWeekCrashes * 0.8) return CrashTrend.Decreasing;
        return CrashTrend.Stable;
    }

    private Dictionary<int, int> AnalyzeFailuresByTimeOfDay(List<CrashReport> crashHistory)
    {
        return crashHistory
            .GroupBy(c => c.Timestamp.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<DayOfWeek, int> AnalyzeFailuresByDayOfWeek(List<CrashReport> crashHistory)
    {
        return crashHistory
            .GroupBy(c => c.Timestamp.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<string, double> CalculateComponentFailureRates(List<CrashReport> crashHistory)
    {
        var totalCrashes = crashHistory.Count;
        if (totalCrashes == 0) return new Dictionary<string, double>();
        
        return crashHistory
            .GroupBy(c => c.Component)
            .ToDictionary(g => g.Key, g => (double)g.Count() / totalCrashes * 100);
    }

    private List<CorrelatedFailure> FindCorrelatedFailures(List<CrashReport> crashHistory)
    {
        var correlations = new List<CorrelatedFailure>();
        
        // Group crashes by time windows (within 1 hour of each other)
        var timeWindows = new List<List<CrashReport>>();
        var sortedCrashes = crashHistory.OrderBy(c => c.Timestamp).ToList();
        
        for (int i = 0; i < sortedCrashes.Count; i++)
        {
            var window = new List<CrashReport> { sortedCrashes[i] };
            var windowStart = sortedCrashes[i].Timestamp;
            
            for (int j = i + 1; j < sortedCrashes.Count; j++)
            {
                if (sortedCrashes[j].Timestamp - windowStart <= TimeSpan.FromHours(1))
                {
                    window.Add(sortedCrashes[j]);
                }
                else
                {
                    break;
                }
            }
            
            if (window.Count > 1)
            {
                timeWindows.Add(window);
            }
        }
        
        // Find patterns in time windows
        foreach (var window in timeWindows)
        {
            var componentPairs = window
                .SelectMany(c1 => window.Where(c2 => c1 != c2), (c1, c2) => new { First = c1.Component, Second = c2.Component })
                .Where(pair => pair.First != pair.Second)
                .GroupBy(pair => $"{pair.First}->{pair.Second}")
                .Where(g => g.Count() >= 2) // At least 2 occurrences
                .Select(g => new CorrelatedFailure
                {
                    PrimaryComponent = g.First().First,
                    SecondaryComponent = g.First().Second,
                    CorrelationStrength = g.Count(),
                    Description = $"{g.First().First} failures often followed by {g.First().Second} failures"
                });
                
            correlations.AddRange(componentPairs);
        }
        
        return correlations.Take(10).ToList(); // Return top 10 correlations
    }

    private List<RecurringIssue> IdentifyRecurringIssues(List<CrashReport> crashHistory)
    {
        return crashHistory
            .GroupBy(c => new { c.ExceptionType, c.Component })
            .Where(g => g.Count() >= 3) // At least 3 occurrences
            .Select(g => new RecurringIssue
            {
                ExceptionType = g.Key.ExceptionType,
                Component = g.Key.Component,
                Frequency = g.Count(),
                FirstOccurrence = g.Min(c => c.Timestamp),
                LastOccurrence = g.Max(c => c.Timestamp),
                AverageTimeBetweenOccurrences = CalculateAverageTimeBetween(g.Select(c => c.Timestamp).OrderBy(t => t).ToList())
            })
            .OrderByDescending(i => i.Frequency)
            .Take(10)
            .ToList();
    }

    private TimeSpan CalculateAverageTimeBetween(List<DateTime> timestamps)
    {
        if (timestamps.Count < 2) return TimeSpan.Zero;
        
        var intervals = new List<TimeSpan>();
        for (int i = 1; i < timestamps.Count; i++)
        {
            intervals.Add(timestamps[i] - timestamps[i - 1]);
        }
        
        return TimeSpan.FromMilliseconds(intervals.Average(i => i.TotalMilliseconds));
    }

    #endregion

    public void Dispose()
    {
        // No resources to dispose
    }
}