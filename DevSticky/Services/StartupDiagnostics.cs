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
/// Service for tracking and logging startup diagnostics with performance metrics
/// </summary>
public class StartupDiagnostics : IStartupDiagnostics
{
    private readonly List<StartupStep> _steps = new();
    private readonly object _stepsLock = new();
    private readonly IErrorHandler? _errorHandler;
    private DateTime? _startupBeginTime;
    private DateTime? _startupEndTime;

    public bool IsVerboseLoggingEnabled { get; set; }

    public StartupDiagnostics(IErrorHandler? errorHandler = null)
    {
        _errorHandler = errorHandler;
        IsVerboseLoggingEnabled = false;
    }

    /// <summary>
    /// Start tracking a new startup step
    /// </summary>
    public StartupStep StartStep(string stepName, string component = "", string phase = "")
    {
        if (string.IsNullOrWhiteSpace(stepName))
            throw new ArgumentException("Step name cannot be null or empty", nameof(stepName));

        var step = StartupStep.Start(stepName, component, phase);
        
        lock (_stepsLock)
        {
            _steps.Add(step);
            
            // Track overall startup timing
            if (_startupBeginTime == null)
            {
                _startupBeginTime = step.StartTime;
            }
        }

        if (IsVerboseLoggingEnabled)
        {
            LogVerbose($"Started: {step}");
        }

        return step;
    }

