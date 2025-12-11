using System;
using System.Threading.Tasks;
using DevSticky.Services;

namespace DevSticky;

/// <summary>
/// Console program to analyze DevSticky crashes
/// Usage: dotnet run --project DevSticky -- --crash-analysis
/// </summary>
public static class CrashAnalysisProgram
{
    public static async Task<int> RunCrashAnalysisAsync(string[] args)
    {
        Console.WriteLine("DevSticky Crash Analysis Tool");
        Console.WriteLine("============================");
        Console.WriteLine();
        
        try
        {
            // Run immediate crash analysis
            var analysis = await StartupCrashDetector.GetImmediateCrashAnalysisAsync();
            Console.WriteLine(analysis);
            
            Console.WriteLine();
            Console.WriteLine("=== Analysis Complete ===");
            Console.WriteLine();
            Console.WriteLine("If crashes were found, consider:");
            Console.WriteLine("1. Running the application in safe mode");
            Console.WriteLine("2. Resetting configuration files to defaults");
            Console.WriteLine("3. Checking file permissions");
            Console.WriteLine("4. Updating .NET runtime");
            Console.WriteLine("5. Reinstalling the application");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Crash analysis failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex}");
            return 1;
        }
    }
}