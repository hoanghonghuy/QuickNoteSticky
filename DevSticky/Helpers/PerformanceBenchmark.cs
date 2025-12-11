using System.Diagnostics;

namespace DevSticky.Helpers;

/// <summary>
/// Utility class for performance benchmarking of LINQ optimizations
/// </summary>
public static class PerformanceBenchmark
{
    /// <summary>
    /// Measures the execution time of an action
    /// </summary>
    public static TimeSpan MeasureTime(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Measures the execution time of an async action
    /// </summary>
    public static async Task<TimeSpan> MeasureTimeAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Runs a benchmark comparing old vs new implementation
    /// </summary>
    public static BenchmarkResult CompareMethods(Action oldMethod, Action newMethod, int iterations = 1000)
    {
        // Warm up
        oldMethod();
        newMethod();

        var oldTimes = new List<TimeSpan>();
        var newTimes = new List<TimeSpan>();

        // Measure old method
        for (int i = 0; i < iterations; i++)
        {
            oldTimes.Add(MeasureTime(oldMethod));
        }

        // Measure new method
        for (int i = 0; i < iterations; i++)
        {
            newTimes.Add(MeasureTime(newMethod));
        }

        return new BenchmarkResult
        {
            OldMethodAverage = TimeSpan.FromTicks((long)oldTimes.Average(t => t.Ticks)),
            NewMethodAverage = TimeSpan.FromTicks((long)newTimes.Average(t => t.Ticks)),
            Iterations = iterations,
            ImprovementPercentage = CalculateImprovement(oldTimes.Average(t => t.Ticks), newTimes.Average(t => t.Ticks))
        };
    }

    private static double CalculateImprovement(double oldTicks, double newTicks)
    {
        if (oldTicks == 0) return 0;
        return ((oldTicks - newTicks) / oldTicks) * 100;
    }
}

/// <summary>
/// Result of a performance benchmark comparison
/// </summary>
public class BenchmarkResult
{
    public TimeSpan OldMethodAverage { get; set; }
    public TimeSpan NewMethodAverage { get; set; }
    public int Iterations { get; set; }
    public double ImprovementPercentage { get; set; }

    public override string ToString()
    {
        return $"Benchmark Results ({Iterations} iterations):\n" +
               $"Old Method: {OldMethodAverage.TotalMilliseconds:F3}ms\n" +
               $"New Method: {NewMethodAverage.TotalMilliseconds:F3}ms\n" +
               $"Improvement: {ImprovementPercentage:F1}%";
    }
}