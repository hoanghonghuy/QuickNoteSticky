using System;
using System.Collections.Generic;
using System.Linq;

namespace DevSticky.Models;

/// <summary>
/// Enhanced performance metrics for startup monitoring and validation overhead tracking
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Overall startup performance metrics
    /// </summary>
    public StartupPerformanceMetrics Startup { get; set; } = new();
    
    /// <summary>
    /// Validation overhead metrics
    /// </summary>
    public ValidationOverheadMetrics Validation { get; set; } = new();
    
    /// <summary>
    /// Memory usage metrics throughout startup
    /// </summary>
    public MemoryUsageMetrics Memory { get; set; } = new();
    
    /// <summary>
    /// Timing breakdown by categories
    /// </summary>
    public TimingBreakdownMetrics Timing { get; set; } = new();
    
    /// <summary>
    /// Performance thresholds and warnings
    /// </summary>
    public PerformanceThresholds Thresholds { get; set; } = new();
    
    /// <summary>
    /// Performance warnings detected during startup
    /// </summary>
    public List<PerformanceWarning> Warnings { get; set; } = new();
    
    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Check if startup performance meets acceptable thresholds
    /// </summary>
    public bool IsPerformanceAcceptable => 
        Startup.TotalDuration <= Thresholds.MaxAcceptableStartupTime &&
        Validation.TotalOverhead <= Thresholds.MaxAcceptableValidationOverhead &&
        Memory.PeakUsageMB <= Thresholds.MaxAcceptableMemoryUsageMB;
    
    /// <summary>
    /// Get performance summary as formatted string
    /// </summary>
    public string GetSummary()
    {
        var summary = $"Startup Performance Summary:\n";
        summary += $"  Total Duration: {Startup.TotalDuration.TotalMilliseconds:F2}ms\n";
        summary += $"  Validation Overhead: {Validation.TotalOverhead.TotalMilliseconds:F2}ms ({Validation.OverheadPercentage:F1}%)\n";
        summary += $"  Peak Memory: {Memory.PeakUsageMB}MB\n";
        summary += $"  Performance Acceptable: {(IsPerformanceAcceptable ? "✅ Yes" : "❌ No")}\n";
        
        if (Warnings.Count > 0)
        {
            summary += $"  Warnings: {Warnings.Count}\n";
            foreach (var warning in Warnings.Take(3))
            {
                summary += $"    - {warning.Message}\n";
            }
        }
        
        return summary;
    }
}

/// <summary>
/// Startup performance metrics
/// </summary>
public class StartupPerformanceMetrics
{
    /// <summary>
    /// Total startup duration from first step to completion
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
    
    /// <summary>
    /// Time to first service registration
    /// </summary>
    public TimeSpan TimeToFirstService { get; set; }
    
    /// <summary>
    /// Time to UI ready (tray icon visible)
    /// </summary>
    public TimeSpan TimeToUIReady { get; set; }
    
    /// <summary>
    /// Time to fully functional (all services initialized)
    /// </summary>
    public TimeSpan TimeToFullyFunctional { get; set; }
    
    /// <summary>
    /// Number of startup steps executed
    /// </summary>
    public int TotalSteps { get; set; }
    
    /// <summary>
    /// Number of failed steps
    /// </summary>
    public int FailedSteps { get; set; }
    
    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalSteps > 0 ? ((double)(TotalSteps - FailedSteps) / TotalSteps) * 100 : 0;
}

/// <summary>
/// Validation overhead metrics
/// </summary>
public class ValidationOverheadMetrics
{
    /// <summary>
    /// Total time spent on validation steps
    /// </summary>
    public TimeSpan TotalOverhead { get; set; }
    
    /// <summary>
    /// Percentage of total startup time spent on validation
    /// </summary>
    public double OverheadPercentage { get; set; }
    
    /// <summary>
    /// Time spent on directory validation
    /// </summary>
    public TimeSpan DirectoryValidationTime { get; set; }
    
    /// <summary>
    /// Time spent on dependency validation
    /// </summary>
    public TimeSpan DependencyValidationTime { get; set; }
    
    /// <summary>
    /// Time spent on configuration validation
    /// </summary>
    public TimeSpan ConfigurationValidationTime { get; set; }
    
    /// <summary>
    /// Time spent on service validation
    /// </summary>
    public TimeSpan ServiceValidationTime { get; set; }
    
    /// <summary>
    /// Time spent on resource validation
    /// </summary>
    public TimeSpan ResourceValidationTime { get; set; }
    
    /// <summary>
    /// Number of validation checks performed
    /// </summary>
    public int ValidationChecksPerformed { get; set; }
    
    /// <summary>
    /// Average time per validation check
    /// </summary>
    public TimeSpan AverageValidationTime => ValidationChecksPerformed > 0 
        ? TimeSpan.FromMilliseconds(TotalOverhead.TotalMilliseconds / ValidationChecksPerformed) 
        : TimeSpan.Zero;
}

/// <summary>
/// Memory usage metrics
/// </summary>
public class MemoryUsageMetrics
{
    /// <summary>
    /// Memory usage at startup begin (MB)
    /// </summary>
    public long StartupMemoryMB { get; set; }
    
    /// <summary>
    /// Peak memory usage during startup (MB)
    /// </summary>
    public long PeakUsageMB { get; set; }
    
    /// <summary>
    /// Memory usage at startup completion (MB)
    /// </summary>
    public long FinalMemoryMB { get; set; }
    
