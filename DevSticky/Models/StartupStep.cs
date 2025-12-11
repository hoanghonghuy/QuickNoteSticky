using System;
using System.Collections.Generic;

namespace DevSticky.Models;

/// <summary>
/// Represents a single step in the application startup process with timing and success tracking
/// </summary>
public class StartupStep
{
    /// <summary>
    /// Name of the startup step (e.g., "ServiceRegistration", "ThemeLoading", "HotkeyRegistration")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// When this step started
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When this step completed (null if still in progress or failed)
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Whether this step completed successfully
    /// </summary>
    public bool IsSuccessful { get; set; }
    
    /// <summary>
    /// Error message if the step failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Duration of the step (calculated from StartTime and EndTime)
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    
    /// <summary>
    /// Additional context information about this step
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
    
    /// <summary>
    /// Memory usage at the start of this step (in MB)
    /// </summary>
    public long StartMemoryMB { get; set; }
    
    /// <summary>
    /// Memory usage at the end of this step (in MB)
    /// </summary>
    public long? EndMemoryMB { get; set; }
    
    /// <summary>
    /// Memory delta for this step (calculated from StartMemoryMB and EndMemoryMB)
    /// </summary>
    public long? MemoryDeltaMB => EndMemoryMB.HasValue ? EndMemoryMB.Value - StartMemoryMB : null;
    
    /// <summary>
    /// Component or service associated with this step
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Phase of startup this step belongs to (e.g., "Initialization", "ServiceRegistration", "ResourceLoading")
    /// </summary>
    public string Phase { get; set; } = string.Empty;
    
    /// <summary>
    /// Create a new startup step and mark it as started
    /// </summary>
    public static StartupStep Start(string name, string component = "", string phase = "")
    {
        return new StartupStep
        {
            Name = name,
            Component = component,
            Phase = phase,
            StartTime = DateTime.UtcNow,
            StartMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
            IsSuccessful = false // Will be set to true when completed successfully
        };
    }
    
    /// <summary>
    /// Mark this step as completed successfully
    /// </summary>
    public void Complete()
    {
        EndTime = DateTime.UtcNow;
        EndMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        IsSuccessful = true;
        ErrorMessage = string.Empty;
    }
    
    /// <summary>
    /// Mark this step as failed with an error message
    /// </summary>
    public void Fail(string errorMessage)
    {
        EndTime = DateTime.UtcNow;
        EndMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        IsSuccessful = false;
        ErrorMessage = errorMessage;
    }
    
    /// <summary>
    /// Mark this step as failed with an exception
    /// </summary>
    public void Fail(Exception exception)
    {
        Fail(exception?.Message ?? "Unknown error");
        
        // Add exception details to context
        if (exception != null)
        {
            Context["ExceptionType"] = exception.GetType().FullName ?? "Unknown";
            Context["StackTrace"] = exception.StackTrace ?? "";
            
            if (exception.InnerException != null)
            {
                Context["InnerExceptionType"] = exception.InnerException.GetType().FullName ?? "Unknown";
                Context["InnerExceptionMessage"] = exception.InnerException.Message;
            }
        }
    }
    
    /// <summary>
    /// Add context information to this step
    /// </summary>
    public void AddContext(string key, object value)
    {
        Context[key] = value;
    }
    
    /// <summary>
    /// Convert startup step to a formatted string for logging
    /// </summary>
    public override string ToString()
    {
        var status = IsSuccessful ? "SUCCESS" : "FAILED";
        var duration = Duration?.TotalMilliseconds.ToString("F2") ?? "N/A";
        var memoryDelta = MemoryDeltaMB?.ToString("+#;-#;0") ?? "N/A";
        
        var result = $"[{StartTime:HH:mm:ss.fff}] {Phase}.{Component}.{Name} - {status}";
        
        if (Duration.HasValue)
        {
            result += $" ({duration}ms)";
        }
        
        if (MemoryDeltaMB.HasValue)
        {
            result += $" [Memory: {memoryDelta}MB]";
        }
        
        if (!IsSuccessful && !string.IsNullOrEmpty(ErrorMessage))
        {
            result += $" - {ErrorMessage}";
        }
        
        return result;
    }
    
    /// <summary>
    /// Get a detailed string representation including context
    /// </summary>
    public string ToDetailedString()
    {
        var result = ToString();
        
        if (Context.Count > 0)
        {
            result += "\n  Context: " + string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }
        
        return result;
    }
}