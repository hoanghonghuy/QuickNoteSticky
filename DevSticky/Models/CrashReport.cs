using System;
using System.Collections.Generic;

namespace DevSticky.Models;

/// <summary>
/// Represents a crash report containing detailed information about an application crash
/// </summary>
public class CrashReport
{
    /// <summary>
    /// When the crash occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Type of the exception that caused the crash
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Exception message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Complete stack trace of the exception
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;
    
    /// <summary>
    /// Component or module where the crash occurred
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional context information about the crash
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
    
    /// <summary>
    /// Application version when the crash occurred
    /// </summary>
    public string ApplicationVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Operating system information
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;
    
    /// <summary>
    /// .NET runtime version
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Memory usage at the time of crash (in MB)
    /// </summary>
    public long MemoryUsageMB { get; set; }
    
    /// <summary>
    /// Whether this crash was recovered from
    /// </summary>
    public bool WasRecovered { get; set; }
    
    /// <summary>
    /// Recovery actions taken (if any)
    /// </summary>
    public List<string> RecoveryActions { get; set; } = new();
    
    /// <summary>
    /// Create a crash report from an exception
    /// </summary>
    public static CrashReport FromException(Exception exception, string component = "Unknown")
    {
        var report = new CrashReport
        {
            Timestamp = DateTime.UtcNow,
            ExceptionType = exception.GetType().FullName ?? "Unknown",
            Message = exception.Message,
            StackTrace = exception.ToString(),
            Component = component,
            ApplicationVersion = GetApplicationVersion(),
            OperatingSystem = Environment.OSVersion.ToString(),
            RuntimeVersion = Environment.Version.ToString(),
            MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
        };
        
        // Add inner exception information if present
        if (exception.InnerException != null)
        {
            report.Context["InnerExceptionType"] = exception.InnerException.GetType().FullName ?? "Unknown";
            report.Context["InnerExceptionMessage"] = exception.InnerException.Message;
        }
        
        // Add additional context
        report.Context["MachineName"] = Environment.MachineName;
        report.Context["UserName"] = Environment.UserName;
        report.Context["ProcessorCount"] = Environment.ProcessorCount;
        report.Context["WorkingSet"] = Environment.WorkingSet;
        
        return report;
    }
    
    /// <summary>
    /// Get the application version
    /// </summary>
    private static string GetApplicationVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
    
    /// <summary>
    /// Convert crash report to a formatted string for logging
    /// </summary>
    public override string ToString()
    {
        var contextStr = string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] CRASH in {Component}: {ExceptionType} - {Message}\n" +
               $"Version: {ApplicationVersion}, OS: {OperatingSystem}, Runtime: {RuntimeVersion}\n" +
               $"Memory: {MemoryUsageMB}MB, Recovered: {WasRecovered}\n" +
               $"Context: {contextStr}\n" +
               $"Stack Trace:\n{StackTrace}";
    }
}