using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Exception context for startup-specific logging
/// </summary>
public class StartupExceptionContext
{
    public string Component { get; set; } = "";
    public string Operation { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Phase { get; set; } = ""; // e.g., "ServiceRegistration", "ThemeLoading", "HotkeyRegistration"
}

/// <summary>
/// Centralized exception logger with dual logging to file and Windows Event Log
/// Extends ErrorHandler functionality for startup-specific scenarios
/// </summary>
public class ExceptionLogger : IExceptionLogger
{
    private readonly IErrorHandler _errorHandler;
    private readonly string _eventLogSource = "DevSticky";
    private readonly object _logLock = new();
    private readonly List<IDisposable> _trackedResources = new();

    public ExceptionLogger(IErrorHandler errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        EnsureEventLogSourceExists();
    }

    /// <summary>
    /// Log exception with startup-specific context and dual logging
    /// </summary>
    public void LogStartupException(Exception exception, StartupExceptionContext context)
    {
        if (exception == null) throw new ArgumentNullException(nameof(exception));
        if (context == null) throw new ArgumentNullException(nameof(context));

        try
        {
            // Log to file via ErrorHandler
            var contextString = FormatStartupContext(context);
            _errorHandler.Handle(exception, contextString);

            // Log to Windows Event Log
            LogToEventLog(exception, context);
        }
        catch (Exception logException)
        {
            // Fallback to Debug output if logging fails
            Debug.WriteLine($"Failed to log startup exception: {logException.Message}");
            Debug.WriteLine($"Original exception: {exception.Message}");
        }
    }

    /// <summary>
    /// Log exception asynchronously with startup context
    /// </summary>
    public async Task LogStartupExceptionAsync(Exception exception, StartupExceptionContext context)
    {
        if (exception == null) throw new ArgumentNullException(nameof(exception));
        if (context == null) throw new ArgumentNullException(nameof(context));

        try
        {
            // Log to file via ErrorHandler
            var contextString = FormatStartupContext(context);
            await _errorHandler.HandleAsync(exception, contextString);

            // Log to Windows Event Log (run on thread pool to avoid blocking)
            await Task.Run(() => LogToEventLog(exception, context));
        }
        catch (Exception logException)
        {
            // Fallback to Debug output if logging fails
            Debug.WriteLine($"Failed to log startup exception: {logException.Message}");
            Debug.WriteLine($"Original exception: {exception.Message}");
        }
    }

    /// <summary>
    /// Track a resource for cleanup on failure
    /// </summary>
    public void TrackResource(IDisposable resource)
    {
        if (resource == null) return;

        lock (_logLock)
        {
            _trackedResources.Add(resource);
        }
    }

    /// <summary>
    /// Clean up all tracked resources
    /// </summary>
    public void CleanupTrackedResources()
    {
        lock (_logLock)
        {
            foreach (var resource in _trackedResources)
            {
                try
                {
                    resource?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to dispose resource: {ex.Message}");
                }
            }
            _trackedResources.Clear();
        }
    }

    /// <summary>
    /// Execute operation with resource tracking and cleanup on failure
    /// </summary>
    public T ExecuteWithResourceTracking<T>(Func<T> operation, StartupExceptionContext context)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (context == null) throw new ArgumentNullException(nameof(context));

        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            // Log the exception
            LogStartupException(ex, context);
            
            // Clean up tracked resources
            CleanupTrackedResources();
            
            // Re-throw to allow caller to handle
            throw;
        }
    }

    /// <summary>
    /// Execute async operation with resource tracking and cleanup on failure
    /// </summary>
    public async Task<T> ExecuteWithResourceTrackingAsync<T>(Func<Task<T>> operation, StartupExceptionContext context)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (context == null) throw new ArgumentNullException(nameof(context));

        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            // Log the exception
            await LogStartupExceptionAsync(ex, context);
            
            // Clean up tracked resources
            CleanupTrackedResources();
            
            // Re-throw to allow caller to handle
            throw;
        }
    }

    /// <summary>
    /// Format startup context for logging
    /// </summary>
    private string FormatStartupContext(StartupExceptionContext context)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(context.Phase))
            parts.Add($"Phase:{context.Phase}");
            
        if (!string.IsNullOrEmpty(context.Component))
            parts.Add($"Component:{context.Component}");
            
        if (!string.IsNullOrEmpty(context.Operation))
            parts.Add($"Operation:{context.Operation}");

        var contextString = string.Join(".", parts);
        
        // Add parameters if any
        if (context.Parameters.Count > 0)
        {
            var paramStrings = new List<string>();
            foreach (var param in context.Parameters)
            {
                paramStrings.Add($"{param.Key}={param.Value}");
            }
            contextString += $" [{string.Join(", ", paramStrings)}]";
        }

        return contextString;
    }

    /// <summary>
    /// Log to Windows Event Log
    /// </summary>
    private void LogToEventLog(Exception exception, StartupExceptionContext context)
    {
        try
        {
            if (!EventLog.SourceExists(_eventLogSource))
                return; // Skip if source doesn't exist and we can't create it

            var eventType = DetermineEventLogType(exception);
            var message = FormatEventLogMessage(exception, context);

            EventLog.WriteEntry(_eventLogSource, message, eventType);
        }
        catch
        {
            // Silently fail if event log writing fails
            // This prevents cascading failures during startup
        }
    }

    /// <summary>
    /// Ensure Windows Event Log source exists
    /// </summary>
    private void EnsureEventLogSourceExists()
    {
        try
        {
            if (!EventLog.SourceExists(_eventLogSource))
            {
                EventLog.CreateEventSource(_eventLogSource, "Application");
            }
        }
        catch
        {
            // Silently fail if we can't create event source
            // This typically happens when running without admin privileges
        }
    }

    /// <summary>
    /// Determine appropriate event log entry type based on exception
    /// </summary>
    private EventLogEntryType DetermineEventLogType(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => EventLogEntryType.Error,
            StackOverflowException => EventLogEntryType.Error,
            UnauthorizedAccessException => EventLogEntryType.Error,
            FileNotFoundException => EventLogEntryType.Warning,
            DirectoryNotFoundException => EventLogEntryType.Warning,
            ArgumentException => EventLogEntryType.Warning,
            _ => EventLogEntryType.Error
        };
    }

    /// <summary>
    /// Format message for Windows Event Log
    /// </summary>
    private string FormatEventLogMessage(Exception exception, StartupExceptionContext context)
    {
        var message = $"DevSticky Startup Exception\n\n";
        message += $"Timestamp: {context.Timestamp:yyyy-MM-dd HH:mm:ss}\n";
        message += $"Phase: {context.Phase}\n";
        message += $"Component: {context.Component}\n";
        message += $"Operation: {context.Operation}\n\n";
        message += $"Exception Type: {exception.GetType().Name}\n";
        message += $"Message: {exception.Message}\n\n";
        
        if (context.Parameters.Count > 0)
        {
            message += "Parameters:\n";
            foreach (var param in context.Parameters)
            {
                message += $"  {param.Key}: {param.Value}\n";
            }
            message += "\n";
        }
        
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            message += $"Stack Trace:\n{exception.StackTrace}";
        }

        return message;
    }

    /// <summary>
    /// Dispose tracked resources
    /// </summary>
    public void Dispose()
    {
        CleanupTrackedResources();
    }
}