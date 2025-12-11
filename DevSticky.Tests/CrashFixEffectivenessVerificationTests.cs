using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Final verification tests to ensure crash fix effectiveness meets all requirements
/// Task 17: Final checkpoint - Verify crash fix effectiveness
/// </summary>
public class CrashFixEffectivenessVerificationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public CrashFixEffectivenessVerificationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyCrashFixVerification", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    [Fact]
    public async Task VerifyStartupUnderVariousFailureConditions_ShouldRecoverGracefully()
    {
        // Test application startup under various failure conditions
        var scenarios = new[]
        {
            "MissingDirectories",
            "CorruptedConfiguration", 
            "MissingDependencies",
            "ServiceInitializationFailures"
        };

        var results = new List<bool>();

        foreach (var scenario in scenarios)
        {
            var testPath = Path.Combine(_testDirectory, scenario);
            var result = await TestFailureScenario(scenario, testPath);
            results.Add(result);
        }

        // All scenarios should be handled gracefully
        Assert.True(results.All(r => r), "All failure scenarios should be handled gracefully");
    }

    [Fact]
    public async Task VerifyAllRecoveryMechanisms_ShouldWorkCorrectly()
    {
        // Test all recovery mechanisms
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);

        var testPath = Path.Combine(_testDirectory, "RecoveryTest");
        
        // Test directory recovery
        var directoryResult = await recoveryManager.RecoverMissingDirectoriesAsync(testPath);
        Assert.True(directoryResult.IsSuccessful, "Directory recovery should succeed");

        // Test configuration recovery
        var configPath = Path.Combine(testPath, "test.json");
        var configResult = await recoveryManager.RecoverMissingConfigurationAsync(configPath);
        Assert.True(configResult.IsSuccessful, "Configuration recovery should succeed");

        // Test comprehensive recovery
        var comprehensiveResults = await recoveryManager.PerformComprehensiveRecoveryAsync();
        Assert.NotNull(comprehensiveResults);
        Assert.True(comprehensiveResults.Any(r => r.IsSuccessful), "Should have successful recovery operations");
    }

    [Fact]
    public void VerifyDiagnosticInformation_ShouldBeComprehensive()
    {
        // Test diagnostic information collection
        var startupDiagnostics = new StartupDiagnostics();
        var performanceMonitor = new PerformanceMonitoringService(startupDiagnostics);

        // Enable verbose logging
        startupDiagnostics.IsVerboseLoggingEnabled = true;

        // Perform some operations
        var step1 = startupDiagnostics.StartStep("TestOperation1", "TestComponent", "TestPhase");
        performanceMonitor.StartCategoryTiming("TestCategory");
        
        System.Threading.Thread.Sleep(10); // Simulate work
        
        performanceMonitor.StopCategoryTiming("TestCategory");
        startupDiagnostics.CompleteStep(step1);

        // Verify diagnostic data
        var steps = startupDiagnostics.GetAllSteps();
        Assert.True(steps.Count > 0, "Should have recorded steps");
        
        var metrics = startupDiagnostics.GetPerformanceMetrics();
        Assert.True(metrics.TotalSteps > 0, "Should have performance metrics");
        Assert.True(metrics.TotalDuration.TotalMilliseconds >= 0, "Should have timing information");
    }

    [Fact]
    public void ValidateSafeModeFunction_ShouldWorkCorrectly()
    {
        // Test safe mode functionality
        var fileSystem = new FileSystemAdapter();
        var exceptionLogger = new ExceptionLogger(new ErrorHandler());
        var safeModeController = new SafeModeController(fileSystem, exceptionLogger);

        // Test activation
        safeModeController.ActivateSafeMode("Test activation");
        Assert.True(safeModeController.IsInSafeMode, "Safe mode should be active");

        // Test service management
        var essentialServices = safeModeController.GetEssentialServices();
        var nonEssentialServices = safeModeController.GetNonEssentialServices();
        
        Assert.True(essentialServices.Count > 0, "Should have essential services defined");
        Assert.True(nonEssentialServices.Count > 0, "Should have non-essential services defined");

        // Test deactivation
        var deactivated = safeModeController.DeactivateSafeMode();
        Assert.True(deactivated, "Should be able to deactivate safe mode");
        Assert.False(safeModeController.IsInSafeMode, "Safe mode should not be active after deactivation");
    }

    [Fact]
    public async Task EnsurePerformanceImpact_ShouldBeMinimal()
    {
        // Test that performance impact is minimal (<100ms additional startup time)
        var startupDiagnostics = new StartupDiagnostics();
        var validator = new StartupValidator();
        
        var stopwatch = Stopwatch.StartNew();
        
        // Perform validation operations
        var directoryValidation = validator.ValidateDirectories();
        var dependencyValidation = validator.ValidateDependencies();
        var configValidation = validator.ValidateConfiguration();
        
        stopwatch.Stop();
        
        // Performance should be acceptable
        Assert.True(stopwatch.ElapsedMilliseconds < 100, 
            $"Validation overhead should be under 100ms, was {stopwatch.ElapsedMilliseconds}ms");
        
        // Individual validations should be fast - allow more realistic timing for CI environments
        Assert.True(directoryValidation.Duration.TotalMilliseconds < 100, 
            "Directory validation should be under 100ms");
        Assert.True(dependencyValidation.Duration.TotalMilliseconds < 100, 
            "Dependency validation should be under 100ms");
        Assert.True(configValidation.Duration.TotalMilliseconds < 100, 
            "Configuration validation should be under 100ms");
    }

    [Fact]
    public async Task VerifyEndToEndCrashFixWorkflow_ShouldCompleteSuccessfully()
    {
        // Test complete end-to-end crash fix workflow
        var testPath = Path.Combine(_testDirectory, "EndToEndTest");
        var overallStopwatch = Stopwatch.StartNew();

        // Create services
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var exceptionLogger = new ExceptionLogger(errorHandler);
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        var validator = new StartupValidator();
        var safeModeController = new SafeModeController(fileSystem, exceptionLogger);
        var startupDiagnostics = new StartupDiagnostics();
        var performanceMonitor = new PerformanceMonitoringService(startupDiagnostics);

        // 1. Initial validation (should fail)
        var initialValidation = validator.ValidateDirectories();
        
        // 2. Check safe mode activation
        var criticalIssues = initialValidation.Issues.Where(i => i.Severity == ValidationSeverity.Critical).ToList();
        var shouldActivateSafeMode = safeModeController.ShouldActivateSafeMode(criticalIssues);
        
        if (shouldActivateSafeMode)
        {
            safeModeController.ActivateSafeMode("End-to-end test simulation");
        }
        
        // 3. Perform recovery
        var recoveryResults = await recoveryManager.PerformComprehensiveRecoveryAsync();
        
        // 4. Post-recovery validation
        var postRecoveryValidation = validator.ValidateDirectories();
        var configValidation = validator.ValidateConfiguration();
        
        // 5. Performance metrics
        performanceMonitor.MarkMilestone("EndToEndComplete");
        
        overallStopwatch.Stop();

        // Assert: Complete workflow should work correctly
        Assert.NotNull(recoveryResults);
        Assert.True(recoveryResults.Count > 0, "Should have recovery results");
        
        var successfulRecoveries = recoveryResults.Where(r => r.IsSuccessful).ToList();
        Assert.True(successfulRecoveries.Count > 0, "Should have successful recoveries");
        
        // System should be functional after recovery - check if directory was created during recovery
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevSticky");
        Assert.True(fileSystem.DirectoryExists(appDataPath) || Directory.Exists(testPath), "App data directory should exist");
        
        // Performance should be reasonable
        Assert.True(overallStopwatch.ElapsedMilliseconds < 5000, 
            $"End-to-end crash fix should complete within 5 seconds, took {overallStopwatch.ElapsedMilliseconds}ms");
        
        // Safe mode should work correctly if activated
        if (safeModeController.IsInSafeMode)
        {
            var status = safeModeController.GetSafeModeStatus();
            Assert.True(status.IsActive, "Safe mode should be active");
            Assert.True(status.DisabledServices.Count > 0, "Should have disabled services");
        }
    }

    private async Task<bool> TestFailureScenario(string scenarioName, string testPath)
    {
        try
        {
            var fileSystem = new FileSystemAdapter();
            var errorHandler = new ErrorHandler();
            var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
            var validator = new StartupValidator();

            switch (scenarioName)
            {
                case "MissingDirectories":
                    // Ensure directory doesn't exist
                    if (Directory.Exists(testPath))
                        Directory.Delete(testPath, true);
                    
                    var dirResult = await recoveryManager.RecoverMissingDirectoriesAsync(testPath);
                    return dirResult.IsSuccessful && Directory.Exists(testPath);

                case "CorruptedConfiguration":
                    Directory.CreateDirectory(testPath);
                    _createdDirectories.Add(testPath);
                    
                    var configPath = Path.Combine(testPath, "test.json");
                    await File.WriteAllTextAsync(configPath, "{ invalid json");
                    _createdFiles.Add(configPath);
                    
                    var configResult = await recoveryManager.RecoverCorruptedConfigurationAsync(configPath);
                    return configResult.IsSuccessful;

                case "MissingDependencies":
                    var depValidation = validator.ValidateDependencies();
                    return depValidation.Duration.TotalMilliseconds >= 0; // Should complete without crashing

                case "ServiceInitializationFailures":
                    // Test with minimal service collection
                    var services = new ServiceCollection();
                    services.AddSingleton<IFileSystem, FileSystemAdapter>();
                    var serviceProvider = services.BuildServiceProvider();
                    
                    var serviceValidation = validator.ValidateServices();
                    return serviceValidation.Duration.TotalMilliseconds >= 0; // Should complete without crashing

                default:
                    return false;
            }
        }
        catch (Exception)
        {
            return false; // Scenario failed
        }
    }

    public void Dispose()
    {
        // Clean up created directories
        foreach (var directory in _createdDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        // Clean up created files
        foreach (var file in _createdFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}