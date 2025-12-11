using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Enhanced performance monitoring service for startup and validation overhead tracking
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly IStartupDiagnostics _startupDiagnostics;
    private readonly IErrorHandler? _errorHandler;
    private readonly PerformanceThresholds _thresholds;
    private readonly List<PerformanceWarning> _warnings = new();
    private readonly Dictionary<string, Stopwatch> _categoryTimers = new();
    private readonly Dictionary<string, long> _categoryMemoryStart = new();
    
    private DateTime _startupBeginTime;
    private DateTime? _firstServiceTime;
    private DateTime? _uiReadyTime;
    private DateTime? _fullyFunctionalTime;
    private long _startupMemoryMB;
    private long _peakMemoryMB;
    
    // GC tracking
    private readonly Dictionary<int, int> _gcCollectionsStart = new();
    private readonly Dictionary<int, int> _gcCollectionsEnd = new();
    private long _memoryBeforeGC;
    private long _memoryAfterGC;

    public PerformanceMonitoringService(IStartupDiagnostics startupDiagnostics, IErrorHandler? errorHandler = null)
    {
        _startupDiagnostics = startupDiagnostics ?? throw new ArgumentNullException(nameof(startupDiagnostics));
        _errorHandler = errorHandler;
        _thresholds = new PerformanceThresholds();
        
        InitializeMonitoring();
    }

    /// <summary>
    /// Initialize performance monitoring
    /// </summary>
    private void InitializeMonitoring()
    {
        _startupBeginTime = DateTime.UtcNow;
        _startupMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        _peakMemoryMB = _startupMemoryMB;
        
        // Record initial GC state
        for (int i = 0; i <= GC.MaxGeneration; i++)
        {
            _gcCollectionsStart[i] = GC.CollectionCount(i);
        }
        _memoryBeforeGC = GC.GetTotalMemory(false);
        
        // Initialize category timers
        InitializeCategoryTimers();
    }

    /// <summary>
    /// Initialize timing categories
    /// </summary>
    private void InitializeCategoryTimers()
    {
        var categories = new[]
        {
            "ServiceInitialization",
            "ConfigurationLoading", 
            "UISetup",
            "ResourceLoading",
            "DataLoading",
            "OptionalServices",
            "CrashFixInfrastructure",
            "Validation"
        };

        foreach (var category in categories)
        {
            _categoryTimers[category] = new Stopwatch();
            _categoryMemoryStart[category] = 0;
        }
    }

    /// <summary>
    /// Start timing a category
    /// </summary>
    public void StartCategoryTiming(string category)
    {
        if (_categoryTimers.TryGetValue(category, out var timer))
        {
            timer.Start();
            _categoryMemoryStart[category] = GC.GetTotalMemory(false) / (1024 * 1024);
        }
    }

    /// <summary>
    /// Stop timing a category
    /// </summary>
    public void StopCategoryTiming(string category)
    {
        if (_categoryTimers.TryGetValue(category, out var timer))
        {
            timer.Stop();
            
            // Update peak memory if needed
            var currentMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            if (currentMemory > _peakMemoryMB)
            {
                _peakMemoryMB = currentMemory;
            }
            
            // Check for performance warnings
            CheckCategoryPerformance(category, timer.Elapsed, currentMemory - _categoryMemoryStart[category]);
        }
    }

    /// <summary>
    /// Mark milestone timestamps
    /// </summary>
    public void MarkMilestone(string milestone)
    {
        var now = DateTime.UtcNow;
        
        switch (milestone.ToLowerInvariant())
        {
            case "firstservice":
                _firstServiceTime = now;
                break;
            case "uiready":
                _uiReadyTime = now;
                break;
            case "fullyfunctional":
                _fullyFunctionalTime = now;
                break;
        }
    }

    /// <summary>
    /// Check category performance against thresholds
    /// </summary>
    private void CheckCategoryPerformance(string category, TimeSpan duration, long memoryDeltaMB)
    {
        // Check for slow operations
        if (duration > _thresholds.SlowStepThreshold)
        {
            _warnings.Add(new PerformanceWarning
            {
                Severity = PerformanceWarningSeverity.Warning,
                Category = PerformanceWarningCategory.StepDuration,
                Component = category,
                Message = $"{category} took {duration.TotalMilliseconds:F2}ms (threshold: {_thresholds.SlowStepThreshold.TotalMilliseconds}ms)",
                MeasuredValue = duration,
                Threshold = _thresholds.SlowStepThreshold,
                SuggestedAction = "Consider optimizing this operation or investigating potential bottlenecks"
            });
        }

        // Check for high memory allocation
        if (memoryDeltaMB > _thresholds.HighMemoryAllocationThresholdMB)
        {
            _warnings.Add(new PerformanceWarning
            {
                Severity = PerformanceWarningSeverity.Warning,
                Category = PerformanceWarningCategory.MemoryUsage,
                Component = category,
                Message = $"{category} allocated {memoryDeltaMB}MB (threshold: {_thresholds.HighMemoryAllocationThresholdMB}MB)",
                MeasuredValue = memoryDeltaMB,
                Threshold = _thresholds.HighMemoryAllocationThresholdMB,
                SuggestedAction = "Review memory usage patterns and consider optimizing allocations"
            });
        }
    }

    /// <summary>
    /// Get comprehensive performance metrics
    /// </summary>
    public PerformanceMetrics GetPerformanceMetrics()
    {
        var startupMetrics = _startupDiagnostics.GetPerformanceMetrics();
        var totalDuration = _startupDiagnostics.GetTotalStartupDuration() ?? TimeSpan.Zero;
        
        // Record final GC state
        for (int i = 0; i <= GC.MaxGeneration; i++)
        {
            _gcCollectionsEnd[i] = GC.CollectionCount(i);
        }
        _memoryAfterGC = GC.GetTotalMemory(false);
        
        var currentMemory = GC.GetTotalMemory(false) / (1024 * 1024);
        
        var metrics = new PerformanceMetrics
        {
            Startup = new StartupPerformanceMetrics
            {
                TotalDuration = totalDuration,
                TimeToFirstService = _firstServiceTime?.Subtract(_startupBeginTime) ?? TimeSpan.Zero,
                TimeToUIReady = _uiReadyTime?.Subtract(_startupBeginTime) ?? TimeSpan.Zero,
                TimeToFullyFunctional = _fullyFunctionalTime?.Subtract(_startupBeginTime) ?? TimeSpan.Zero,
                TotalSteps = startupMetrics.TotalSteps,
                FailedSteps = startupMetrics.FailedSteps
            },
            
            Validation = CalculateValidationOverhead(totalDuration),
            
            Memory = new MemoryUsageMetrics
            {
                StartupMemoryMB = _startupMemoryMB,
                PeakUsageMB = _peakMemoryMB,
                FinalMemoryMB = currentMemory,
                TotalAllocatedMB = startupMetrics.TotalMemoryAllocatedMB,
                MemoryByPhase = startupMetrics.PhaseBreakdown.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.MemoryAllocatedMB),
                MemoryByComponent = startupMetrics.ComponentBreakdown.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.MemoryAllocatedMB),
                GC = new GarbageCollectionMetrics
                {
                    CollectionsByGeneration = _gcCollectionsEnd.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value - _gcCollectionsStart.GetValueOrDefault(kvp.Key, 0)),
                    MemoryBeforeGC = _memoryBeforeGC,
                    MemoryAfterGC = _memoryAfterGC
                }
            },
            
            Timing = new TimingBreakdownMetrics
            {
                ServiceInitializationTime = _categoryTimers["ServiceInitialization"].Elapsed,
                ConfigurationLoadingTime = _categoryTimers["ConfigurationLoading"].Elapsed,
                UISetupTime = _categoryTimers["UISetup"].Elapsed,
                ResourceLoadingTime = _categoryTimers["ResourceLoading"].Elapsed,
                DataLoadingTime = _categoryTimers["DataLoading"].Elapsed,
                OptionalServicesTime = _categoryTimers["OptionalServices"].Elapsed,
                CrashFixInfrastructureTime = _categoryTimers["CrashFixInfrastructure"].Elapsed
            },
            
            Thresholds = _thresholds,
            Warnings = _warnings.ToList()
        };

        // Check overall performance thresholds
        CheckOverallPerformance(metrics);
        
        return metrics;
    }

    /// <summary>
    /// Calculate validation overhead metrics
    /// </summary>
    private ValidationOverheadMetrics CalculateValidationOverhead(TimeSpan totalDuration)
    {
        var validationTime = _categoryTimers["Validation"].Elapsed;
        var validationSteps = _startupDiagnostics.GetAllSteps()
            .Where(s => s.Component.Contains("Validator") || s.Name.Contains("Validation"))
            .ToList();

        var directoryValidationTime = validationSteps
            .Where(s => s.Name.Contains("Directory"))
            .Sum(s => s.Duration?.TotalMilliseconds ?? 0);
            
        var dependencyValidationTime = validationSteps
            .Where(s => s.Name.Contains("Dependency"))
            .Sum(s => s.Duration?.TotalMilliseconds ?? 0);
            
        var configValidationTime = validationSteps
            .Where(s => s.Name.Contains("Configuration"))
            .Sum(s => s.Duration?.TotalMilliseconds ?? 0);
            
        var serviceValidationTime = validationSteps
            .Where(s => s.Name.Contains("Service"))
            .Sum(s => s.Duration?.TotalMilliseconds ?? 0);
            
        var resourceValidationTime = validationSteps
            .Where(s => s.Name.Contains("Resource"))
            .Sum(s => s.Duration?.TotalMilliseconds ?? 0);

        return new ValidationOverheadMetrics
        {
            TotalOverhead = validationTime,
            OverheadPercentage = totalDuration.TotalMilliseconds > 0 
                ? (validationTime.TotalMilliseconds / totalDuration.TotalMilliseconds) * 100 
                : 0,
            DirectoryValidationTime = TimeSpan.FromMilliseconds(directoryValidationTime),
            DependencyValidationTime = TimeSpan.FromMilliseconds(dependencyValidationTime),
            ConfigurationValidationTime = TimeSpan.FromMilliseconds(configValidationTime),
            ServiceValidationTime = TimeSpan.FromMilliseconds(serviceValidationTime),
            ResourceValidationTime = TimeSpan.FromMilliseconds(resourceValidationTime),
            ValidationChecksPerformed = validationSteps.Count
        };
    }

    /// <summary>
    /// Check overall performance against thresholds
    /// </summary>
    private void CheckOverallPerformance(PerformanceMetrics metrics)
    {
        // Check total startup time
        if (metrics.Startup.TotalDuration > _thresholds.MaxAcceptableStartupTime)
        {
            _warnings.Add(new PerformanceWarning
            {
                Severity = PerformanceWarningSeverity.Critical,
                Category = PerformanceWarningCategory.StartupTime,
                Component = "Overall",
                Message = $"Startup time {metrics.Startup.TotalDuration.TotalMilliseconds:F2}ms exceeds threshold {_thresholds.MaxAcceptableStartupTime.TotalMilliseconds}ms",
                MeasuredValue = metrics.Startup.TotalDuration,
                Threshold = _thresholds.MaxAcceptableStartupTime,
                SuggestedAction = "Review startup sequence and optimize slow components"
            });
        }

        // Check validation overhead
        if (metrics.Validation.TotalOverhead > _thresholds.MaxAcceptableValidationOverhead)
        {
            _warnings.Add(new PerformanceWarning
            {
                Severity = PerformanceWarningSeverity.Warning,
                Category = PerformanceWarningCategory.ValidationOverhead,
                Component = "Validation",
                Message = $"Validation overhead {metrics.Validation.TotalOverhead.TotalMilliseconds:F2}ms exceeds threshold {_thresholds.MaxAcceptableValidationOverhead.TotalMilliseconds}ms",
                MeasuredValue = metrics.Validation.TotalOverhead,
                Threshold = _thresholds.MaxAcceptableValidationOverhead,
                SuggestedAction = "Consider optimizing validation logic or reducing validation scope"
            });
        }

        // Check memory usage
        if (metrics.Memory.PeakUsageMB > _thresholds.MaxAcceptableMemoryUsageMB)
        {
            _warnings.Add(new PerformanceWarning
            {
                Severity = PerformanceWarningSeverity.Warning,
                Category = PerformanceWarningCategory.MemoryUsage,
                Component = "Overall",
                Message = $"Peak memory usage {metrics.Memory.PeakUsageMB}MB exceeds threshold {_thresholds.MaxAcceptableMemoryUsageMB}MB",
                MeasuredValue = metrics.Memory.PeakUsageMB,
                Threshold = _thresholds.MaxAcceptableMemoryUsageMB,
                SuggestedAction = "Review memory allocation patterns and consider optimizations"
            });
        }
    }

    /// <summary>
    /// Export performance metrics to file
    /// </summary>
    public async Task ExportPerformanceMetricsAsync(string filePath)
    {
        try
        {
            var metrics = GetPerformanceMetrics();
            
            var exportData = new
            {
                ExportTime = DateTime.UtcNow,
                ApplicationVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                SystemInfo = new
                {
                    OS = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet / (1024 * 1024), // MB
                    Is64BitProcess = Environment.Is64BitProcess
                },
                PerformanceMetrics = metrics
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json);
            
            Debug.WriteLine($"Performance metrics exported to: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to export performance metrics: {ex.Message}");
            _errorHandler?.Handle(ex, "PerformanceMonitoringService.ExportPerformanceMetricsAsync");
        }
    }

    /// <summary>
    /// Log performance summary
    /// </summary>
    public void LogPerformanceSummary()
    {
        var metrics = GetPerformanceMetrics();
        
        Debug.WriteLine("=== Performance Monitoring Summary ===");
        Debug.WriteLine(metrics.GetSummary());
        
        // Log timing breakdown
        var timingBreakdown = metrics.Timing.GetPercentageBreakdown(metrics.Startup.TotalDuration);
        Debug.WriteLine("Timing Breakdown:");
        foreach (var kvp in timingBreakdown.OrderByDescending(x => x.Value))
        {
            Debug.WriteLine($"  {kvp.Key}: {kvp.Value:F1}%");
        }
        
        // Log validation overhead details
        Debug.WriteLine($"Validation Overhead Details:");
        Debug.WriteLine($"  Directory: {metrics.Validation.DirectoryValidationTime.TotalMilliseconds:F2}ms");
        Debug.WriteLine($"  Dependencies: {metrics.Validation.DependencyValidationTime.TotalMilliseconds:F2}ms");
        Debug.WriteLine($"  Configuration: {metrics.Validation.ConfigurationValidationTime.TotalMilliseconds:F2}ms");
        Debug.WriteLine($"  Services: {metrics.Validation.ServiceValidationTime.TotalMilliseconds:F2}ms");
        Debug.WriteLine($"  Resources: {metrics.Validation.ResourceValidationTime.TotalMilliseconds:F2}ms");
        
        // Log memory details
        Debug.WriteLine($"Memory Details:");
        Debug.WriteLine($"  Growth: {metrics.Memory.MemoryGrowthMB}MB");
        Debug.WriteLine($"  GC Collections: Gen0={metrics.Memory.GC.CollectionsByGeneration.GetValueOrDefault(0)}, Gen1={metrics.Memory.GC.CollectionsByGeneration.GetValueOrDefault(1)}, Gen2={metrics.Memory.GC.CollectionsByGeneration.GetValueOrDefault(2)}");
        Debug.WriteLine($"  GC Freed: {metrics.Memory.GC.MemoryFreed / (1024 * 1024)}MB");
    }

    /// <summary>
    /// Get performance warnings
    /// </summary>
    public IReadOnlyList<PerformanceWarning> GetWarnings()
    {
        return _warnings.AsReadOnly();
    }

    /// <summary>
    /// Clear performance warnings
    /// </summary>
    public void ClearWarnings()
    {
        _warnings.Clear();
    }

    /// <summary>
    /// Update performance thresholds
    /// </summary>
    public void UpdateThresholds(PerformanceThresholds newThresholds)
    {
        _thresholds.MaxAcceptableStartupTime = newThresholds.MaxAcceptableStartupTime;
        _thresholds.MaxAcceptableValidationOverhead = newThresholds.MaxAcceptableValidationOverhead;
        _thresholds.MaxAcceptableMemoryUsageMB = newThresholds.MaxAcceptableMemoryUsageMB;
        _thresholds.SlowStepThreshold = newThresholds.SlowStepThreshold;
        _thresholds.HighMemoryAllocationThresholdMB = newThresholds.HighMemoryAllocationThresholdMB;
    }

    public void Dispose()
    {
        // Log final performance summary
        LogPerformanceSummary();
        
        // Stop all timers
        foreach (var timer in _categoryTimers.Values)
        {
            timer.Stop();
        }
    }
}