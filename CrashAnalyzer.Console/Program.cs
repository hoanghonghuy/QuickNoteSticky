using DevSticky.Services;

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
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Crash analysis failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex}");
    return 1;
}

return 0;