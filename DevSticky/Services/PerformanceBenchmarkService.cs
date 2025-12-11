using System.Diagnostics;
using System.Runtime;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for comprehensive performance benchmarking of DevSticky operations.
/// Measures memory usage, save performance, cache hit rates, and LINQ query performance.
/// </summary>
public class PerformanceBenchmarkService
{
    private readonly IStorageService _storageService;
    private readonly ICacheService _cacheService;
    private readonly Process _currentProcess;

    public PerformanceBenchmarkService(IStorageService storageService, ICacheService cacheService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _currentProcess = Process.GetCurrentProcess();
    }

    /// <summary>
    /// Runs a comprehensive performance benchmark suite.
    /// </summary>
    /// <param name="noteCount">Number of notes to use for testing (default: 100)</param>
    /// <returns>Complete benchmark results</returns>
    public async Task<ComprehensiveBenchmarkResult> RunComprehensiveBenchmarkAsync(int noteCount = 100)
    {
        var result = new ComprehensiveBenchmarkResult
        {
            TestConfiguration = new BenchmarkConfiguration
            {
                NoteCount = noteCount,
                TestDate = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                OperatingSystem = Environment.OSVersion.ToString()
            }
        };

        // Measure baseline memory
        result.MemoryBenchmark = await BenchmarkMemoryUsageAsync(noteCount);
        
        // Measure save performance
        result.SaveBenchmark = await BenchmarkSavePerformanceAsync(noteCount);
        
        // Measure cache performance
        result.CacheBenchmark = BenchmarkCachePerformance();
        
        // Measure LINQ performance
        result.LinqBenchmark = BenchmarkLinqPerformance(noteCount);

        return result;
    }

    /// <summary>
    /// Benchmarks memory usage with different numbers of notes.
    /// </summary>
    public async Task<MemoryBenchmarkResult> BenchmarkMemoryUsageAsync(int maxNotes = 100)
    {
        var result = new MemoryBenchmarkResult();
        
        // Force garbage collection to get accurate baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var baselineMemory = GetCurrentMemoryUsage();
        result.BaselineMemoryMB = baselineMemory;

        // Create test data
        var testNotes = GenerateTestNotes(maxNotes);
        var testAppData = new AppData
        {
            Notes = testNotes,
            Groups = GenerateTestGroups(10),
            Tags = GenerateTestTags(20),
            AppSettings = new AppSettings()
        };

        // Measure memory after loading data
        var afterLoadMemory = GetCurrentMemoryUsage();
        result.AfterLoadMemoryMB = afterLoadMemory;
        result.MemoryPerNoteMB = (afterLoadMemory - baselineMemory) / maxNotes;

        // Test memory with different note counts
        var memoryProgression = new List<MemoryDataPoint>();
        for (int noteCount = 10; noteCount <= maxNotes; noteCount += 10)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var subsetNotes = testNotes.Take(noteCount).ToList();
            var subsetData = new AppData
            {
                Notes = subsetNotes,
                Groups = testAppData.Groups,
                Tags = testAppData.Tags,
                AppSettings = testAppData.AppSettings
            };

            var memoryUsage = GetCurrentMemoryUsage();
            memoryProgression.Add(new MemoryDataPoint
            {
                NoteCount = noteCount,
                MemoryUsageMB = memoryUsage
            });
        }

        result.MemoryProgression = memoryProgression;
        result.PeakMemoryMB = memoryProgression.Max(m => m.MemoryUsageMB);

