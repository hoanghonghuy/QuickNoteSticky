using System;
using System.Linq;
using System.Threading.Tasks;

namespace DevSticky;

/// <summary>
/// Main program entry point with support for crash analysis
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Check for crash analysis command
        // Note: Crash analysis runs on a separate thread to preserve STA for WPF
        if (args.Contains("--crash-analysis"))
        {
            // Run crash analysis synchronously to avoid breaking STA thread
            return CrashAnalysisProgram.RunCrashAnalysisAsync(args).GetAwaiter().GetResult();
        }
        
        // Normal WPF application startup - must remain synchronous to preserve STA thread
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}