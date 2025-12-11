namespace DevSticky.Models;

/// <summary>
/// Comprehensive benchmark results containing all performance metrics.
/// </summary>
public class ComprehensiveBenchmarkResult
{
    public BenchmarkConfiguration TestConfiguration { get; set; } = new();
    public MemoryBenchmarkResult MemoryBenchmark { get; set; } = new();
    public SaveBenchmarkResult SaveBenchmark { get; set; } = new();
    public CacheBenchmarkResult CacheBenchmark { get; set; } = new();
    public LinqBenchmarkResult LinqBenchmark { get; set; } = new();

    /// <summary>
    /// Generates a comprehensive report of all benchmark results.
    /// </summary>
    public string GenerateReport()
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine("=== DevSticky Performance Benchmark Report ===");
        report.AppendLine($"Test Date: {TestConfiguration.TestDate:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"Machine: {TestConfiguration.MachineName}");
        report.AppendLine($"OS: {TestConfiguration.OperatingSystem}");
        report.AppendLine($"Processors: {TestConfiguration.ProcessorCount}");
        report.AppendLine($"Test Notes: {TestConfiguration.NoteCount}");
        report.AppendLine();

        // Memory Benchmark
        report.AppendLine("--- Memory Usage Benchmark ---");
        report.AppendLine($"Baseline Memory: {MemoryBenchmark.BaselineMemoryMB:F2} MB");
        report.AppendLine($"After Load Memory: {MemoryBenchmark.AfterLoadMemoryMB:F2} MB");
        report.AppendLine($"Peak Memory: {MemoryBenchmark.PeakMemoryMB:F2} MB");
        report.AppendLine($"Memory per Note: {MemoryBenchmark.MemoryPerNoteMB:F4} MB");
        report.AppendLine($"Target: <50MB for 100 notes - {(MemoryBenchmark.PeakMemoryMB < 50 ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine();

        // Save Performance Benchmark
        report.AppendLine("--- Save Performance Benchmark ---");
        report.AppendLine($"Average Save Time: {SaveBenchmark.AverageSaveTimeMs:F2} ms");
        report.AppendLine($"Min Save Time: {SaveBenchmark.MinSaveTimeMs:F2} ms");
        report.AppendLine($"Max Save Time: {SaveBenchmark.MaxSaveTimeMs:F2} ms");
        report.AppendLine($"Standard Deviation: {SaveBenchmark.SaveTimeStandardDeviation:F2} ms");
        if (SaveBenchmark.AverageIncrementalSaveTimeMs.HasValue)
        {
            report.AppendLine($"Incremental Save Time: {SaveBenchmark.AverageIncrementalSaveTimeMs:F2} ms");
            report.AppendLine($"Incremental Improvement: {SaveBenchmark.IncrementalSaveImprovement:F1}%");
        }
        report.AppendLine($"Target: <50ms - {(SaveBenchmark.AverageSaveTimeMs < 50 ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine();

        // Cache Performance Benchmark
        report.AppendLine("--- Cache Performance Benchmark ---");
        report.AppendLine($"Total Operations: {CacheBenchmark.TotalOperations:N0}");
        report.AppendLine($"Cache Hit Rate: {CacheBenchmark.CacheHitRate:F1}%");
        report.AppendLine($"Total Hits: {CacheBenchmark.TotalHits:N0}");
        report.AppendLine($"Total Misses: {CacheBenchmark.TotalMisses:N0}");
        report.AppendLine($"Average Operation Time: {CacheBenchmark.AverageOperationTimeMs:F4} ms");
        report.AppendLine($"Tag Cache Utilization: {CacheBenchmark.TagCacheUtilization:F1}%");
        report.AppendLine($"Group Cache Utilization: {CacheBenchmark.GroupCacheUtilization:F1}%");
        report.AppendLine($"Target: >90% hit rate - {(CacheBenchmark.CacheHitRate > 90 ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine();

        // LINQ Performance Benchmark
        report.AppendLine("--- LINQ Performance Benchmark ---");
        report.AppendLine($"Tag Search - Unoptimized: {LinqBenchmark.TagSearchUnoptimizedMs:F3} ms");
        report.AppendLine($"Tag Search - Optimized: {LinqBenchmark.TagSearchOptimizedMs:F3} ms");
        report.AppendLine($"Tag Search Improvement: {LinqBenchmark.TagSearchImprovement:F1}%");
        report.AppendLine();
        report.AppendLine($"Grouping - Unoptimized: {LinqBenchmark.GroupingUnoptimizedMs:F3} ms");
        report.AppendLine($"Grouping - Optimized: {LinqBenchmark.GroupingOptimizedMs:F3} ms");
        report.AppendLine($"Grouping Improvement: {LinqBenchmark.GroupingImprovement:F1}%");
        report.AppendLine();
        report.AppendLine($"Filtering - Unoptimized: {LinqBenchmark.FilteringUnoptimizedMs:F3} ms");
        report.AppendLine($"Filtering - Optimized: {LinqBenchmark.FilteringOptimizedMs:F3} ms");
        report.AppendLine($"Filtering Improvement: {LinqBenchmark.FilteringImprovement:F1}%");
        report.AppendLine();

        // Overall Assessment
        report.AppendLine("--- Overall Assessment ---");
        var memoryPass = MemoryBenchmark.PeakMemoryMB < 50;
        var savePass = SaveBenchmark.AverageSaveTimeMs < 50;
        var cachePass = CacheBenchmark.CacheHitRate > 90;
        var overallPass = memoryPass && savePass && cachePass;
        
        report.AppendLine($"Memory Target: {(memoryPass ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine($"Save Performance Target: {(savePass ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine($"Cache Performance Target: {(cachePass ? "✓ PASS" : "✗ FAIL")}");
        report.AppendLine($"Overall: {(overallPass ? "✓ ALL TARGETS MET" : "✗ SOME TARGETS MISSED")}");

        return report.ToString();
    }
}

/// <summary>
/// Configuration settings for benchmark tests.
/// </summary>
public class BenchmarkConfiguration
{
    public int NoteCount { get; set; }
    public DateTime TestDate { get; set; }
    public string MachineName { get; set; } = "";
    public int ProcessorCount { get; set; }
    public string OperatingSystem { get; set; } = "";
}

/// <summary>
/// Results of memory usage benchmarking.
/// </summary>
public class MemoryBenchmarkResult
{
    public double BaselineMemoryMB { get; set; }
    public double AfterLoadMemoryMB { get; set; }
    public double PeakMemoryMB { get; set; }
    public double MemoryPerNoteMB { get; set; }
    public List<MemoryDataPoint> MemoryProgression { get; set; } = new();
}

/// <summary>
/// Data point for memory usage at a specific note count.
/// </summary>
public class MemoryDataPoint
{
    public int NoteCount { get; set; }
    public double MemoryUsageMB { get; set; }
}

/// <summary>
/// Results of save operation performance benchmarking.
/// </summary>
public class SaveBenchmarkResult
{
    public double AverageSaveTimeMs { get; set; }
    public double MinSaveTimeMs { get; set; }
    public double MaxSaveTimeMs { get; set; }
    public double SaveTimeStandardDeviation { get; set; }
    public double? AverageIncrementalSaveTimeMs { get; set; }
    public double IncrementalSaveImprovement { get; set; }
    public List<SaveDataPoint> SaveProgression { get; set; } = new();
}

/// <summary>
/// Data point for save performance at a specific note count.
/// </summary>
public class SaveDataPoint
{
    public int NoteCount { get; set; }
    public double SaveTimeMs { get; set; }
}

/// <summary>
/// Results of cache performance benchmarking.
/// </summary>
public class CacheBenchmarkResult
{
    public int TotalOperations { get; set; }
    public double CacheHitRate { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double AverageOperationTimeMs { get; set; }
    public double TagCacheUtilization { get; set; }
    public double GroupCacheUtilization { get; set; }
}

/// <summary>
/// Results of LINQ query performance benchmarking.
/// </summary>
public class LinqBenchmarkResult
{
    public double TagSearchUnoptimizedMs { get; set; }
    public double TagSearchOptimizedMs { get; set; }
    public double TagSearchImprovement { get; set; }
    
    public double GroupingUnoptimizedMs { get; set; }
    public double GroupingOptimizedMs { get; set; }
    public double GroupingImprovement { get; set; }
    
    public double FilteringUnoptimizedMs { get; set; }
    public double FilteringOptimizedMs { get; set; }
    public double FilteringImprovement { get; set; }
}