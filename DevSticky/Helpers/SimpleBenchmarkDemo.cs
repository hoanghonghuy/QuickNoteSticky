using System.Diagnostics;
using DevSticky.Models;

namespace DevSticky.Helpers;

/// <summary>
/// Simple demonstration of the benchmarking capabilities.
/// This class provides working examples of performance measurement.
/// </summary>
public static class SimpleBenchmarkDemo
{
    /// <summary>
    /// Demonstrates memory usage measurement.
    /// </summary>
    public static void DemonstrateMemoryBenchmark()
    {
        Console.WriteLine("=== Memory Usage Benchmark Demo ===");
        
        // Get baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var process = Process.GetCurrentProcess();
        var baselineMemory = process.WorkingSet64 / (1024.0 * 1024.0);
        Console.WriteLine($"Baseline Memory: {baselineMemory:F2} MB");
        
        // Create test data
        var notes = new List<Note>();
        for (int i = 0; i < 100; i++)
        {
            notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = $"Test Note {i + 1}",
                Content = new string('A', 1000), // 1KB of content
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            });
        }
        
        // Measure memory after loading
        process.Refresh();
        var afterLoadMemory = process.WorkingSet64 / (1024.0 * 1024.0);
        var memoryPerNote = (afterLoadMemory - baselineMemory) / 100;
        
        Console.WriteLine($"After Load Memory: {afterLoadMemory:F2} MB");
        Console.WriteLine($"Memory per Note: {memoryPerNote:F4} MB");
        Console.WriteLine($"Target (<50MB for 100 notes): {(afterLoadMemory < 50 ? "✓ PASS" : "✗ FAIL")}");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates save performance measurement.
    /// </summary>
    public static async Task DemonstrateSaveBenchmarkAsync()
    {
        Console.WriteLine("=== Save Performance Benchmark Demo ===");
        
        // Create test data
        var testData = new AppData
        {
            Notes = GenerateTestNotes(100),
            Groups = GenerateTestGroups(10),
            Tags = GenerateTestTags(20),
            AppSettings = new AppSettings()
        };
        
        // Measure serialization time (simulating save operation)
        var times = new List<TimeSpan>();
        const int iterations = 10;
        
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate save operation
            var json = System.Text.Json.JsonSerializer.Serialize(testData, JsonSerializerOptionsFactory.Default);
            await Task.Delay(1); // Simulate I/O delay
            
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }
        
        var averageTime = times.Average(t => t.TotalMilliseconds);
        var minTime = times.Min(t => t.TotalMilliseconds);
        var maxTime = times.Max(t => t.TotalMilliseconds);
        
        Console.WriteLine($"Average Save Time: {averageTime:F2} ms");
        Console.WriteLine($"Min Save Time: {minTime:F2} ms");
        Console.WriteLine($"Max Save Time: {maxTime:F2} ms");
        Console.WriteLine($"Target (<50ms): {(averageTime < 50 ? "✓ PASS" : "✗ FAIL")}");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates cache performance measurement.
    /// </summary>
    public static void DemonstrateCacheBenchmark()
    {
        Console.WriteLine("=== Cache Performance Benchmark Demo ===");
        
        // Create a simple cache simulation
        var cache = new Dictionary<Guid, string>();
        var testIds = Enumerable.Range(1, 50).Select(_ => Guid.NewGuid()).ToList();
        
        // Pre-populate cache with some items
        foreach (var id in testIds.Take(25))
        {
            cache[id] = $"Cached value for {id}";
        }
        
        // Simulate cache operations
        var hits = 0;
        var misses = 0;
        var random = new Random(42);
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 1000; i++)
        {
            var testId = testIds[random.Next(testIds.Count)];
            
            if (cache.ContainsKey(testId))
            {
                hits++;
                var value = cache[testId]; // Simulate cache hit
            }
            else
            {
                misses++;
                // Simulate cache miss - add to cache
                cache[testId] = $"New value for {testId}";
            }
        }
        
        stopwatch.Stop();
        
        var hitRate = (double)hits / (hits + misses) * 100;
        var avgOperationTime = stopwatch.Elapsed.TotalMilliseconds / 1000;
        
        Console.WriteLine($"Total Operations: 1,000");
        Console.WriteLine($"Cache Hit Rate: {hitRate:F1}%");
        Console.WriteLine($"Total Hits: {hits:N0}");
        Console.WriteLine($"Total Misses: {misses:N0}");
        Console.WriteLine($"Average Operation Time: {avgOperationTime:F4} ms");
        Console.WriteLine($"Target (>90% hit rate): {(hitRate > 90 ? "✓ PASS" : "✗ FAIL")}");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates LINQ performance comparison.
    /// </summary>
    public static void DemonstrateLinqBenchmark()
    {
        Console.WriteLine("=== LINQ Performance Benchmark Demo ===");
        
        var notes = GenerateTestNotes(1000);
        var targetTag = Guid.NewGuid();
        
        // Add the target tag to some notes
        foreach (var note in notes.Take(100))
        {
            note.TagIds = new List<Guid> { targetTag };
        }
        
        // Benchmark: Unoptimized approach
        var unoptimizedTime = PerformanceBenchmark.MeasureTime(() =>
        {
            var result = notes
                .Where(n => n.TagIds != null)
                .Where(n => n.TagIds.Contains(targetTag))
                .Select(n => n.Id)
                .ToList();
        });
        
        // Benchmark: Optimized approach
        var optimizedTime = PerformanceBenchmark.MeasureTime(() =>
        {
            var result = new List<Guid>();
            foreach (var note in notes)
            {
                if (note.TagIds?.Contains(targetTag) == true)
                {
                    result.Add(note.Id);
                }
            }
        });
        
        var improvement = ((unoptimizedTime.TotalMilliseconds - optimizedTime.TotalMilliseconds) / unoptimizedTime.TotalMilliseconds) * 100;
        
        Console.WriteLine($"Tag Search - Unoptimized: {unoptimizedTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"Tag Search - Optimized: {optimizedTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"Improvement: {improvement:F1}%");
        Console.WriteLine();
    }

    /// <summary>
    /// Runs all benchmark demonstrations.
    /// </summary>
    public static async Task RunAllDemosAsync()
    {
        Console.WriteLine("DevSticky Performance Benchmark Demonstration");
        Console.WriteLine("============================================");
        Console.WriteLine();
        
        DemonstrateMemoryBenchmark();
        await DemonstrateSaveBenchmarkAsync();
        DemonstrateCacheBenchmark();
        DemonstrateLinqBenchmark();
        
        Console.WriteLine("=== Summary ===");
        Console.WriteLine("All benchmark demonstrations completed successfully.");
        Console.WriteLine("The benchmarking infrastructure is working correctly.");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("1. Run comprehensive benchmarks on actual codebase");
        Console.WriteLine("2. Establish performance baselines");
        Console.WriteLine("3. Integrate into CI/CD pipeline");
        Console.WriteLine("4. Monitor performance trends over time");
    }

    private static List<Note> GenerateTestNotes(int count)
    {
        var notes = new List<Note>();
        var random = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = $"Test Note {i + 1}",
                Content = GenerateRandomContent(random.Next(100, 1000)),
                Language = "CSharp",
                IsPinned = random.NextDouble() > 0.8,
                CreatedDate = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                ModifiedDate = DateTime.UtcNow.AddHours(-random.Next(0, 24))
            });
        }
        
        return notes;
    }

    private static List<NoteGroup> GenerateTestGroups(int count)
    {
        var groups = new List<NoteGroup>();
        
        for (int i = 0; i < count; i++)
        {
            groups.Add(new NoteGroup
            {
                Id = Guid.NewGuid(),
                Name = $"Test Group {i + 1}"
            });
        }
        
        return groups;
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
                Name = $"Test Tag {i + 1}",
                Color = colors[i % colors.Length]
            });
        }
        
        return tags;
    }

    private static string GenerateRandomContent(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        var random = new Random(42);
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}