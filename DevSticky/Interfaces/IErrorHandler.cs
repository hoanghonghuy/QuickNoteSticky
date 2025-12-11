using System;
using System.Threading.Tasks;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for centralized error handling with logging and fallback mechanisms
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Handle an exception synchronously with context
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="context">Context information about where the error occurred</param>
    void Handle(Exception exception, string context = "");
    
    /// <summary>
    /// Handle an exception asynchronously with context
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="context">Context information about where the error occurred</param>
    Task HandleAsync(Exception exception, string context = "");
    
    /// <summary>
    /// Execute an operation with error handling and fallback value
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="fallback">Fallback value if operation fails</param>
    /// <param name="context">Context information</param>
    /// <returns>Result of operation or fallback value</returns>
    T HandleWithFallback<T>(Func<T> operation, T fallback, string context = "");
    
    /// <summary>
    /// Execute an async operation with error handling and fallback value
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="fallback">Fallback value if operation fails</param>
    /// <param name="context">Context information</param>
    /// <returns>Result of operation or fallback value</returns>
    Task<T> HandleWithFallbackAsync<T>(Func<Task<T>> operation, T fallback, string context = "");
}