        return result;
    }

    /// <summary>
    /// Benchmarks save operation performance.
    /// </summary>
    public async Task<SaveBenchmarkResult> BenchmarkSavePerformanceAsync(int noteCount = 100)
    {
        var result = new SaveBenchmarkResult();
        var testNotes = GenerateTestNotes(noteCount);
        var testAppData = new AppData
        {
            Notes = testNotes,
            Groups = GenerateTestGroups(10),
            Tags = GenerateTestTags(20),
            AppSettings = new AppSettings()
        };

        // Warm-up run
        await _storageService.SaveAsync(testAppData);

        // Benchmark full save operations
        var saveTimes = new List<TimeSpan>();
        const int iterations = 10;

        for (int i = 0; i < iterations; i++)
        {
            var saveTime = await PerformanceBenchmark.MeasureTimeAsync(async () =>
            {
                await _storageService.SaveAsync(testAppData);
            });
            saveTimes.Add(saveTime);
        }

        result.AverageSaveTimeMs = saveTimes.Average(t => t.TotalMilliseconds);
        result.MinSaveTimeMs = saveTimes.Min(t => t.TotalMilliseconds);
        result.MaxSaveTimeMs = saveTimes.Max(t => t.TotalMilliseconds);
        result.SaveTimeStandardDeviation = CalculateStandardDeviation(saveTimes.Select(t => t.TotalMilliseconds));

        // Benchmark incremental save if available
        if (_storageService is StorageService storageService)
        {
            var incrementalTimes = new List<TimeSpan>();
            var singleNote = testNotes.Take(1);

            for (int i = 0; i < iterations; i++)
            {
                var incrementalTime = await PerformanceBenchmark.MeasureTimeAsync(async () =>
                {
                    await storageService.SaveNotesAsync(singleNote, testAppData);
                });
                incrementalTimes.Add(incrementalTime);
            }

            result.AverageIncrementalSaveTimeMs = incrementalTimes.Average(t => t.TotalMilliseconds);
            result.IncrementalSaveImprovement = ((result.AverageSaveTimeMs - result.AverageIncrementalSaveTimeMs.Value) / result.AverageSaveTimeMs) * 100;
        }

        // Test save performance with different note counts
        var saveProgression = new List<SaveDataPoint>();
        for (int count = 10; count <= noteCount; count += 10)
        {
            var subsetData = new AppData
            {
                Notes = testNotes.Take(count).ToList(),
                Groups = testAppData.Groups,
                Tags = testAppData.Tags,
                AppSettings = testAppData.AppSettings
            };

            var time = await PerformanceBenchmark.MeasureTimeAsync(async () =>
            {
                await _storageService.SaveAsync(subsetData);
            });

            saveProgression.Add(new SaveDataPoint
            {
                NoteCount = count,
                SaveTimeMs = time.TotalMilliseconds
            });
        }

        result.SaveProgression = saveProgression;
        return result;
    }

    /// <summary>
    /// Benchmarks cache hit rates and performance.
    /// </summary>
    public CacheBenchmarkResult BenchmarkCachePerformance()
    {
        var result = new CacheBenchmarkResult();
        
        // Get initial statistics
        var initialStats = _cacheService.GetStatistics();
        
        // Generate test data
        var testTagIds = Enumerable.Range(1, 50).Select(_ => Guid.NewGuid()).ToList();
        var testGroupIds = Enumerable.Range(1, 25).Select(_ => Guid.NewGuid()).ToList();

        // Simulate cache operations
        const int operationCount = 1000;
        var random = new Random(42); // Fixed seed for reproducible results

        var cacheOperationTime = PerformanceBenchmark.MeasureTime(() =>
        {
            for (int i = 0; i < operationCount; i++)
            {
                // Randomly access tags and groups (some will hit, some will miss)
                var tagId = testTagIds[random.Next(testTagIds.Count)];
                var groupId = testGroupIds[random.Next(testGroupIds.Count)];
                
                _cacheService.GetTag(tagId);
                _cacheService.GetGroup(groupId);
                
                // Occasionally get multiple tags
                if (i % 10 == 0)
                {
                    var randomTagIds = testTagIds.Take(random.Next(1, 6));
                    _cacheService.GetTags(randomTagIds);
                }
            }
        });

        // Get final statistics
        var finalStats = _cacheService.GetStatistics();
        
        result.TotalOperations = operationCount * 2; // Tags + Groups
        result.CacheHitRate = finalStats.HitRate;
        result.TotalHits = finalStats.TotalHits - initialStats.TotalHits;
        result.TotalMisses = finalStats.TotalMisses - initialStats.TotalMisses;
        result.AverageOperationTimeMs = cacheOperationTime.TotalMilliseconds / result.TotalOperations;
        result.TagCacheUtilization = (double)finalStats.TagCacheSize / finalStats.TagCacheMaxSize * 100;
        result.GroupCacheUtilization = (double)finalStats.GroupCacheSize / finalStats.GroupCacheMaxSize * 100;

        return result;
    }

    /// <summary>
    /// Benchmarks LINQ query performance with optimized vs unoptimized approaches.
    /// </summary>
    public LinqBenchmarkResult BenchmarkLinqPerformance(int noteCount = 100)
    {
        var result = new LinqBenchmarkResult();
        var testNotes = GenerateTestNotes(noteCount);
        var testTags = GenerateTestTags(20);
        var testGroups = GenerateTestGroups(10);

        // Assign random tags and groups to notes
        var random = new Random(42);
        foreach (var note in testNotes)
        {
            note.TagIds = testTags.Take(random.Next(0, 4)).Select(t => t.Id).ToList();
            if (random.NextDouble() > 0.3) // 70% chance of having a group
            {
                note.GroupId = testGroups[random.Next(testGroups.Count)].Id;
            }
        }

        // Benchmark: Find notes by tag (unoptimized vs optimized)
        var targetTagId = testTags.First().Id;
        
        var unoptimizedTagSearch = PerformanceBenchmark.MeasureTime(() =>
        {
            // Unoptimized: Multiple LINQ operations
            var notesWithTag = testNotes
                .Where(n => n.TagIds != null)
                .Where(n => n.TagIds.Contains(targetTagId))
                .Select(n => n.Id)
                .ToList();
        });

        var optimizedTagSearch = PerformanceBenchmark.MeasureTime(() =>
        {
            // Optimized: Single pass with direct enumeration
            var notesWithTag = new List<Guid>();
            foreach (var note in testNotes)
            {
                if (note.TagIds?.Contains(targetTagId) == true)
                {
                    notesWithTag.Add(note.Id);
                }
            }
        });

        result.TagSearchUnoptimizedMs = unoptimizedTagSearch.TotalMilliseconds;
        result.TagSearchOptimizedMs = optimizedTagSearch.TotalMilliseconds;
        result.TagSearchImprovement = ((unoptimizedTagSearch.TotalMilliseconds - optimizedTagSearch.TotalMilliseconds) / unoptimizedTagSearch.TotalMilliseconds) * 100;

        // Benchmark: Group notes by group (unoptimized vs optimized)
        var unoptimizedGrouping = PerformanceBenchmark.MeasureTime(() =>
        {
            // Unoptimized: Multiple LINQ operations
            var groupedNotes = testNotes
                .Where(n => n.GroupId.HasValue)
                .GroupBy(n => n.GroupId)
                .ToDictionary(g => g.Key!.Value, g => g.Count());
        });

        var optimizedGrouping = PerformanceBenchmark.MeasureTime(() =>
        {
            // Optimized: Single pass with dictionary
            var groupedNotes = new Dictionary<Guid, int>();
            foreach (var note in testNotes)
            {
                if (note.GroupId.HasValue)
                {
                    groupedNotes.TryGetValue(note.GroupId.Value, out var count);
                    groupedNotes[note.GroupId.Value] = count + 1;
                }
            }
        });

        result.GroupingUnoptimizedMs = unoptimizedGrouping.TotalMilliseconds;
        result.GroupingOptimizedMs = optimizedGrouping.TotalMilliseconds;
        result.GroupingImprovement = ((unoptimizedGrouping.TotalMilliseconds - optimizedGrouping.TotalMilliseconds) / unoptimizedGrouping.TotalMilliseconds) * 100;

        // Benchmark: Complex filtering (unoptimized vs optimized)
        var unoptimizedFiltering = PerformanceBenchmark.MeasureTime(() =>
        {
            // Unoptimized: Multiple passes
            var recentNotes = testNotes.Where(n => n.ModifiedDate > DateTime.UtcNow.AddDays(-7));
            var pinnedNotes = testNotes.Where(n => n.IsPinned);
            var result = recentNotes.Union(pinnedNotes).Distinct().Count();
        });

        var optimizedFiltering = PerformanceBenchmark.MeasureTime(() =>
        {
            // Optimized: Single pass
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var uniqueNotes = new HashSet<Guid>();
            foreach (var note in testNotes)
            {
                if (note.ModifiedDate > cutoffDate || note.IsPinned)
                {
                    uniqueNotes.Add(note.Id);
                }
            }
            var result = uniqueNotes.Count;
        });

        result.FilteringUnoptimizedMs = unoptimizedFiltering.TotalMilliseconds;
        result.FilteringOptimizedMs = optimizedFiltering.TotalMilliseconds;
        result.FilteringImprovement = ((unoptimizedFiltering.TotalMilliseconds - optimizedFiltering.TotalMilliseconds) / unoptimizedFiltering.TotalMilliseconds) * 100;

        return result;
    }

    private double GetCurrentMemoryUsage()
    {
        _currentProcess.Refresh();
        return _currentProcess.WorkingSet64 / (1024.0 * 1024.0); // Convert to MB
    }

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        var average = valueList.Average();
        var sumOfSquaresOfDifferences = valueList.Select(val => (val - average) * (val - average)).Sum();
        return Math.Sqrt(sumOfSquaresOfDifferences / valueList.Count);
    }

    private static List<Note> GenerateTestNotes(int count)
    {
        var notes = new List<Note>();
        var random = new Random(42); // Fixed seed for reproducible results
        var baseDate = DateTime.UtcNow.AddDays(-30);

        for (int i = 0; i < count; i++)
        {
            var createdDate = baseDate.AddMinutes(random.Next(0, 43200)); // Random time within 30 days
            notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = $"Test Note {i + 1}",
                Content = GenerateRandomContent(random.Next(50, 500)),
                Language = "CSharp",
                IsPinned = random.NextDouble() > 0.8, // 20% chance of being pinned
                Opacity = 0.8 + (random.NextDouble() * 0.2), // 0.8 to 1.0
                WindowRect = new WindowRect
                {
                    Top = random.Next(0, 800),
                    Left = random.Next(0, 1200),
                    Width = 300 + random.Next(0, 200),
                    Height = 200 + random.Next(0, 300)
                },
                CreatedDate = createdDate,
                ModifiedDate = createdDate.AddMinutes(random.Next(0, 1440)) // Modified within 24 hours of creation
            });
        }

        return notes;
    }

    private static List<NoteTag> GenerateTestTags(int count)
    {
        var tags = new List<NoteTag>();
        var colors = new[] { "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#F5FF33", "#33FFF5" };
        
        for (int i = 0; i < count; i++)
        {
            tags.Add(new NoteTag
            {
                Id = Guid.NewGuid(),
                Name = $"Tag{i + 1}",
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
                Name = $"Group {i + 1}"
            });
        }

        return groups;
    }

    private static string GenerateRandomContent(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 \n";
        var random = new Random(42);
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}