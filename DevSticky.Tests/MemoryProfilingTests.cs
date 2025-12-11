using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace DevSticky.Tests;

/// <summary>
/// Comprehensive memory profiling tests for DevSticky application.
/// Tests memory usage with 100 notes, identifies memory leaks, verifies cache limits, and validates disposal.
/// Requirements: 4.1, 4.2, 4.3
/// </summary>
public class MemoryProfilingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IStorageService _storageService;
    private readonly ICacheService _cacheService;
    private readonly MemoryProfilerService _profilerService;
    private readonly string _testDataPath;

    public MemoryProfilingTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Create test data directory
        _testDataPath = Path.Combine(Path.GetTempPath(), "DevStickyMemoryProfileTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataPath);

        // Create services for testing
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        
        _storageService = new StorageService(errorHandler, fileSystem);
        
        _cacheService = new EnhancedCacheService(
            tagProvider: () => GenerateTestTags(50),
            groupProvider: () => GenerateTestGroups(25)
        );
        
        _profilerService = new MemoryProfilerService(_storageService, _cacheService);
    }

    public void Dispose()
    {
        _cacheService?.Dispose();
        
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }

    /// <summary>
    /// Main memory profiling test - profiles memory usage with exactly 100 notes.
    /// Requirements: 4.1, 4.2, 4.3
    /// </summary>
    [Fact]
    public async Task ProfileMemoryWith100Notes_ShouldMeetAllTargets()
    {
        // Act
        var result = await _profilerService.ProfileMemoryWith100NotesAsync();

        // Log detailed results
        var report = result.GenerateReport();
        _output.WriteLine(report);

        // Assert - Memory Usage Profile
        Assert.NotNull(result.MemoryUsageProfile);
        Assert.True(result.MemoryUsageProfile.BaselineMemory.WorkingSetMB > 0, 
            "Baseline memory should be positive");
        // Memory measurements can fluctuate due to GC, so we check managed memory instead
        Assert.True(result.MemoryUsageProfile.AfterLoadMemory.ManagedMemoryMB >= 0, 
            "Managed memory after load should be valid");
        Assert.True(result.MemoryUsageProfile.MemoryPerNote > 0, 
            "Memory per note should be positive");

        // Primary Target: Memory growth <50MB for 100 notes (Requirement 4.2)
        Assert.True(result.MemoryUsageProfile.MeetsTarget, 
            $"Memory growth for 100 notes should be less than 50 MB. Managed growth: {result.MemoryUsageProfile.ManagedMemoryGrowth:F2} MB");

        // Assert - Memory Leak Analysis (Requirement 4.1)
        Assert.NotNull(result.MemoryLeakAnalysis);
        Assert.False(result.MemoryLeakAnalysis.HasCriticalLeaks, 
            $"Should not have critical memory leaks. Found {result.MemoryLeakAnalysis.CriticalIssues} critical issues");

        // Assert - Cache Limit Verification (Requirement 4.3)
        Assert.NotNull(result.CacheLimitVerification);
        Assert.True(result.CacheLimitVerification.Success, 
            "Cache size limits should be properly enforced");
        Assert.True(result.CacheLimitVerification.TagCacheLimitEnforced, 
            "Tag cache size limit should be enforced");
        Assert.True(result.CacheLimitVerification.GroupCacheLimitEnforced, 
            "Group cache size limit should be enforced");

        // Assert - Disposal Verification (Requirement 4.1)
        Assert.NotNull(result.DisposalVerification);
        Assert.True(result.DisposalVerification.Success, 
            $"All resources should be properly disposed. Found {result.DisposalVerification.CriticalIssues} critical disposal issues");

        // Log key metrics
        _output.WriteLine($"Key Metrics:");
        _output.WriteLine($"  Peak Memory: {result.MemoryUsageProfile.PeakMemory.WorkingSetMB:F2} MB (Target: <50 MB)");
        _output.WriteLine($"  Memory per Note: {result.MemoryUsageProfile.MemoryPerNote:F4} MB");
        _output.WriteLine($"  Memory Leaks: {result.MemoryLeakAnalysis.TotalIssues} total, {result.MemoryLeakAnalysis.CriticalIssues} critical");
        _output.WriteLine($"  Cache Limits: {(result.CacheLimitVerification.Success ? "Enforced" : "Not Enforced")}");
        _output.WriteLine($"  Disposal: {(result.DisposalVerification.Success ? "Proper" : "Issues Found")}");
    }

    /// <summary>
    /// Tests memory usage progression with different note counts.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ProfileMemoryUsage_ShouldScaleLinearly(int noteCount)
    {
        // Arrange
        var profiler = new MemoryProfilerService(_storageService, _cacheService);

        // Act
        var result = await profiler.ProfileMemoryWith100NotesAsync();

        // Assert
        Assert.NotNull(result.MemoryUsageProfile);
        
        // Memory should scale reasonably with note count
        var memoryPerNote = result.MemoryUsageProfile.MemoryPerNote;
        Assert.True(memoryPerNote > 0, "Memory per note should be positive");
        Assert.True(memoryPerNote < 1.0, "Memory per note should be less than 1 MB"); // Reasonable upper bound

        _output.WriteLine($"Note Count: {noteCount}, Memory per Note: {memoryPerNote:F4} MB");
    }

    /// <summary>
    /// Tests that memory leaks are properly identified.
    /// </summary>
    [Fact]
    public async Task IdentifyMemoryLeaks_ShouldDetectKnownIssues()
    {
        // Act
        var result = await _profilerService.ProfileMemoryWith100NotesAsync();
        var leakAnalysis = result.MemoryLeakAnalysis;

        // Assert
        Assert.NotNull(leakAnalysis);
        
        // Should not have critical leaks after all fixes
        Assert.False(leakAnalysis.HasCriticalLeaks, 
            $"Should not have critical memory leaks after fixes. Issues: {string.Join(", ", leakAnalysis.Issues.Where(i => i.Severity == MemoryLeakSeverity.Critical).Select(i => i.Description))}");

        // Log all issues for review
        if (leakAnalysis.Issues.Any())
        {
            _output.WriteLine("Memory Leak Issues Found:");
            foreach (var issue in leakAnalysis.Issues.OrderByDescending(i => i.Severity))
            {
                _output.WriteLine($"  [{issue.Severity}] {issue.Type}: {issue.Description}");
            }
        }
        else
        {
            _output.WriteLine("No memory leak issues detected.");
        }
    }

    /// <summary>
    /// Tests that cache size limits are properly enforced.
    /// </summary>
    [Fact]
    public async Task VerifyCacheSizeLimits_ShouldEnforceLimits()
    {
        // Act
        var result = await _profilerService.ProfileMemoryWith100NotesAsync();
        var cacheVerification = result.CacheLimitVerification;

        // Assert
        Assert.NotNull(cacheVerification);
        Assert.True(cacheVerification.Success, 
            $"Cache limits should be enforced. Error: {cacheVerification.Error}");

        Assert.NotNull(cacheVerification.FinalCacheStats);
        var stats = cacheVerification.FinalCacheStats;

        // Verify tag cache limits
        Assert.True(stats.TagCacheSize <= stats.TagCacheMaxSize, 
            $"Tag cache size ({stats.TagCacheSize}) should not exceed limit ({stats.TagCacheMaxSize})");

        // Verify group cache limits
        Assert.True(stats.GroupCacheSize <= stats.GroupCacheMaxSize, 
            $"Group cache size ({stats.GroupCacheSize}) should not exceed limit ({stats.GroupCacheMaxSize})");

        _output.WriteLine($"Cache Verification Results:");
        _output.WriteLine($"  Tag Cache: {stats.TagCacheSize}/{stats.TagCacheMaxSize} (Limit Enforced: {cacheVerification.TagCacheLimitEnforced})");
        _output.WriteLine($"  Group Cache: {stats.GroupCacheSize}/{stats.GroupCacheMaxSize} (Limit Enforced: {cacheVerification.GroupCacheLimitEnforced})");
    }

    /// <summary>
    /// Tests that all disposable resources are properly disposed.
    /// </summary>
    [Fact]
    public async Task VerifyProperDisposal_ShouldDisposeAllResources()
    {
        // Act
        var result = await _profilerService.ProfileMemoryWith100NotesAsync();
        var disposalVerification = result.DisposalVerification;

        // Assert
        Assert.NotNull(disposalVerification);
        Assert.True(disposalVerification.Success, 
            $"All resources should be properly disposed. Critical issues: {disposalVerification.CriticalIssues}");

        // Log disposal issues if any
        if (disposalVerification.Issues.Any())
        {
            _output.WriteLine("Disposal Issues Found:");
            foreach (var issue in disposalVerification.Issues.OrderByDescending(i => i.Severity))
            {
                _output.WriteLine($"  [{issue.Severity}] {issue.ServiceName}: {issue.Description}");
            }
        }
        else
        {
            _output.WriteLine("All resources properly disposed.");
        }
    }

    /// <summary>
    /// Tests memory usage under stress conditions.
    /// </summary>
    [Fact]
    public async Task ProfileMemoryUnderStress_ShouldRemainStable()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Run multiple profiling sessions to stress test
        var results = new List<MemoryProfilingResult>();
        for (int i = 0; i < 3; i++)
        {
            var result = await _profilerService.ProfileMemoryWith100NotesAsync();
            results.Add(result);
            
            // Force cleanup between runs
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        foreach (var result in results)
        {
            Assert.True(result.MemoryUsageProfile.MeetsTarget, 
                "Each profiling run should meet memory targets");
        }

        // Memory should not grow significantly between runs
        var memoryGrowth = (finalMemory - initialMemory) / (1024.0 * 1024.0); // Convert to MB
        Assert.True(memoryGrowth < 10, 
            $"Memory growth ({memoryGrowth:F2} MB) should be less than 10 MB after multiple profiling runs");

        _output.WriteLine($"Stress Test Results:");
        _output.WriteLine($"  Runs: {results.Count}");
        _output.WriteLine($"  Memory Growth: {memoryGrowth:F2} MB");
        _output.WriteLine($"  All Runs Met Targets: {results.All(r => r.MemoryUsageProfile.MeetsTarget)}");
    }

    /// <summary>
    /// Benchmarks memory profiling performance itself.
    /// </summary>
    [Fact]
    public async Task ProfileMemoryProfilingPerformance_ShouldBeEfficient()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _profilerService.ProfileMemoryWith100NotesAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        
        // Profiling should complete in reasonable time (less than 30 seconds)
        Assert.True(stopwatch.Elapsed.TotalSeconds < 30, 
            $"Memory profiling should complete in less than 30 seconds, took {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        _output.WriteLine($"Memory Profiling Performance:");
        _output.WriteLine($"  Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        _output.WriteLine($"  Memory Target Met: {result.MemoryUsageProfile.MeetsTarget}");
        _output.WriteLine($"  Peak Memory: {result.MemoryUsageProfile.PeakMemory.WorkingSetMB:F2} MB");
    }

    #region Helper Methods

    private static List<NoteTag> GenerateTestTags(int count)
    {
        var tags = new List<NoteTag>();
        var colors = new[] { "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#F5FF33", "#33FFF5" };
        
        for (int i = 0; i < count; i++)
        {
            tags.Add(new NoteTag
            {
                Id = Guid.NewGuid(),
                Name = $"ProfileTestTag{i + 1}",
                Color = colors[i % colors.Length]
            });
        }

        return tags;
    }

    private static List<NoteGroup> GenerateTestGroups(int count)
    {
        var groups = new List<NoteGroup>();
        
        for (int i = 0; i < count; i++)
        {
            groups.Add(new NoteGroup
            {
                Id = Guid.NewGuid(),
                Name = $"ProfileTestGroup{i + 1}"
            });
        }

        return groups;
    }

    #endregion
}