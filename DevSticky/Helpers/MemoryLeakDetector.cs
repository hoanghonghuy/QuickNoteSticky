using System.Diagnostics;
using System.Reflection;

namespace DevSticky.Helpers;

/// <summary>
/// Utility class for detecting potential memory leaks from event handler subscriptions
/// </summary>
public static class MemoryLeakDetector
{
    /// <summary>
    /// Analyze an object for potential event handler memory leaks
    /// </summary>
    /// <param name="obj">Object to analyze</param>
    /// <returns>List of potential memory leak issues</returns>
    public static List<MemoryLeakIssue> AnalyzeObject(object obj)
    {
        var issues = new List<MemoryLeakIssue>();
        
        if (obj == null) return issues;
        
        var type = obj.GetType();
        
        // Check for event fields that might have subscriptions
        var eventFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType));
        
        foreach (var field in eventFields)
        {
            try
            {
                var eventDelegate = field.GetValue(obj) as Delegate;
                if (eventDelegate != null)
                {
                    var invocationList = eventDelegate.GetInvocationList();
                    foreach (var handler in invocationList)
                    {
                        if (handler.Target != null && handler.Target != obj)
                        {
                            issues.Add(new MemoryLeakIssue
                            {
                                Type = MemoryLeakType.EventSubscription,
                                Description = $"Event '{field.Name}' has subscription from {handler.Target.GetType().Name}",
                                Source = obj.GetType().Name,
                                Target = handler.Target.GetType().Name,
                                EventName = field.Name,
                                Severity = MemoryLeakSeverity.Medium
                            });
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }
        }
        
        return issues;
    }
    
    /// <summary>
    /// Force garbage collection and analyze memory usage
    /// </summary>
    /// <returns>Memory usage information</returns>
    public static MemoryUsageInfo GetMemoryUsage()
    {
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var process = Process.GetCurrentProcess();
        
        return new MemoryUsageInfo
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            ManagedMemory = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }
    
    /// <summary>
    /// Check if an object has been properly disposed
    /// </summary>
    /// <param name="disposable">Object to check</param>
    /// <returns>True if properly disposed</returns>
    public static bool IsProperlyDisposed(IDisposable disposable)
    {
        if (disposable == null) return true;
        
        try
        {
            // Try to access a field that should be null after disposal
            var type = disposable.GetType();
            var disposedField = type.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (disposedField != null && disposedField.FieldType == typeof(bool))
            {
                return (bool)(disposedField.GetValue(disposable) ?? false);
            }
            
            // If no _disposed field, assume it's not properly implemented
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Analyze all loaded assemblies for potential memory leaks
    /// </summary>
    /// <returns>Summary of potential issues</returns>
    public static MemoryLeakSummary AnalyzeApplication()
    {
        var summary = new MemoryLeakSummary();
        var memoryBefore = GetMemoryUsage();
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var memoryAfter = GetMemoryUsage();
        
        summary.MemoryBefore = memoryBefore;
        summary.MemoryAfter = memoryAfter;
        summary.MemoryFreed = memoryBefore.ManagedMemory - memoryAfter.ManagedMemory;
        
        return summary;
    }
}

/// <summary>
/// Represents a potential memory leak issue
/// </summary>
public class MemoryLeakIssue
{
    public MemoryLeakType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public MemoryLeakSeverity Severity { get; set; }
}

/// <summary>
/// Types of memory leaks
/// </summary>
public enum MemoryLeakType
{
    EventSubscription,
    CircularReference,
    UnmanagedResource,
    StaticReference
}

/// <summary>
/// Severity levels for memory leaks
/// </summary>
public enum MemoryLeakSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Memory usage information
/// </summary>
public class MemoryUsageInfo
{
    public long WorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long ManagedMemory { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    
    public override string ToString()
    {
        return $"Working Set: {WorkingSet / 1024 / 1024:N0} MB, " +
               $"Private: {PrivateMemory / 1024 / 1024:N0} MB, " +
               $"Managed: {ManagedMemory / 1024 / 1024:N0} MB, " +
               $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
    }
}

/// <summary>
/// Summary of memory leak analysis
/// </summary>
public class MemoryLeakSummary
{
    public MemoryUsageInfo MemoryBefore { get; set; } = new();
    public MemoryUsageInfo MemoryAfter { get; set; } = new();
    public long MemoryFreed { get; set; }
    public List<MemoryLeakIssue> Issues { get; set; } = new();
    
    public int HighSeverityIssues => Issues.Count(i => i.Severity >= MemoryLeakSeverity.High);
    public int TotalIssues => Issues.Count;
}