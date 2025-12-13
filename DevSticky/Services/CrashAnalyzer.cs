using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Static class for analyzing application crashes and Windows Event Log entries
/// </summary>
public static class CrashAnalyzer
{
    private const string ApplicationName = "DevSticky";
    private const string EventLogSource = "Application";
    
    /// <summary>
    /// Analyze recent crash entries from Windows Event Log
    /// </summary>
    /// <param name="hoursBack">How many hours back to search for crashes</param>
    /// <returns>List of crash reports found in event log</returns>
    public static List<CrashReport> AnalyzeCrashesFromEventLog(int hoursBack = 24)
    {
        var crashes = new List<CrashReport>();
        var cutoffTime = DateTime.Now.AddHours(-hoursBack);
        
        try
        {
            using var eventLog = new EventLog(EventLogSource);
            
            // Search through recent event log entries
            for (int i = eventLog.Entries.Count - 1; i >= 0; i--)
            {
                var entry = eventLog.Entries[i];
                
                // Stop if we've gone back too far
                if (entry.TimeGenerated < cutoffTime)
                    break;
                
                // Look for error entries related to our application
                if (IsDevStickyCrashEntry(entry))
                {
                    var crashReport = CreateCrashReportFromEventLogEntry(entry);
                    if (crashReport != null)
                    {
                        crashes.Add(crashReport);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If we can't access event log, create a report about that
            var report = new CrashReport
            {
                Timestamp = DateTime.UtcNow,
                ExceptionType = "EventLogAccessException",
                Message = $"Could not access Windows Event Log: {ex.Message}",
                Component = "CrashAnalyzer",
                Context = { ["OriginalException"] = ex.GetType().FullName ?? "Unknown" }
            };
            crashes.Add(report);
        }
        
        return crashes.OrderByDescending(c => c.Timestamp).ToList();
    }
    
    /// <summary>
    /// Check if an event log entry is related to DevSticky crash
    /// </summary>
    private static bool IsDevStickyCrashEntry(EventLogEntry entry)
    {
        if (entry.EntryType != EventLogEntryType.Error)
            return false;
        
        var message = entry.Message?.ToLowerInvariant() ?? "";
        var source = entry.Source?.ToLowerInvariant() ?? "";
        
        // Look for DevSticky-related keywords
        return message.Contains("devsticky") || 
               source.Contains("devsticky") ||
               message.Contains(".net runtime") && message.Contains("devsticky.exe");
    }
    
    /// <summary>
    /// Create a crash report from an event log entry
    /// </summary>
    private static CrashReport? CreateCrashReportFromEventLogEntry(EventLogEntry entry)
    {
        try
        {
            var report = new CrashReport
            {
                Timestamp = entry.TimeGenerated,
                Component = "EventLog",
                Message = entry.Message ?? "No message available",
                Context = 
                {
                    ["EventId"] = entry.InstanceId,
                    ["Source"] = entry.Source ?? "Unknown",
                    ["Category"] = entry.Category ?? "Unknown",
                    ["EntryType"] = entry.EntryType.ToString()
                }
            };
            
            // Try to extract exception information from the message
            ExtractExceptionInfoFromMessage(entry.Message ?? "", report);
            
            return report;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Extract exception type and stack trace from event log message
    /// </summary>
    private static void ExtractExceptionInfoFromMessage(string message, CrashReport report)
    {
        try
        {
            // Look for common .NET exception patterns
            var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Look for exception type (usually contains "Exception")
                if (trimmedLine.Contains("Exception") && string.IsNullOrEmpty(report.ExceptionType))
                {
                    // Extract exception type from patterns like "System.ArgumentException:" or "Unhandled exception: System.NullReferenceException"
                    var exceptionMatch = System.Text.RegularExpressions.Regex.Match(
                        trimmedLine, 
                        @"(?:Unhandled exception:\s*)?([A-Za-z0-9\.]+Exception)");
                    
                    if (exceptionMatch.Success)
                    {
                        report.ExceptionType = exceptionMatch.Groups[1].Value;
                    }
                }
                
                // Look for stack trace indicators
                if (trimmedLine.StartsWith("at ") || trimmedLine.Contains("DevSticky."))
                {
                    if (string.IsNullOrEmpty(report.StackTrace))
                    {
                        report.StackTrace = message; // Use full message as stack trace
                    }
                    
                    // Extract component from stack trace
                    if (trimmedLine.Contains("DevSticky.") && string.IsNullOrEmpty(report.Component))
                    {
                        var componentMatch = System.Text.RegularExpressions.Regex.Match(
                            trimmedLine,
                            @"DevSticky\.([A-Za-z0-9]+)");
                        
                        if (componentMatch.Success)
                        {
                            report.Component = componentMatch.Groups[1].Value;
                        }
                    }
                }
            }
        }
        catch
        {
            // If extraction fails, we still have the basic report
        }
    }
    
    /// <summary>
    /// Analyze a crash report to identify the most likely cause
    /// </summary>
    public static string AnalyzeCrashCause(CrashReport crashReport)
    {
        var causes = new List<string>();
        
        // Analyze exception type
        switch (crashReport.ExceptionType.ToLowerInvariant())
        {
            case var type when type.Contains("nullreference"):
                causes.Add("Null reference - likely uninitialized object or missing dependency");
                break;
            case var type when type.Contains("argumentnull"):
                causes.Add("Argument null - missing dependency or service not properly injected");
                break;
            case var type when type.Contains("filenotfound"):
                causes.Add("Missing file - configuration, resource, or dependency file not found");
                break;
            case var type when type.Contains("directorynotfound"):
                causes.Add("Missing directory - application data directory may not exist");
                break;
            case var type when type.Contains("unauthorizedaccess"):
                causes.Add("Permission issue - insufficient rights to access file or directory");
                break;
            case var type when type.Contains("configuration"):
                causes.Add("Configuration error - invalid or corrupted configuration file");
                break;
            case var type when type.Contains("serialization") || type.Contains("json"):
                causes.Add("Data serialization error - corrupted or invalid JSON data");
                break;
            case var type when type.Contains("dependency") || type.Contains("assembly"):
                causes.Add("Dependency issue - missing or incompatible assembly/NuGet package");
                break;
        }
        
        // Analyze component
        switch (crashReport.Component.ToLowerInvariant())
        {
            case "app" or "application":
                causes.Add("Application startup failure - check DI container and service registration");
                break;
            case "storageservice":
                causes.Add("Storage system failure - check file permissions and disk space");
                break;
            case "themeservice":
                causes.Add("Theme system failure - check theme files and resource dictionaries");
                break;
            case "hotkeyservice":
                causes.Add("Hotkey system failure - check Win32 API access and hotkey conflicts");
                break;
            case "cloudsyncservice":
                causes.Add("Cloud sync failure - check network connectivity and authentication");
                break;
        }
        
        // Analyze message content
        var message = crashReport.Message.ToLowerInvariant();
        if (message.Contains("json"))
            causes.Add("JSON parsing error - check configuration file format");
        if (message.Contains("network") || message.Contains("connection"))
            causes.Add("Network connectivity issue");
        if (message.Contains("permission") || message.Contains("access"))
            causes.Add("File system permission issue");
        if (message.Contains("memory"))
            causes.Add("Memory-related issue - possible memory leak or insufficient memory");
        
        return causes.Any() 
            ? string.Join("; ", causes.Distinct())
            : "Unknown cause - requires detailed investigation";
    }
    
    /// <summary>
    /// Get suggested recovery actions for a crash
    /// </summary>
    public static List<string> GetSuggestedRecoveryActions(CrashReport crashReport)
    {
        var actions = new List<string>();
        
        var exceptionType = crashReport.ExceptionType.ToLowerInvariant();
        var message = crashReport.Message.ToLowerInvariant();
        
        // Exception-specific actions
        if (exceptionType.Contains("filenotfound") || message.Contains("file") && message.Contains("not found"))
        {
            actions.Add("Create missing configuration files with default values");
            actions.Add("Verify application installation integrity");
        }
        
        if (exceptionType.Contains("directorynotfound") || 
            exceptionType.Contains("directorynotfoundexception") ||
            crashReport.ExceptionType.Contains("DirectoryNotFoundException"))
        {
            actions.Add("Create missing directory structure for application data");
            actions.Add("Reset application data folder structure");
        }
        
        if (exceptionType.Contains("unauthorizedaccess") || message.Contains("permission"))
        {
            actions.Add("Run application as administrator");
            actions.Add("Check file and folder permissions");
            actions.Add("Verify antivirus is not blocking the application");
        }
        
        if (exceptionType.Contains("json") || message.Contains("json"))
        {
            actions.Add("Reset corrupted configuration files to defaults");
            actions.Add("Backup and recreate application settings");
        }
        
        if (exceptionType.Contains("dependency") || exceptionType.Contains("assembly"))
        {
            actions.Add("Reinstall application to restore missing dependencies");
            actions.Add("Update .NET runtime to latest version");
        }
        
        // Component-specific actions
        switch (crashReport.Component.ToLowerInvariant())
        {
            case "hotkeyservice":
                actions.Add("Reset hotkey configuration to defaults");
                actions.Add("Check for hotkey conflicts with other applications");
                break;
            case "cloudsyncservice":
                actions.Add("Disable cloud sync temporarily");
                actions.Add("Re-authenticate cloud storage provider");
                break;
            case "themeservice":
                actions.Add("Reset theme to default");
                actions.Add("Verify theme resource files are not corrupted");
                break;
        }
        
        // General recovery actions
        actions.Add("Start application in safe mode");
        actions.Add("Reset all configuration to factory defaults");
        
        return actions.Distinct().ToList();
    }
    
    /// <summary>
    /// Save crash report to file for later analysis
    /// </summary>
    public static async Task SaveCrashReportAsync(CrashReport crashReport, string? filePath = null)
    {
        try
        {
            filePath ??= GetDefaultCrashReportPath();
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Serialize crash report to JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(crashReport, options);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // If we can't save the crash report, there's not much we can do
            // This method should not throw exceptions
        }
    }
    
    /// <summary>
    /// Load crash reports from file
    /// </summary>
    public static async Task<List<CrashReport>> LoadCrashReportsAsync(string? filePath = null)
    {
        try
        {
            filePath ??= GetDefaultCrashReportPath();
            
            if (!File.Exists(filePath))
                return new List<CrashReport>();
            
            var json = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var reports = JsonSerializer.Deserialize<List<CrashReport>>(json, options);
            return reports ?? new List<CrashReport>();
        }
        catch
        {
            return new List<CrashReport>();
        }
    }
    
    /// <summary>
    /// Get default path for crash report file
    /// </summary>
    private static string GetDefaultCrashReportPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var devStickyPath = Path.Combine(appDataPath, "DevSticky");
        return Path.Combine(devStickyPath, "crash-reports.json");
    }
}