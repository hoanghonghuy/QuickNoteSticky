using DevSticky.Helpers;
using DevSticky.Interfaces;

namespace DevSticky.Models;

/// <summary>
/// Complete memory profiling result for the application.
/// </summary>
public class MemoryProfilingResult
{
    public DateTime ProfileDate { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    
    public MemoryUsageProfile MemoryUsageProfile { get; set; } = new();
    public MemoryLeakAnalysis MemoryLeakAnalysis { get; set; } = new();
    public CacheLimitVerification CacheLimitVerification { get; set; } = new();
    public DisposalVerification DisposalVerification { get; set; } = new();

    /// <summary>
    /// Generates a comprehensive report of all memory profiling results.
    /// </summary>
    public string GenerateReport()
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine("=== MEMORY PROFILING REPORT ===");
        report.AppendLine($"Profile Date: {ProfileDate:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"Machine: {MachineName}");
        report.AppendLine($"Processors: {ProcessorCount}");
        report.AppendLine($"OS: {OperatingSystem}");
        report.AppendLine();

        // Memory Usage Profile
        report.AppendLine("--- MEMORY USAGE PROFILE (100 NOTES) ---");
        report.AppendLine($"Baseline Memory: {MemoryUsageProfile.BaselineMemory.WorkingSetMB:F2} MB");
        report.AppendLine($"After Load Memory: {MemoryUsageProfile.AfterLoadMemory.WorkingSetMB:F2} MB");
        report.AppendLine($"Peak Memory: {MemoryUsageProfile.PeakMemory.WorkingSetMB:F2} MB");
        report.AppendLine($"Final Memory: {MemoryUsageProfile.FinalMemory.WorkingSetMB:F2} MB");
        report.AppendLine($"Memory per Note: {MemoryUsageProfile.MemoryPerNote:F4} MB");
        report.AppendLine($"Total Growth: {MemoryUsageProfile.TotalMemoryGrowth:F2} MB");
        report.AppendLine($"Managed Growth: {MemoryUsageProfile.ManagedMemoryGrowth:F2} MB");
        report.AppendLine($"Target (Growth <50MB): {(MemoryUsageProfile.MeetsTarget ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine();

        // Memory Leak Analysis
        report.AppendLine("--- MEMORY LEAK ANALYSIS ---");
        report.AppendLine($"Total Issues: {MemoryLeakAnalysis.TotalIssues}");
        report.AppendLine($"High Severity: {MemoryLeakAnalysis.HighSeverityIssues}");
        report.AppendLine($"Critical Issues: {MemoryLeakAnalysis.CriticalIssues}");
        report.AppendLine($"Has Critical Leaks: {(MemoryLeakAnalysis.HasCriticalLeaks ? "✗ YES" : "✓ NO")}");
        
        if (MemoryLeakAnalysis.Issues.Any())
        {
            report.AppendLine("Issues Found:");
            foreach (var issue in MemoryLeakAnalysis.Issues.OrderByDescending(i => i.Severity))
            {
                report.AppendLine($"  [{issue.Severity}] {issue.Description}");
            }
        }
        report.AppendLine();

        // Cache Limit Verification
        report.AppendLine("--- CACHE LIMIT VERIFICATION ---");
        report.AppendLine($"Tag Cache Limit Enforced: {(CacheLimitVerification.TagCacheLimitEnforced ? "✓ YES" : "✗ NO")}");
        report.AppendLine($"Group Cache Limit Enforced: {(CacheLimitVerification.GroupCacheLimitEnforced ? "✓ YES" : "✗ NO")}");
        report.AppendLine($"Overall Success: {(CacheLimitVerification.Success ? "✓ PASS" : "✗ FAIL")}");
        
        if (CacheLimitVerification.FinalCacheStats != null)
        {
            var stats = CacheLimitVerification.FinalCacheStats;
            report.AppendLine($"Final Tag Cache: {stats.TagCacheSize}/{stats.TagCacheMaxSize}");
            report.AppendLine($"Final Group Cache: {stats.GroupCacheSize}/{stats.GroupCacheMaxSize}");
        }
        
        if (!string.IsNullOrEmpty(CacheLimitVerification.Error))
        {
            report.AppendLine($"Error: {CacheLimitVerification.Error}");
        }
        report.AppendLine();

        // Disposal Verification
        report.AppendLine("--- DISPOSAL VERIFICATION ---");
        report.AppendLine($"Total Issues: {DisposalVerification.TotalIssues}");
        report.AppendLine($"Critical Issues: {DisposalVerification.CriticalIssues}");
        report.AppendLine($"Success: {(DisposalVerification.Success ? "✓ PASS" : "✗ FAIL")}");
        
        if (DisposalVerification.Issues.Any())
        {
            report.AppendLine("Issues Found:");
            foreach (var issue in DisposalVerification.Issues.OrderByDescending(i => i.Severity))
            {
                report.AppendLine($"  [{issue.Severity}] {issue.ServiceName}: {issue.Description}");
            }
        }
        report.AppendLine();

        // Overall Summary
        report.AppendLine("--- OVERALL SUMMARY ---");
        var overallSuccess = MemoryUsageProfile.MeetsTarget && 
                           !MemoryLeakAnalysis.HasCriticalLeaks && 
                           CacheLimitVerification.Success && 
                           DisposalVerification.Success;
        
        report.AppendLine($"Overall Status: {(overallSuccess ? "✓ PASS" : "✗ FAIL")}");
        
        if (!overallSuccess)
        {
            report.AppendLine("Issues to Address:");
            if (!MemoryUsageProfile.MeetsTarget)
                report.AppendLine("  - Memory growth exceeds 50MB target for 100 notes");
            if (MemoryLeakAnalysis.HasCriticalLeaks)
                report.AppendLine("  - Critical memory leaks detected");
            if (!CacheLimitVerification.Success)
                report.AppendLine("  - Cache size limits not properly enforced");
            if (!DisposalVerification.Success)
                report.AppendLine("  - Resource disposal issues detected");
        }

        return report.ToString();
    }
}

/// <summary>
/// Detailed memory usage profile with 100 notes.
/// </summary>
public class MemoryUsageProfile
{
    public DetailedMemoryUsage BaselineMemory { get; set; } = new();
    public DetailedMemoryUsage AfterLoadMemory { get; set; } = new();
    public DetailedMemoryUsage AfterUsageMemory { get; set; } = new();
    public DetailedMemoryUsage PeakMemory { get; set; } = new();
    public DetailedMemoryUsage FinalMemory { get; set; } = new();
    
    public double MemoryPerNote { get; set; }
    public double TotalMemoryGrowth { get; set; }
    public double ManagedMemoryGrowth { get; set; }
    public bool MeetsTarget { get; set; }
}

/// <summary>
/// Detailed memory usage information at a specific point in time.
/// </summary>
public class DetailedMemoryUsage
{
    public double WorkingSetMB { get; set; }
    public double PrivateMemoryMB { get; set; }
    public double ManagedMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public DateTime Timestamp { get; set; }

    public override string ToString()
    {
        return $"Working Set: {WorkingSetMB:F2} MB, " +
               $"Private: {PrivateMemoryMB:F2} MB, " +
               $"Managed: {ManagedMemoryMB:F2} MB, " +
               $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
    }
}

/// <summary>
/// Analysis of memory leaks in the application.
/// </summary>
public class MemoryLeakAnalysis
{
    public List<MemoryLeakIssue> Issues { get; set; } = new();
    public int TotalIssues { get; set; }
    public int HighSeverityIssues { get; set; }
    public int CriticalIssues { get; set; }
    public bool HasCriticalLeaks { get; set; }
}

/// <summary>
/// Verification that cache size limits are properly enforced.
/// </summary>
public class CacheLimitVerification
{
    public CacheStatistics? InitialCacheStats { get; set; }
    public CacheStatistics? AfterTagTestStats { get; set; }
    public CacheStatistics? FinalCacheStats { get; set; }
    
    public bool TagCacheLimitEnforced { get; set; }
    public bool GroupCacheLimitEnforced { get; set; }
    public bool OverallLimitEnforced { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Verification that all disposable resources are properly disposed.
/// </summary>
public class DisposalVerification
{
    public List<DisposalIssue> Issues { get; set; } = new();
    public int TotalIssues { get; set; }
    public int CriticalIssues { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Represents an issue with resource disposal.
/// </summary>
public class DisposalIssue
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DisposalSeverity Severity { get; set; }
}

/// <summary>
/// Severity levels for disposal issues.
/// </summary>
public enum DisposalSeverity
{
    Low,
    Medium,
    High,
    Critical
}