    /// <summary>
    /// Total memory allocated during startup (MB)
    /// </summary>
    public long TotalAllocatedMB { get; set; }
    
    /// <summary>
    /// Memory growth during startup (MB)
    /// </summary>
    public long MemoryGrowthMB => FinalMemoryMB - StartupMemoryMB;
    
    /// <summary>
    /// Memory usage by phase
    /// </summary>
    public Dictionary<string, long> MemoryByPhase { get; set; } = new();
    
    /// <summary>
    /// Memory usage by component
    /// </summary>
    public Dictionary<string, long> MemoryByComponent { get; set; } = new();
    
    /// <summary>
    /// Garbage collection statistics during startup
    /// </summary>
    public GarbageCollectionMetrics GC { get; set; } = new();
}

/// <summary>
/// Timing breakdown metrics
/// </summary>
public class TimingBreakdownMetrics
{
    /// <summary>
    /// Time spent on service initialization
    /// </summary>
    public TimeSpan ServiceInitializationTime { get; set; }
    
    /// <summary>
    /// Time spent on configuration loading
    /// </summary>
    public TimeSpan ConfigurationLoadingTime { get; set; }
    
    /// <summary>
    /// Time spent on UI setup
    /// </summary>
    public TimeSpan UISetupTime { get; set; }
    
    /// <summary>
    /// Time spent on resource loading
    /// </summary>
    public TimeSpan ResourceLoadingTime { get; set; }
    
    /// <summary>
    /// Time spent on data loading
    /// </summary>
    public TimeSpan DataLoadingTime { get; set; }
    
    /// <summary>
    /// Time spent on optional services (cloud sync, hotkeys, etc.)
    /// </summary>
    public TimeSpan OptionalServicesTime { get; set; }
    
    /// <summary>
    /// Time spent on crash fix infrastructure
    /// </summary>
    public TimeSpan CrashFixInfrastructureTime { get; set; }
    
    /// <summary>
    /// Get timing breakdown as percentages
    /// </summary>
    public Dictionary<string, double> GetPercentageBreakdown(TimeSpan totalTime)
    {
        if (totalTime.TotalMilliseconds == 0) return new Dictionary<string, double>();
        
        return new Dictionary<string, double>
        {
            ["Service Initialization"] = (ServiceInitializationTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100,
            ["Configuration Loading"] = (ConfigurationLoadingTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100,
            ["UI Setup"] = (UISetupTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100,
            ["Resource Loading"] = (ResourceLoadingTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100,
            ["Data Loading"] = (DataLoadingTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100,
            ["Optional Services"] = (OptionalServicesTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100,
            ["Crash Fix Infrastructure"] = (CrashFixInfrastructureTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100
        };
    }
}

/// <summary>
/// Garbage collection metrics
/// </summary>
public class GarbageCollectionMetrics
{
    /// <summary>
    /// GC collections during startup by generation
    /// </summary>
    public Dictionary<int, int> CollectionsByGeneration { get; set; } = new();
    
    /// <summary>
    /// Total memory before GC (bytes)
    /// </summary>
    public long MemoryBeforeGC { get; set; }
    
    /// <summary>
    /// Total memory after GC (bytes)
    /// </summary>
    public long MemoryAfterGC { get; set; }
    
    /// <summary>
    /// Memory freed by GC (bytes)
    /// </summary>
    public long MemoryFreed => MemoryBeforeGC - MemoryAfterGC;
}

/// <summary>
/// Performance thresholds for warnings
/// </summary>
public class PerformanceThresholds
{
    /// <summary>
    /// Maximum acceptable startup time (default: 5 seconds)
    /// </summary>
    public TimeSpan MaxAcceptableStartupTime { get; set; } = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Maximum acceptable validation overhead (default: 100ms)
    /// </summary>
    public TimeSpan MaxAcceptableValidationOverhead { get; set; } = TimeSpan.FromMilliseconds(100);
    
    /// <summary>
    /// Maximum acceptable memory usage (default: 200MB)
    /// </summary>
    public long MaxAcceptableMemoryUsageMB { get; set; } = 200;
    
    /// <summary>
    /// Warning threshold for slow steps (default: 500ms)
    /// </summary>
    public TimeSpan SlowStepThreshold { get; set; } = TimeSpan.FromMilliseconds(500);
    
    /// <summary>
    /// Warning threshold for high memory allocation (default: 50MB)
    /// </summary>
    public long HighMemoryAllocationThresholdMB { get; set; } = 50;
}

/// <summary>
/// Performance warning
/// </summary>
public class PerformanceWarning
{
    /// <summary>
    /// Warning severity
    /// </summary>
    public PerformanceWarningSeverity Severity { get; set; }
    
    /// <summary>
    /// Warning category
    /// </summary>
    public PerformanceWarningCategory Category { get; set; }
    
    /// <summary>
    /// Warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Component that triggered the warning
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Measured value that triggered the warning
    /// </summary>
    public object? MeasuredValue { get; set; }
    
    /// <summary>
    /// Threshold that was exceeded
    /// </summary>
    public object? Threshold { get; set; }
    
    /// <summary>
    /// Suggested action to address the warning
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;
}

/// <summary>
/// Performance warning severity levels
/// </summary>
public enum PerformanceWarningSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Performance warning categories
/// </summary>
public enum PerformanceWarningCategory
{
    StartupTime,
    MemoryUsage,
    ValidationOverhead,
    StepDuration,
    ResourceUsage
}