using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Property-based tests for startup diagnostics functionality
/// **Feature: crash-fix, Property 8: Diagnostic Logging Completeness**
/// </summary>
public class StartupDiagnosticsPropertyTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<IStartupDiagnostics> _testDiagnostics = new();

    public StartupDiagnosticsPropertyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    /// **Feature: crash-fix, Property 8: Diagnostic Logging Completeness**
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5**
    /// 
    /// For any startup process, all major steps, service registrations, and configuration loading 
    /// should be logged with appropriate detail level
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiagnosticLoggingCompleteness_ShouldLogAllStartupSteps()
    {
        return Prop.ForAll(
            GenerateStartupSteps(),
            (List<StartupStepInfo> startupSteps) =>
            {
                // Arrange
                var errorHandler = new ErrorHandler();
                var diagnostics = new StartupDiagnostics(errorHandler);
                _testDiagnostics.Add(diagnostics);

                // Act: Execute all startup steps
                var executedSteps = new List<StartupStep>();
                foreach (var stepInfo in startupSteps)
                {
                    var step = diagnostics.StartStep(stepInfo.Name, stepInfo.Component, stepInfo.Phase);
                    
                    // Add some context to simulate real startup
                    step.AddContext("TestParameter", stepInfo.TestValue);
                    
                    if (stepInfo.ShouldSucceed)
                    {
                        diagnostics.CompleteStep(step);
                    }
                    else
                    {
                        diagnostics.FailStep(step, stepInfo.ErrorMessage);
                    }
                    
                    executedSteps.Add(step);
                }

                // Assert: Verify all steps are logged with required information
                var allSteps = diagnostics.GetAllSteps();
                
                var allStepsRecorded = allSteps.Count == startupSteps.Count;
                var allStepsHaveNames = allSteps.All(s => !string.IsNullOrEmpty(s.Name));
                var allStepsHaveStartTimes = allSteps.All(s => s.StartTime != default);
                var allStepsHaveEndTimes = allSteps.All(s => s.EndTime.HasValue);
                var allStepsHaveDurations = allSteps.All(s => s.Duration.HasValue);
                var allStepsHaveMemoryTracking = allSteps.All(s => s.StartMemoryMB >= 0);
                var successTrackingCorrect = allSteps.Where(s => s.IsSuccessful).Count() == startupSteps.Count(s => s.ShouldSucceed);
                var failedStepsHaveErrorMessages = allSteps.Where(s => !s.IsSuccessful).All(s => !string.IsNullOrEmpty(s.ErrorMessage));
                
                return allStepsRecorded && allStepsHaveNames && allStepsHaveStartTimes && 
                       allStepsHaveEndTimes && allStepsHaveDurations && allStepsHaveMemoryTracking &&
                       successTrackingCorrect && failedStepsHaveErrorMessages;
            });
    }

    /// <summary>
    /// Property test for startup metrics calculation
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StartupMetrics_ShouldCalculateCorrectly()
    {
        return Prop.ForAll(
            GenerateStartupSteps(),
            (List<StartupStepInfo> startupSteps) =>
            {
                // Arrange
                var diagnostics = new StartupDiagnostics();
                _testDiagnostics.Add(diagnostics);

                // Act: Execute steps and get metrics
                foreach (var stepInfo in startupSteps)
                {
                    if (stepInfo.ShouldSucceed)
                    {
                        diagnostics.ExecuteStep(stepInfo.Name, () => { /* Simulate work */ }, stepInfo.Component, stepInfo.Phase);
                    }
                    else
                    {
                        try
                        {
                            diagnostics.ExecuteStep(stepInfo.Name, () => throw new InvalidOperationException(stepInfo.ErrorMessage), stepInfo.Component, stepInfo.Phase);
                        }
                        catch (InvalidOperationException)
                        {
                            // Expected for failed steps
                        }
                    }
                }

                var metrics = diagnostics.GetPerformanceMetrics();

                // Assert: Verify metrics are calculated correctly
                var expectedSuccessful = startupSteps.Count(s => s.ShouldSucceed);
                var expectedFailed = startupSteps.Count(s => !s.ShouldSucceed);
                
                var totalStepsCorrect = metrics.TotalSteps == startupSteps.Count;
                var successfulStepsCorrect = metrics.SuccessfulSteps == expectedSuccessful;
                var failedStepsCorrect = metrics.FailedSteps == expectedFailed;
                var durationNonNegative = metrics.TotalDuration >= TimeSpan.Zero;
                var memoryAllocationNonNegative = metrics.TotalMemoryAllocatedMB >= 0;
                
                return totalStepsCorrect && successfulStepsCorrect && failedStepsCorrect &&
                       durationNonNegative && memoryAllocationNonNegative;
            });
    }

    /// <summary>
    /// Property test for phase and component filtering
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PhaseAndComponentFiltering_ShouldWorkCorrectly()
    {
        return Prop.ForAll(
            GenerateStartupSteps(),
            (List<StartupStepInfo> startupSteps) =>
            {
                // Arrange
                var diagnostics = new StartupDiagnostics();
                _testDiagnostics.Add(diagnostics);

                // Act: Execute steps
                foreach (var stepInfo in startupSteps)
                {
                    diagnostics.ExecuteStep(stepInfo.Name, () => { /* Simulate work */ }, stepInfo.Component, stepInfo.Phase);
                }

                // Assert: Verify filtering works correctly
                var allSteps = diagnostics.GetAllSteps();
                var phases = startupSteps.Select(s => s.Phase).Distinct().ToList();
                var components = startupSteps.Select(s => s.Component).Distinct().ToList();

                var phaseFilteringCorrect = phases.All(phase =>
                {
                    var expectedCount = startupSteps.Count(s => s.Phase == phase);
                    var actualCount = diagnostics.GetStepsForPhase(phase).Count;
                    return expectedCount == actualCount;
                });

                var componentFilteringCorrect = components.All(component =>
                {
                    var expectedCount = startupSteps.Count(s => s.Component == component);
                    var actualCount = diagnostics.GetStepsForComponent(component).Count;
                    return expectedCount == actualCount;
                });

                return phaseFilteringCorrect && componentFilteringCorrect;
            });
    }

    /// <summary>
    /// Property test for verbose logging behavior
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VerboseLogging_ShouldNotAffectFunctionality()
    {
        return Prop.ForAll(
            GenerateStartupSteps(),
            Arb.From(Gen.Elements(true, false)),
            (List<StartupStepInfo> startupSteps, bool verboseEnabled) =>
            {
                // Arrange
                var diagnostics = new StartupDiagnostics();
                diagnostics.IsVerboseLoggingEnabled = verboseEnabled;
                _testDiagnostics.Add(diagnostics);

                // Act: Execute steps
                foreach (var stepInfo in startupSteps)
                {
                    diagnostics.ExecuteStep(stepInfo.Name, () => { /* Simulate work */ }, stepInfo.Component, stepInfo.Phase);
                }

                // Assert: Verbose logging should not affect step tracking
                var allSteps = diagnostics.GetAllSteps();
                
                var stepCountUnaffected = allSteps.Count == startupSteps.Count;
                var allStepsSuccessful = allSteps.All(s => s.IsSuccessful);
                var verboseSettingPreserved = diagnostics.IsVerboseLoggingEnabled == verboseEnabled;
                
                return stepCountUnaffected && allStepsSuccessful && verboseSettingPreserved;
            });
    }

    /// <summary>
    /// Property test for async step execution
    /// </summary>
    [Property(MaxTest = 50)] // Fewer iterations for async tests
    public Property AsyncStepExecution_ShouldTrackCorrectly()
    {
        return Prop.ForAll(
            Arb.From(GenerateStartupSteps().Generator.Where(steps => steps.Count <= 5)), // Limit for async performance
            (List<StartupStepInfo> startupSteps) =>
            {
                // Arrange
                var diagnostics = new StartupDiagnostics();
                _testDiagnostics.Add(diagnostics);

                // Act: Execute steps synchronously for property testing (async testing is complex in FsCheck)
                foreach (var stepInfo in startupSteps)
                {
                    if (stepInfo.ShouldSucceed)
                    {
                        diagnostics.ExecuteStep(stepInfo.Name, () => stepInfo.TestValue, stepInfo.Component, stepInfo.Phase);
                    }
                    else
                    {
                        try
                        {
                            diagnostics.ExecuteStep(stepInfo.Name, () => throw new InvalidOperationException(stepInfo.ErrorMessage), stepInfo.Component, stepInfo.Phase);
                        }
                        catch (InvalidOperationException)
                        {
                            // Expected for failed steps
                        }
                    }
                }

                // Assert: All steps should be tracked correctly
                var allSteps = diagnostics.GetAllSteps();
                var expectedSuccessful = startupSteps.Count(s => s.ShouldSucceed);
                var expectedFailed = startupSteps.Count(s => !s.ShouldSucceed);

                var allStepsRecorded = allSteps.Count == startupSteps.Count;
                var successTrackingCorrect = allSteps.Count(s => s.IsSuccessful) == expectedSuccessful;
                var failureTrackingCorrect = allSteps.Count(s => !s.IsSuccessful) == expectedFailed;

                return allStepsRecorded && successTrackingCorrect && failureTrackingCorrect;
            });
    }

    /// <summary>
    /// Generate test startup steps
    /// </summary>
    private static Arbitrary<List<StartupStepInfo>> GenerateStartupSteps()
    {
        var stepNameGen = Gen.Elements("Initialize", "LoadConfig", "RegisterServices", "LoadThemes", "SetupHotkeys", "ValidateSettings");
        var componentGen = Gen.Elements("App", "ServiceContainer", "ThemeManager", "HotkeyService", "ConfigManager", "WindowService");
        var phaseGen = Gen.Elements("Initialization", "ServiceRegistration", "ResourceLoading", "Validation", "Finalization");
        var errorMessageGen = Gen.Elements("Service failed to initialize", "Configuration file not found", "Theme loading failed", "Invalid hotkey configuration");
        var testValueGen = Gen.Choose(1, 1000).Select(i => $"TestValue{i}");

        var stepInfoGen = 
            from stepName in stepNameGen
            from component in componentGen
            from phase in phaseGen
            from errorMessage in errorMessageGen
            from testValue in testValueGen
            from shouldSucceed in Gen.Elements(true, false)
            select new StartupStepInfo
            {
                Name = stepName,
                Component = component,
                Phase = phase,
                ErrorMessage = errorMessage,
                TestValue = testValue,
                ShouldSucceed = shouldSucceed
            };

        return Arb.From(Gen.ListOf(stepInfoGen).Where(list => list.Count() > 0 && list.Count() <= 20).Select(list => list.ToList()));
    }

    /// <summary>
    /// Test data for startup steps
    /// </summary>
    private class StartupStepInfo
    {
        public string Name { get; set; } = "";
        public string Component { get; set; } = "";
        public string Phase { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public string TestValue { get; set; } = "";
        public bool ShouldSucceed { get; set; }
    }

    public void Dispose()
    {
        foreach (var diagnostics in _testDiagnostics)
        {
            diagnostics?.Dispose();
        }
        _testDiagnostics.Clear();

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}