    /// <summary>
    /// Complete a startup step successfully
    /// </summary>
    public void CompleteStep(StartupStep step)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));

        step.Complete();
        
        lock (_stepsLock)
        {
            _startupEndTime = step.EndTime;
        }

        if (IsVerboseLoggingEnabled)
        {
            LogVerbose($"Completed: {step}");
        }
    }

    /// <summary>
    /// Mark a startup step as failed
    /// </summary>
    public void FailStep(StartupStep step, string errorMessage)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        step.Fail(errorMessage);
        
        lock (_stepsLock)
        {
            _startupEndTime = step.EndTime;
        }

        LogError($"Failed: {step}");
    }

    /// <summary>
    /// Mark a startup step as failed with exception
    /// </summary>
    public void FailStep(StartupStep step, Exception exception)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        if (exception == null) throw new ArgumentNullException(nameof(exception));

        step.Fail(exception);
        
        lock (_stepsLock)
        {
            _startupEndTime = step.EndTime;
        }

        LogError($"Failed: {step}");
        
        // Log exception details if error handler is available
        _errorHandler?.Handle(exception, $"StartupStep.{step.Phase}.{step.Component}.{step.Name}");
    }

    /// <summary>
    /// Execute an operation with automatic step tracking
    /// </summary>
    public T ExecuteStep<T>(string stepName, Func<T> operation, string component = "", string phase = "")
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var step = StartStep(stepName, component, phase);
        
        try
        {
            var result = operation();
            CompleteStep(step);
            return result;
        }
        catch (Exception ex)
        {
            FailStep(step, ex);
            throw;
        }
    }

    /// <summary>
    /// Execute an async operation with automatic step tracking
    /// </summary>
    public async Task<T> ExecuteStepAsync<T>(string stepName, Func<Task<T>> operation, string component = "", string phase = "")
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var step = StartStep(stepName, component, phase);
        
        try
        {
            var result = await operation();
            CompleteStep(step);
            return result;
        }
        catch (Exception ex)
        {
            FailStep(step, ex);
            throw;
        }
    }

    /// <summary>
    /// Execute an operation without return value with automatic step tracking
    /// </summary>
    public void ExecuteStep(string stepName, Action operation, string component = "", string phase = "")
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var step = StartStep(stepName, component, phase);
        
        try
        {
            operation();
            CompleteStep(step);
        }
        catch (Exception ex)
        {
            FailStep(step, ex);
            throw;
        }
    }

    /// <summary>
    /// Execute an async operation without return value with automatic step tracking
    /// </summary>
    public async Task ExecuteStepAsync(string stepName, Func<Task> operation, string component = "", string phase = "")
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var step = StartStep(stepName, component, phase);
        
        try
        {
            await operation();
            CompleteStep(step);
        }
        catch (Exception ex)
        {
            FailStep(step, ex);
            throw;
        }
    }

    /// <summary>
    /// Get all recorded startup steps
    /// </summary>
    public IReadOnlyList<StartupStep> GetAllSteps()
    {
        lock (_stepsLock)
        {
            return _steps.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Get startup steps for a specific phase
    /// </summary>
    public IReadOnlyList<StartupStep> GetStepsForPhase(string phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
            return new List<StartupStep>().AsReadOnly();

        lock (_stepsLock)
        {
            return _steps.Where(s => string.Equals(s.Phase, phase, StringComparison.OrdinalIgnoreCase))
                        .ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Get startup steps for a specific component
    /// </summary>
    public IReadOnlyList<StartupStep> GetStepsForComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
            return new List<StartupStep>().AsReadOnly();

        lock (_stepsLock)
        {
            return _steps.Where(s => string.Equals(s.Component, component, StringComparison.OrdinalIgnoreCase))
                        .ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Get failed startup steps
    /// </summary>
    public IReadOnlyList<StartupStep> GetFailedSteps()
    {
        lock (_stepsLock)
        {
            return _steps.Where(s => !s.IsSuccessful).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Get total startup duration
    /// </summary>
    public TimeSpan? GetTotalStartupDuration()
    {
        lock (_stepsLock)
        {
            if (_startupBeginTime == null || _startupEndTime == null)
                return null;
                
            return _startupEndTime.Value - _startupBeginTime.Value;
        }
    }

    /// <summary>
    /// Get startup performance metrics
    /// </summary>
    public StartupMetrics GetPerformanceMetrics()
    {
        lock (_stepsLock)
        {
            var metrics = new StartupMetrics
            {
                TotalDuration = GetTotalStartupDuration() ?? TimeSpan.Zero,
                TotalSteps = _steps.Count,
                SuccessfulSteps = _steps.Count(s => s.IsSuccessful),
                FailedSteps = _steps.Count(s => !s.IsSuccessful)
            };

            if (_steps.Count > 0)
            {
                // Calculate memory metrics
                var memoryDeltas = _steps.Where(s => s.MemoryDeltaMB.HasValue).Select(s => s.MemoryDeltaMB!.Value);
                metrics.TotalMemoryAllocatedMB = memoryDeltas.Where(d => d > 0).Sum();
                metrics.PeakMemoryUsageMB = _steps.Where(s => s.EndMemoryMB.HasValue).Max(s => s.EndMemoryMB!.Value);

                // Calculate timing metrics
                var completedSteps = _steps.Where(s => s.Duration.HasValue).ToList();
                if (completedSteps.Count > 0)
                {
                    var totalDuration = completedSteps.Sum(s => s.Duration!.Value.TotalMilliseconds);
                    metrics.AverageStepDuration = TimeSpan.FromMilliseconds(totalDuration / completedSteps.Count);
                    metrics.SlowestStep = completedSteps.OrderByDescending(s => s.Duration!.Value).First();
                }

                // Find step with highest memory allocation
                var memorySteps = _steps.Where(s => s.MemoryDeltaMB.HasValue && s.MemoryDeltaMB > 0).ToList();
                if (memorySteps.Count > 0)
                {
                    metrics.HighestMemoryStep = memorySteps.OrderByDescending(s => s.MemoryDeltaMB!.Value).First();
                }

                // Calculate phase breakdown
                var phaseGroups = _steps.Where(s => !string.IsNullOrEmpty(s.Phase))
                                       .GroupBy(s => s.Phase);
                
                foreach (var phaseGroup in phaseGroups)
                {
                    var phaseSteps = phaseGroup.ToList();
                    var phaseMetrics = new PhaseMetrics
                    {
                        PhaseName = phaseGroup.Key,
                        StepCount = phaseSteps.Count,
                        FailedSteps = phaseSteps.Count(s => !s.IsSuccessful),
                        Duration = TimeSpan.FromMilliseconds(
                            phaseSteps.Where(s => s.Duration.HasValue)
                                     .Sum(s => s.Duration!.Value.TotalMilliseconds)),
                        MemoryAllocatedMB = phaseSteps.Where(s => s.MemoryDeltaMB.HasValue && s.MemoryDeltaMB > 0)
                                                    .Sum(s => s.MemoryDeltaMB!.Value)
                    };
                    metrics.PhaseBreakdown[phaseGroup.Key] = phaseMetrics;
                }

                // Calculate component breakdown
                var componentGroups = _steps.Where(s => !string.IsNullOrEmpty(s.Component))
                                           .GroupBy(s => s.Component);
                
                foreach (var componentGroup in componentGroups)
                {
                    var componentSteps = componentGroup.ToList();
                    var componentMetrics = new ComponentMetrics
                    {
                        ComponentName = componentGroup.Key,
                        StepCount = componentSteps.Count,
                        FailedSteps = componentSteps.Count(s => !s.IsSuccessful),
                        Duration = TimeSpan.FromMilliseconds(
                            componentSteps.Where(s => s.Duration.HasValue)
                                         .Sum(s => s.Duration!.Value.TotalMilliseconds)),
                        MemoryAllocatedMB = componentSteps.Where(s => s.MemoryDeltaMB.HasValue && s.MemoryDeltaMB > 0)
                                                         .Sum(s => s.MemoryDeltaMB!.Value)
                    };
                    metrics.ComponentBreakdown[componentGroup.Key] = componentMetrics;
                }
            }

            return metrics;
        }
    }

    /// <summary>
    /// Log current startup progress
    /// </summary>
    public void LogCurrentProgress(bool includeContext = false)
    {
        lock (_stepsLock)
        {
            LogInfo($"Startup Progress: {_steps.Count(s => s.IsSuccessful)}/{_steps.Count} steps completed");
            
            if (includeContext)
            {
                foreach (var step in _steps.TakeLast(5)) // Show last 5 steps
                {
                    LogInfo($"  {(includeContext ? step.ToDetailedString() : step.ToString())}");
                }
            }
        }
    }

    /// <summary>
    /// Log startup summary after completion
    /// </summary>
    public void LogStartupSummary()
    {
        var metrics = GetPerformanceMetrics();
        
        LogInfo("=== Startup Summary ===");
        LogInfo($"Total Duration: {metrics.TotalDuration.TotalMilliseconds:F2}ms");
        LogInfo($"Steps: {metrics.SuccessfulSteps}/{metrics.TotalSteps} successful");
        LogInfo($"Memory Allocated: {metrics.TotalMemoryAllocatedMB}MB");
        LogInfo($"Peak Memory: {metrics.PeakMemoryUsageMB}MB");
        
        if (metrics.FailedSteps > 0)
        {
            LogError($"Failed Steps: {metrics.FailedSteps}");
            foreach (var failedStep in GetFailedSteps())
            {
                LogError($"  - {failedStep}");
            }
        }
        
        if (metrics.SlowestStep != null)
        {
            LogInfo($"Slowest Step: {metrics.SlowestStep.Name} ({metrics.SlowestStep.Duration?.TotalMilliseconds:F2}ms)");
        }
        
        // Log phase breakdown
        if (metrics.PhaseBreakdown.Count > 0)
        {
            LogInfo("Phase Breakdown:");
            foreach (var phase in metrics.PhaseBreakdown.Values.OrderByDescending(p => p.Duration))
            {
                LogInfo($"  {phase.PhaseName}: {phase.Duration.TotalMilliseconds:F2}ms ({phase.StepCount} steps)");
            }
        }
    }

    /// <summary>
    /// Export diagnostic information to a file
    /// </summary>
    public async Task ExportDiagnosticsAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        try
        {
            var diagnosticData = new
            {
                ExportTime = DateTime.UtcNow,
                Metrics = GetPerformanceMetrics(),
                Steps = GetAllSteps().Select(s => new
                {
                    s.Name,
                    s.Component,
                    s.Phase,
                    s.StartTime,
                    s.EndTime,
                    s.IsSuccessful,
                    s.ErrorMessage,
                    DurationMs = s.Duration?.TotalMilliseconds,
                    s.StartMemoryMB,
                    s.EndMemoryMB,
                    s.MemoryDeltaMB,
                    s.Context
                }).ToList()
            };

            var json = JsonSerializer.Serialize(diagnosticData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json);
            LogInfo($"Diagnostic information exported to: {filePath}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to export diagnostics: {ex.Message}");
            _errorHandler?.Handle(ex, "StartupDiagnostics.ExportDiagnosticsAsync");
        }
    }

    /// <summary>
    /// Clear all recorded steps (useful for testing)
    /// </summary>
    public void ClearSteps()
    {
        lock (_stepsLock)
        {
            _steps.Clear();
            _startupBeginTime = null;
            _startupEndTime = null;
        }
    }

    /// <summary>
    /// Log verbose message
    /// </summary>
    private void LogVerbose(string message)
    {
        if (IsVerboseLoggingEnabled)
        {
            Debug.WriteLine($"[STARTUP-VERBOSE] {message}");
        }
    }

    /// <summary>
    /// Log info message
    /// </summary>
    private void LogInfo(string message)
    {
        Debug.WriteLine($"[STARTUP-INFO] {message}");
    }

    /// <summary>
    /// Log error message
    /// </summary>
    private void LogError(string message)
    {
        Debug.WriteLine($"[STARTUP-ERROR] {message}");
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        // Log final summary if we have steps
        lock (_stepsLock)
        {
            if (_steps.Count > 0)
            {
                LogStartupSummary();
            }
        }
    }
}