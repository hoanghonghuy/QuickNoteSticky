using System;
using System.Threading.Tasks;
using DevSticky.Services;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for centralized exception logging with dual logging support
/// </summary>
public interface IExceptionLogger : IDisposable
{
    /// <summary>
    /// Log exception with startup-specific context and dual logging
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="context">Startup-specific context information</param>
    void LogStartupException(Exception exception, StartupExceptionContext context);
    
    /// <summary>
    /// Log exception asynchronously with startup context
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="context">Startup-specific context information</param>
    Task LogStartupExceptionAsync(Exception exception, StartupExceptionContext context);
    
    /// <summary>
    /// Track a resource for cleanup on failure
    /// </summary>
    /// <param name="resource">Resource to track for cleanup</param>
    void TrackResource(IDisposable resource);
    
    /// <summary>
    /// Clean up all tracked resources
    /// </summary>
    void CleanupTrackedResources();
    
    /// <summary>
    /// Execute operation with resource tracking and cleanup on failure
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="context">Context for logging if operation fails</param>
    /// <returns>Result of operation</returns>
    T ExecuteWithResourceTracking<T>(Func<T> operation, StartupExceptionContext context);
    
    /// <summary>
    /// Execute async operation with resource tracking and cleanup on failure
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">Async operation to execute</param>
    /// <param name="context">Context for logging if operation fails</param>
    /// <returns>Result of operation</returns>
    Task<T> ExecuteWithResourceTrackingAsync<T>(Func<Task<T>> operation, StartupExceptionContext context);
}