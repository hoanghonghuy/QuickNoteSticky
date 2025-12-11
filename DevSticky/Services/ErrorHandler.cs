using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Error severity levels for categorizing errors
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Context information for error tracking
/// </summary>
public class ErrorContext
{
    public string Operation { get; set; } = "";
    public string Component { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Centralized error handler with logging and fallback mechanisms
/// </summary>
public class ErrorHandler : IErrorHandler
{
    private readonly string _logPath;
    private readonly object _logLock = new();

    public ErrorHandler()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var devStickyFolder = Path.Combine(appDataPath, AppConstants.AppDataFolderName);
        
        if (!Directory.Exists(devStickyFolder))
        {
            Directory.CreateDirectory(devStickyFolder);
        }
        
        _logPath = Path.Combine(devStickyFolder, "error.log");
    }

    /// <summary>
    /// Handle an exception synchronously with context
    /// </summary>
    public void Handle(Exception exception, string context = "")
    {
        var errorContext = CreateErrorContext(exception, context);
        LogError(errorContext, exception);
        
        // For critical errors, also write to Debug output
        if (errorContext.Severity == ErrorSeverity.Critical)
        {
            Debug.WriteLine($"CRITICAL ERROR: {context} - {exception.Message}");
        }
    }

    /// <summary>
    /// Handle an exception asynchronously with context
    /// </summary>
    public async Task HandleAsync(Exception exception, string context = "")
    {
        var errorContext = CreateErrorContext(exception, context);
        await LogErrorAsync(errorContext, exception);
        
        // For critical errors, also write to Debug output
        if (errorContext.Severity == ErrorSeverity.Critical)
        {
            Debug.WriteLine($"CRITICAL ERROR: {context} - {exception.Message}");
        }
    }

    /// <summary>
    /// Execute an operation with error handling and fallback value
    /// </summary>
    public T HandleWithFallback<T>(Func<T> operation, T fallback, string context = "")
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            Handle(ex, context);
            return fallback;
        }
    }

    /// <summary>
    /// Execute an async operation with error handling and fallback value
    /// </summary>
    public async Task<T> HandleWithFallbackAsync<T>(Func<Task<T>> operation, T fallback, string context = "")
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            await HandleAsync(ex, context);
            return fallback;
        }
    }

    /// <summary>
    /// Create error context from exception and context string
    /// </summary>
    private ErrorContext CreateErrorContext(Exception exception, string context)
    {
        var errorContext = new ErrorContext
        {
            Operation = context,
            Severity = DetermineSeverity(exception),
            Timestamp = DateTime.UtcNow
        };

        // Extract component from context if available
        if (!string.IsNullOrEmpty(context))
        {
            var parts = context.Split('.');
            if (parts.Length > 0)
            {
                errorContext.Component = parts[0];
            }
        }

        // Add exception metadata
        errorContext.Metadata["ExceptionType"] = exception.GetType().Name;
        errorContext.Metadata["Message"] = exception.Message;
        
        if (exception.InnerException != null)
        {
            errorContext.Metadata["InnerException"] = exception.InnerException.Message;
        }

        return errorContext;
    }

    /// <summary>
    /// Determine error severity based on exception type
    /// </summary>
    private ErrorSeverity DetermineSeverity(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => ErrorSeverity.Error,
            ArgumentException => ErrorSeverity.Warning,
            InvalidOperationException => ErrorSeverity.Error,
            UnauthorizedAccessException => ErrorSeverity.Error,
            IOException => ErrorSeverity.Warning,
            OutOfMemoryException => ErrorSeverity.Critical,
            StackOverflowException => ErrorSeverity.Critical,
            _ => ErrorSeverity.Error
        };
    }

    /// <summary>
    /// Log error to file synchronously
    /// </summary>
    private void LogError(ErrorContext context, Exception exception)
    {
        lock (_logLock)
        {
            try
            {
                var logEntry = FormatLogEntry(context, exception);
                File.AppendAllText(_logPath, logEntry);
                
                // Keep log file size manageable (max 5MB)
                TrimLogFileIfNeeded();
            }
            catch
            {
                // Silently fail if logging fails - don't throw from error handler
                Debug.WriteLine($"Failed to log error: {exception.Message}");
            }
        }
    }

    /// <summary>
    /// Log error to file asynchronously
    /// </summary>
    private async Task LogErrorAsync(ErrorContext context, Exception exception)
    {
        try
        {
            var logEntry = FormatLogEntry(context, exception);
            
            // Use lock for file access
            await Task.Run(() =>
            {
                lock (_logLock)
                {
                    File.AppendAllText(_logPath, logEntry);
                    TrimLogFileIfNeeded();
                }
            });
        }
        catch
        {
            // Silently fail if logging fails - don't throw from error handler
            Debug.WriteLine($"Failed to log error: {exception.Message}");
        }
    }

    /// <summary>
    /// Format log entry for writing to file
    /// </summary>
    private string FormatLogEntry(ErrorContext context, Exception exception)
    {
        var entry = $"[{context.Timestamp:yyyy-MM-dd HH:mm:ss}] [{context.Severity}] ";
        
        if (!string.IsNullOrEmpty(context.Component))
        {
            entry += $"[{context.Component}] ";
        }
        
        if (!string.IsNullOrEmpty(context.Operation))
        {
            entry += $"{context.Operation}: ";
        }
        
        entry += $"{exception.GetType().Name}: {exception.Message}";
        
        if (exception.StackTrace != null)
        {
            entry += $"\nStack Trace:\n{exception.StackTrace}";
        }
        
        if (exception.InnerException != null)
        {
            entry += $"\nInner Exception: {exception.InnerException.Message}";
        }
        
        entry += "\n---\n";
        
        return entry;
    }

    /// <summary>
    /// Trim log file if it exceeds 5MB
    /// </summary>
    private void TrimLogFileIfNeeded()
    {
        try
        {
            if (!File.Exists(_logPath))
                return;

            var fileInfo = new FileInfo(_logPath);
            const long maxSize = 5 * 1024 * 1024; // 5MB

            if (fileInfo.Length > maxSize)
            {
                // Keep only the last 50% of the file
                var lines = File.ReadAllLines(_logPath);
                var keepLines = lines.Length / 2;
                var trimmedLines = lines[^keepLines..];
                File.WriteAllLines(_logPath, trimmedLines);
            }
        }
        catch
        {
            // Silently fail if trimming fails
        }
    }
}
