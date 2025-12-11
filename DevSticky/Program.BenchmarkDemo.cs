using DevSticky.Helpers;

namespace DevSticky;

/// <summary>
/// Demonstration program for the performance benchmarking system.
/// This can be run to validate that the benchmarking infrastructure works correctly.
/// </summary>
public class BenchmarkDemoProgram
{
    /// <summary>
    /// Run the benchmark demonstration.
    /// </summary>
    public static async Task RunAsync(string[] args)
    {
        try
        {
            Console.WriteLine("DevSticky Performance Benchmark System");
            Console.WriteLine("====================================");
            Console.WriteLine();
            
            if (args.Length > 0 && args[0] == "--demo")
            {
                await SimpleBenchmarkDemo.RunAllDemosAsync();
            }
            else
            {
                Console.WriteLine("Available options:");
                Console.WriteLine("  --demo    Run benchmark demonstrations");
                Console.WriteLine();
                Console.WriteLine("Example usage:");
                Console.WriteLine("  dotnet run --project DevSticky -- --demo");
                Console.WriteLine();
                Console.WriteLine("For comprehensive benchmarking, use:");
                Console.WriteLine("  dotnet test DevSticky.Tests --filter \"PerformanceBenchmarkTests\"");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running benchmark demo: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}