using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service to detect and analyze startup crashes immediately when the application starts
/// </summary>
public static class StartupCrashDetector
{
    private static readonly string CrashMarkerFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DevSticky",
        "startup-marker.txt");
    
    /// <summary>
    /// Check if the application crashed during the last startup attempt
    /// </summary>
    public static async Task<bool> CheckForPreviousCrashAsync()
    {
        try
        {
            // If marker file exists, it means the previous startup didn't complete successfully
            if (File.Exists(CrashMarkerFile))
            {
                var markerContent = await File.ReadAllTextAsync(CrashMarkerFile);
                var lines = markerContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length >= 2)
                {
                    var timestampStr = lines[0];
                    var component = lines[1];
                    
                    if (DateTime.TryParse(timestampStr, out var timestamp))
                    {
                        // If marker is older than 5 minutes, consider it a crash
                        if (DateTime.Now - timestamp > TimeSpan.FromMinutes(5))
                        {
                            Console.WriteLine($"[CRASH DETECTED] Previous startup failed at {timestamp} in component: {component}");
                            
                            // Analyze recent crashes from event log
                            var recentCrashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(1); // Last 1 hour
                            
                            if (recentCrashes.Any())
                            {
                                Console.WriteLine($"Found {recentCrashes.Count} recent crash(es) in Event Log:");
                                foreach (var crash in recentCrashes.Take(3)) // Show top 3
                                {
                                    Console.WriteLine($"  - {crash.Timestamp}: {crash.ExceptionType} in {crash.Component}");
                                    Console.WriteLine($"    Message: {crash.Message}");
                                    Console.WriteLine($"    Suggested cause: {CrashAnalyzer.AnalyzeCrashCause(crash)}");
                                    Console.WriteLine();
                                }
                            }
                            
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Could not check for previous crash: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Mark the start of a startup component
    /// </summary>
    public static async Task MarkStartupComponentAsync(string componentName)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(CrashMarkerFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var content = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{componentName}\n";
            await File.WriteAllTextAsync(CrashMarkerFile, content);
        }
        catch
        {
            // Don't throw exceptions from crash detection
        }
    }
    
    /// <summary>
    /// Mark successful startup completion
    /// </summary>
    public static void MarkStartupComplete()
    {
        try
        {
            if (File.Exists(CrashMarkerFile))
            {
                File.Delete(CrashMarkerFile);
            }
        }
        catch
        {
            // Don't throw exceptions from crash detection
        }
    }
    
    /// <summary>
    /// Get immediate crash analysis and suggestions
    /// </summary>
    public static async Task<string> GetImmediateCrashAnalysisAsync()
    {
        var analysis = new List<string>();
        
        try
        {
            analysis.Add("=== DevSticky Crash Analysis ===");
            analysis.Add($"Analysis Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            analysis.Add("");
            
            // Check for previous startup crash
            var hadPreviousCrash = await CheckForPreviousCrashAsync();
            if (hadPreviousCrash)
            {
                analysis.Add("⚠️  Previous startup crash detected!");
            }
            else
            {
                analysis.Add("✅ No previous startup crash detected");
            }
            analysis.Add("");
            
            // Analyze recent crashes from Event Log
            analysis.Add("--- Recent Crashes from Windows Event Log ---");
            var recentCrashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(24); // Last 24 hours
            
            if (recentCrashes.Any())
            {
                analysis.Add($"Found {recentCrashes.Count} crash(es) in the last 24 hours:");
                analysis.Add("");
                
                foreach (var crash in recentCrashes.Take(5)) // Show top 5
                {
                    analysis.Add($"🔴 Crash at {crash.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    analysis.Add($"   Exception: {crash.ExceptionType}");
                    analysis.Add($"   Component: {crash.Component}");
                    analysis.Add($"   Message: {crash.Message}");
                    analysis.Add($"   Likely Cause: {CrashAnalyzer.AnalyzeCrashCause(crash)}");
                    
                    var suggestions = CrashAnalyzer.GetSuggestedRecoveryActions(crash);
                    if (suggestions.Any())
                    {
                        analysis.Add("   Suggested Actions:");
                        foreach (var suggestion in suggestions.Take(3))
                        {
                            analysis.Add($"     • {suggestion}");
                        }
                    }
                    analysis.Add("");
                }
            }
            else
            {
                analysis.Add("✅ No crashes found in Windows Event Log (last 24 hours)");
            }
            
            analysis.Add("");
            analysis.Add("--- System Information ---");
            analysis.Add($"OS: {Environment.OSVersion}");
            analysis.Add($".NET Runtime: {Environment.Version}");
            analysis.Add($"Machine: {Environment.MachineName}");
            analysis.Add($"User: {Environment.UserName}");
            analysis.Add($"Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
            analysis.Add($"Processor Count: {Environment.ProcessorCount}");
            
            // Check basic file system access
            analysis.Add("");
            analysis.Add("--- Basic System Checks ---");
            
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var devStickyPath = Path.Combine(appDataPath, "DevSticky");
            
            if (Directory.Exists(devStickyPath))
            {
                analysis.Add($"✅ DevSticky data directory exists: {devStickyPath}");
                
                // Check if we can write to it
                try
                {
                    var testFile = Path.Combine(devStickyPath, "write-test.tmp");
                    await File.WriteAllTextAsync(testFile, "test");
                    File.Delete(testFile);
                    analysis.Add("✅ Can write to DevSticky data directory");
                }
                catch (Exception ex)
                {
                    analysis.Add($"❌ Cannot write to DevSticky data directory: {ex.Message}");
                }
            }
            else
            {
                analysis.Add($"⚠️  DevSticky data directory does not exist: {devStickyPath}");
            }
            
            // Check configuration files
            var configFiles = new[]
            {
                Path.Combine(devStickyPath, "notes.json"),
                Path.Combine(devStickyPath, "settings.json"),
                Path.Combine(devStickyPath, "snippets.json"),
                Path.Combine(devStickyPath, "templates.json")
            };
            
            foreach (var configFile in configFiles)
            {
                var fileName = Path.GetFileName(configFile);
                if (File.Exists(configFile))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(configFile);
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            analysis.Add($"⚠️  {fileName} exists but is empty");
                        }
                        else if (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
                        {
                            analysis.Add($"✅ {fileName} exists and appears to be valid JSON");
                        }
                        else
                        {
                            analysis.Add($"❌ {fileName} exists but does not appear to be valid JSON");
                        }
                    }
                    catch (Exception ex)
                    {
                        analysis.Add($"❌ Cannot read {fileName}: {ex.Message}");
                    }
                }
                else
                {
                    analysis.Add($"ℹ️  {fileName} does not exist (will be created on first run)");
                }
            }
            
        }
        catch (Exception ex)
        {
            analysis.Add($"❌ Error during crash analysis: {ex.Message}");
        }
        
        return string.Join(Environment.NewLine, analysis);
    }
}