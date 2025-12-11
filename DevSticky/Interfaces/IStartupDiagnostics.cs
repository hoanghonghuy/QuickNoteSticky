using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for tracking and logging startup diagnostics with performance metrics
/// </summary>
public interface IStartupDiagnostics : IDisposable
{
    /// <summary>
    /// Whether verbose diagnostic logging is enabled
    /// </summary>
    bool IsVerboseLoggingEnabled { get; set; }
    
    /// <summary>
    /// Start tracking a new startup step
    /// </summary>
    /// <param name="stepName">Name of the step</param>
    /// <param name="component">Component or service name</param>
    /// <param name="phase">Startup phase</param>
    /// <returns>The started startup step</returns>
    StartupStep StartStep(string stepName, string component = "", string phase = "");
    
    /// <summary>
    /// Complete a startup step successfully
    /// </summary>
    /// <param name="step">The step to complete</param>
    void CompleteStep(StartupStep step);
    
    /// <summary>
    /// Mark a startup step as failed
    /// </summary>
    /// <param name="step">The step that failed</param>
    /// <param name="errorMessage">Error message</param>
    void FailStep(StartupStep step, string errorMessage);
    
    /// <summary>
    /// Mark a startup step as failed with exception
    /// </summary>
    /// <param name="step">The step that failed</param>
    /// <param name="exception">The exception that caused the failure</param>
    void FailStep(StartupStep step, Exception exception);
    
    /// <summary>
    /// Execute an operation with automatic step tracking
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="stepName">Name of the step</param>
    /// <param name="operation">Operation to execute</param>
    /// <param name="component">Component name</param>
    /// <param name="phase">Startup phase</param>
    /// <returns>Result of the operation</returns>
    T ExecuteStep<T>(string stepName, Func<T> operation, string component = "", string phase = "");
    
    /// <summary>
    /// Execute an async operation with automatic step tracking
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="stepName">Name of the step</param>
    /// <param name="operation">Async operation to execute</param>
    /// <param name="component">Component name</param>
    /// <param name="phase">Startup phase</param>
    /// <returns>Result of the operation</returns>
    Task<T> ExecuteStepAsync<T>(string stepName, Func<Task<T>> operation, string component = "", string phase = "");
    
    /// <summary>
    /// Execute an operation without return value with automatic step tracking
    /// </summary>
    /// <param name="stepName">Name of the step</param>
    /// <param name="operation">Operation to execute</param>
    /// <param name="component">Component name</param>
    /// <param name="phase">Startup phase</param>
    void ExecuteStep(string stepName, Action operation, string component = "", string phase = "");
    
    /// <summary>
    /// Execute an async operation without return value with automatic step tracking
    /// </summary>
    /// <param name="stepName">Name of the step</param>
    /// <param name="operation">Async operation to execute</param>
    /// <param name="component">Component name</param>
    /// <param name="phase">Startup phase</param>
    Task ExecuteStepAsync(string stepName, Func<Task> operation, string component = "", string phase = "");
    
    /// <summary>
    /// Get all recorded startup steps
    /// </summary>
    /// <returns>List of all startup steps</returns>
    IReadOnlyList<StartupStep> GetAllSteps();
    
    /// <summary>
    /// Get startup steps for a specific phase
    /// </summary>
    /// <param name="phase">Phase name</param>
    /// <returns>List of steps in the specified phase</returns>
    IReadOnlyList<StartupStep> GetStepsForPhase(string phase);
    
    /// <summary>
    /// Get startup steps for a specific component
    /// </summary>
    /// <param name="component">Component name</param>
    /// <returns>List of steps for the specified component</returns>
    IReadOnlyList<StartupStep> GetStepsForComponent(string component);
    
    /// <summary>
    /// Get failed startup steps
    /// </summary>
    /// <returns>List of failed steps</returns>
    IReadOnlyList<StartupStep> GetFailedSteps();
    
    /// <summary>
    /// Get total startup duration
    /// </summary>
    /// <returns>Total time from first step start to last step completion</returns>
    TimeSpan? GetTotalStartupDuration();
    
    /// <summary>
    /// Get startup performance metrics
    /// </summary>
    /// <returns>Performance metrics summary</returns>
    StartupMetrics GetPerformanceMetrics();
    
    /// <summary>
    /// Log current startup progress
    /// </summary>
    /// <param name="includeContext">Whether to include detailed context information</param>
    void LogCurrentProgress(bool includeContext = false);
    
    /// <summary>
    /// Log startup summary after completion
    /// </summary>
    void LogStartupSummary();
    
    /// <summary>
    /// Export diagnostic information to a file
    /// </summary>
    /// <param name="filePath">Path to export file</param>
    Task ExportDiagnosticsAsync(string filePath);
    
    /// <summary>
    /// Clear all recorded steps (useful for testing)
    /// </summary>
    void ClearSteps();
}

/// <summary>
/// Performance metrics for startup process
/// </summary>
public class StartupMetrics
{
    /// <summary>
    /// Total startup duration
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
    
    /// <summary>
    /// Number of steps executed
    /// </summary>
    public int TotalSteps { get; set; }
    
    /// <summary>
    /// Number of successful steps
    /// </summary>
    public int SuccessfulSteps { get; set; }
    
    /// <summary>
    /// Number of failed steps
    /// </summary>
    public int FailedSteps { get; set; }
    
    /// <summary>
    /// Total memory allocated during startup (in MB)
    /// </summary>
    public long TotalMemoryAllocatedMB { get; set; }
    
    /// <summary>
    /// Peak memory usage during startup (in MB)
    /// </summary>
    public long PeakMemoryUsageMB { get; set; }
    
    /// <summary>
    /// Average step duration
    /// </summary>
    public TimeSpan AverageStepDuration { get; set; }
    
    /// <summary>
    /// Slowest step
    /// </summary>
    public StartupStep? SlowestStep { get; set; }
    
    /// <summary>
    /// Step with highest memory allocation
    /// </summary>
    public StartupStep? HighestMemoryStep { get; set; }
    
    /// <summary>
    /// Breakdown by phase
    /// </summary>
    public Dictionary<string, PhaseMetrics> PhaseBreakdown { get; set; } = new();
    
    /// <summary>
    /// Breakdown by component
    /// </summary>
    public Dictionary<string, ComponentMetrics> ComponentBreakdown { get; set; } = new();
}

/// <summary>
/// Performance metrics for a startup phase
/// </summary>
public class PhaseMetrics
{
    public string PhaseName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int StepCount { get; set; }
    public int FailedSteps { get; set; }
    public long MemoryAllocatedMB { get; set; }
}

/// <summary>
/// Performance metrics for a component
/// </summary>
public class ComponentMetrics
{
    public string ComponentName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int StepCount { get; set; }
    public int FailedSteps { get; set; }
    public long MemoryAllocatedMB { get; set; }
}