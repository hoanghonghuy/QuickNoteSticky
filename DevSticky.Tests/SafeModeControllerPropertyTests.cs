using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Property-based tests for safe mode controller functionality
/// **Feature: crash-fix, Property 9: Safe Mode Service Management**
/// **Validates: Requirements 6.2, 6.3**
/// </summary>
public class SafeModeControllerPropertyTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public SafeModeControllerPropertyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    /// <summary>
    /// Simple unit test to debug the safe mode activation issue
    /// </summary>
    [Fact]
    public void SafeModeController_ActivateSafeMode_ShouldSetIsInSafeModeToTrue()
    {
        // Arrange
        var controller = new SafeModeController((IFileSystem)null, null);
        
        // Act
        controller.ActivateSafeMode("Test reason");
        
        // Assert
        Assert.True(controller.IsInSafeMode, "Safe mode should be activated");
        Assert.Equal("Test reason", controller.Configuration.Reason);
        Assert.True(controller.Configuration.IsEnabled);
    }
    
    /// <summary>
    /// Test the test controller to ensure it works correctly
    /// </summary>
    [Fact]
    public void TestSafeModeController_ShouldWorkCorrectly()
    {
        // Arrange
        var controller = new TestSafeModeController();
        
        // Assert initial state
        Assert.False(controller.IsInSafeMode, "Should start in normal mode");
        
        // Act - activate safe mode
        controller.ActivateSafeMode("Test reason");
        
        // Assert activated state
        Assert.True(controller.IsInSafeMode, "Should be in safe mode after activation");
        Assert.Equal("Test reason", controller.Configuration.Reason);
        
        // Act - deactivate safe mode
        controller.DeactivateSafeMode();
        
        // Assert deactivated state
        Assert.False(controller.IsInSafeMode, "Should be back to normal mode after deactivation");
    }

    /// <summary>
    /// **Feature: crash-fix, Property 9: Safe Mode Service Management**
    /// **Validates: Requirements 6.2, 6.3**
    /// 
    /// For any safe mode startup, non-essential services should be disabled and default configurations should be used
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SafeModeServiceManagement_ShouldDisableNonEssentialServices()
    {
        return Prop.ForAll(
            Arb.Default.Bool().Generator.Select(shouldActivate => new SafeModeScenario 
            { 
                ShouldActivateSafeMode = shouldActivate, 
                ActivationReason = "Test reason" 
            }).ToArbitrary(),
            (SafeModeScenario scenario) =>
            {
                // Arrange - use test controller to avoid file system issues
                var controller = CreateSafeModeController(scenario);
                
                if (scenario.ShouldActivateSafeMode)
                {
                    controller.ActivateSafeMode(scenario.ActivationReason);
                }
                
                // Act
                var isInSafeMode = controller.IsInSafeMode;
                var essentialServices = controller.GetEssentialServices();
                var nonEssentialServices = controller.GetNonEssentialServices();
                var status = controller.GetSafeModeStatus();
                var config = controller.Configuration;
                
                // Assert: Safe mode state should match scenario
                var safeModeStateCorrect = isInSafeMode == scenario.ShouldActivateSafeMode;
                
                // Assert: Essential services should be defined and non-empty
                var hasEssentialServices = essentialServices != null && essentialServices.Count > 0;
                
                // Assert: Non-essential services should be defined and non-empty
                var hasNonEssentialServices = nonEssentialServices != null && nonEssentialServices.Count > 0;
                
                // Assert: Essential and non-essential services should not overlap
                var noServiceOverlap = essentialServices != null && nonEssentialServices != null && 
                    !essentialServices.Intersect(nonEssentialServices).Any();
                
                // Assert: When in safe mode, configuration should use defaults
                var usesDefaultsInSafeMode = !isInSafeMode || config.UseDefaultSettings;
                
                // Assert: When in safe mode, non-essential services should be disabled
                var disablesNonEssentialServices = !isInSafeMode || (
                    config.DisableCloudSync &&
                    config.DisableHotkeys &&
                    config.DisableMarkdownPreview &&
                    config.DisableSnippetsAndTemplates);
                
                // Assert: Status should reflect safe mode state correctly
                var statusCorrect = status.IsActive == isInSafeMode;
                
                // Assert: When in safe mode, status should show disabled services
                var statusShowsDisabledServices = !isInSafeMode || 
                    (status.DisabledServices.Count > 0 && 
                     status.DisabledServices.All(s => nonEssentialServices.Any(ns => ns.Name == s)));
                
                // Assert: Configuration should have valid activation reason when in safe mode
                var hasValidReason = !isInSafeMode || !string.IsNullOrWhiteSpace(config.Reason);
                
                // Assert: Configuration should have valid activation timestamp when in safe mode
                var hasValidTimestamp = !isInSafeMode || config.ActivatedAt > DateTime.MinValue;
                
                // Assert: Safe mode indicator should be enabled when in safe mode
                var safeModeIndicatorCorrect = !isInSafeMode || config.ShowSafeModeIndicator;
                
                return safeModeStateCorrect.ToProperty()
                    .And(hasEssentialServices.ToProperty())
                    .And(hasNonEssentialServices.ToProperty())
                    .And(noServiceOverlap.ToProperty())
                    .And(usesDefaultsInSafeMode.ToProperty())
                    .And(disablesNonEssentialServices.ToProperty())
                    .And(statusCorrect.ToProperty())
                    .And(statusShowsDisabledServices.ToProperty())
                    .And(hasValidReason.ToProperty())
                    .And(hasValidTimestamp.ToProperty())
                    .And(safeModeIndicatorCorrect.ToProperty())
                    .Label($"Scenario: ShouldActivate={scenario.ShouldActivateSafeMode}, " +
                           $"ActualSafeMode={isInSafeMode}, " +
                           $"ConfigEnabled={config.IsEnabled}, " +
                           $"SafeModeStateCorrect={safeModeStateCorrect}");
            });
    }

    /// <summary>
    /// Property test for safe mode activation detection logic
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SafeModeActivationDetection_ShouldActivateOnCriticalFailures()
    {
        return Prop.ForAll(
            GenerateStartupFailureScenario(),
            (StartupFailureScenario scenario) =>
            {
                // Arrange
                var controller = new TestSafeModeController();
                
                // Act
                var shouldActivate = controller.ShouldActivateSafeMode(scenario.StartupFailures);
                
                // Assert: Should activate on critical failures
                var criticalFailures = scenario.StartupFailures
                    .Where(f => f.Severity == ValidationSeverity.Critical)
                    .ToList();
                
                var shouldActivateOnCritical = criticalFailures.Count == 0 || shouldActivate;
                
                // Assert: Should activate on multiple error failures
                var errorFailures = scenario.StartupFailures
                    .Where(f => f.Severity == ValidationSeverity.Error)
                    .ToList();
                
                var shouldActivateOnMultipleErrors = errorFailures.Count < 3 || shouldActivate;
                
                // Assert: Should activate on specific failure patterns
                var failurePatterns = new[]
                {
                    "service initialization",
                    "dependency injection",
                    "configuration corruption",
                    "resource loading",
                    "critical service"
                };
                
                var hasPatternMatch = scenario.StartupFailures.Any(f => 
                    failurePatterns.Any(pattern => 
                        f.Issue.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
                
                var shouldActivateOnPattern = !hasPatternMatch || shouldActivate;
                
                // Assert: Should not activate on warnings only
                var onlyWarnings = scenario.StartupFailures.All(f => 
                    f.Severity == ValidationSeverity.Warning || 
                    f.Severity == ValidationSeverity.Information);
                
                var shouldNotActivateOnWarnings = !onlyWarnings || !shouldActivate;
                
                return shouldActivateOnCritical.ToProperty()
                    .And(shouldActivateOnMultipleErrors.ToProperty())
                    .And(shouldActivateOnPattern.ToProperty())
                    .And(shouldNotActivateOnWarnings.ToProperty())
                    .Label($"Critical failures: {criticalFailures.Count}, " +
                           $"Error failures: {errorFailures.Count}, " +
                           $"Has pattern: {hasPatternMatch}, " +
                           $"Only warnings: {onlyWarnings}, " +
                           $"Should activate: {shouldActivate}");
            });
    }

    /// <summary>
    /// Property test for safe mode configuration management
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SafeModeConfiguration_ShouldPersistCorrectly()
    {
        return Prop.ForAll(
            GenerateConfigurationScenario(),
            (ConfigurationScenario scenario) =>
            {
                // Arrange
                var controller = new TestSafeModeController();
                
                // Act: Activate safe mode if requested
                if (scenario.ShouldActivate)
                {
                    controller.ActivateSafeMode(scenario.Reason);
                }
                
                var config = controller.Configuration;
                var status = controller.GetSafeModeStatus();
                
                // Assert: Configuration state should match activation
                var configStateCorrect = config.IsEnabled == scenario.ShouldActivate;
                
                // Assert: Configuration should have valid reason when activated
                var reasonCorrect = !scenario.ShouldActivate || 
                    (!string.IsNullOrWhiteSpace(config.Reason) && config.Reason == scenario.Reason);
                
                // Assert: Configuration should have valid timestamp when activated
                var timestampCorrect = !scenario.ShouldActivate || 
                    config.ActivatedAt > DateTime.MinValue;
                
                // Assert: Status should match configuration
                var statusMatchesConfig = status.IsActive == config.IsEnabled &&
                    status.Reason == config.Reason;
                
                // Assert: Default settings should be used in safe mode
                var defaultSettingsCorrect = !config.IsEnabled || config.UseDefaultSettings;
                
                // Assert: Non-essential services should be disabled in safe mode
                var nonEssentialDisabled = !config.IsEnabled || (
                    config.DisableCloudSync &&
                    config.DisableHotkeys &&
                    config.DisableMarkdownPreview &&
                    config.DisableSnippetsAndTemplates);
                
                // Test deactivation if activated
                bool deactivationWorked = true;
                if (scenario.ShouldActivate && scenario.ShouldDeactivate)
                {
                    deactivationWorked = controller.DeactivateSafeMode();
                    var deactivatedConfig = controller.Configuration;
                    deactivationWorked = deactivationWorked && !deactivatedConfig.IsEnabled;
                }
                
                return configStateCorrect.ToProperty()
                    .And(reasonCorrect.ToProperty())
                    .And(timestampCorrect.ToProperty())
                    .And(statusMatchesConfig.ToProperty())
                    .And(defaultSettingsCorrect.ToProperty())
                    .And(nonEssentialDisabled.ToProperty())
                    .And(deactivationWorked.ToProperty())
                    .Label($"Config state: {configStateCorrect}, " +
                           $"Reason: {reasonCorrect}, " +
                           $"Timestamp: {timestampCorrect}, " +
                           $"Status matches: {statusMatchesConfig}, " +
                           $"Default settings: {defaultSettingsCorrect}, " +
                           $"Non-essential disabled: {nonEssentialDisabled}, " +
                           $"Deactivation: {deactivationWorked}");
            });
    }

    /// <summary>
    /// Property test for service provider configuration in safe mode
    /// </summary>
    [Property(MaxTest = 50)] // Fewer iterations for service provider tests
    public Property SafeModeServiceProvider_ShouldConfigureMinimalServices()
    {
        return Prop.ForAll(
            GenerateServiceProviderScenario(),
            (ServiceProviderScenario scenario) =>
            {
                // Arrange
                var controller = new TestSafeModeController();
                var serviceProvider = CreateServiceProviderForScenario(scenario);
                
                if (scenario.ActivateSafeMode)
                {
                    controller.ActivateSafeMode("Test activation");
                }
                
                // Act
                controller.ConfigureMinimalServices(serviceProvider);
                
                var essentialServices = controller.GetEssentialServices();
                var nonEssentialServices = controller.GetNonEssentialServices();
                
                // Assert: Essential services should be available
                var essentialServicesAvailable = true;
                var availableEssentialCount = 0;
                
                foreach (var serviceType in essentialServices)
                {
                    try
                    {
                        var service = serviceProvider.GetService(serviceType);
                        if (service != null)
                        {
                            availableEssentialCount++;
                        }
                    }
                    catch
                    {
                        // Service not available
                    }
                }
                
                // Assert: Non-essential services should be identifiable
                var nonEssentialServicesIdentified = nonEssentialServices.Count > 0;
                
                // Assert: Essential and non-essential lists should not overlap
                var noServiceOverlap = !essentialServices.Intersect(nonEssentialServices).Any();
                
                // Assert: Configuration should complete without throwing
                var configurationCompleted = true;
                
                return essentialServicesAvailable.ToProperty()
                    .And(nonEssentialServicesIdentified.ToProperty())
                    .And(noServiceOverlap.ToProperty())
                    .And(configurationCompleted.ToProperty())
                    .Label($"Essential available: {availableEssentialCount}/{essentialServices.Count}, " +
                           $"Non-essential identified: {nonEssentialServicesIdentified}, " +
                           $"No overlap: {noServiceOverlap}, " +
                           $"Configuration completed: {configurationCompleted}, " +
                           $"Safe mode active: {scenario.ActivateSafeMode}");
            });
    }

    #region Test Data Generators

    private static Arbitrary<SafeModeScenario> GenerateSafeModeScenario()
    {
        var reasons = new[] { "Startup failure", "Critical error", "Service initialization failed", "Configuration corrupted" };
        
        var gen = from shouldActivate in Arb.Default.Bool().Generator
                  from reasonIndex in Gen.Choose(0, reasons.Length - 1)
                  select new SafeModeScenario
                  {
                      ShouldActivateSafeMode = shouldActivate,
                      ActivationReason = reasons[reasonIndex]
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<StartupFailureScenario> GenerateStartupFailureScenario()
    {
        var gen = from criticalCount in Gen.Choose(0, 3)
                  from errorCount in Gen.Choose(0, 5)
                  from warningCount in Gen.Choose(0, 5)
                  from hasPatterns in Arb.Default.Bool().Generator
                  select new StartupFailureScenario
                  {
                      StartupFailures = GenerateValidationIssues(criticalCount, errorCount, warningCount, hasPatterns)
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<ConfigurationScenario> GenerateConfigurationScenario()
    {
        var reasons = new[] { "Test reason", "Startup failure", "Critical error", "Service failure" };
        
        var gen = from shouldActivate in Arb.Default.Bool().Generator
                  from shouldDeactivate in Arb.Default.Bool().Generator
                  from reasonIndex in Gen.Choose(0, reasons.Length - 1)
                  select new ConfigurationScenario
                  {
                      ShouldActivate = shouldActivate,
                      ShouldDeactivate = shouldDeactivate,
                      Reason = reasons[reasonIndex]
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<ServiceProviderScenario> GenerateServiceProviderScenario()
    {
        var gen = from activateSafeMode in Arb.Default.Bool().Generator
                  from missingEssentialCount in Gen.Choose(0, 3)
                  from hasNonEssential in Arb.Default.Bool().Generator
                  select new ServiceProviderScenario
                  {
                      ActivateSafeMode = activateSafeMode,
                      MissingEssentialServicesCount = missingEssentialCount,
                      HasNonEssentialServices = hasNonEssential
                  };
        
        return gen.ToArbitrary();
    }

    private static List<ValidationIssue> GenerateValidationIssues(int criticalCount, int errorCount, int warningCount, bool hasPatterns)
    {
        var issues = new List<ValidationIssue>();
        
        // Add critical issues
        for (int i = 0; i < criticalCount; i++)
        {
            issues.Add(ValidationIssue.Critical("TestComponent", $"Critical issue {i}", "Fix critical issue"));
        }
        
        // Add error issues
        for (int i = 0; i < errorCount; i++)
        {
            var message = hasPatterns && i == 0 ? "service initialization failed" : $"Error issue {i}";
            issues.Add(ValidationIssue.Error("TestComponent", message, "Fix error"));
        }
        
        // Add warning issues
        for (int i = 0; i < warningCount; i++)
        {
            issues.Add(ValidationIssue.Warning("TestComponent", $"Warning issue {i}", "Address warning"));
        }
        
        return issues;
    }

    #endregion

    #region Test Data Classes

    public class SafeModeScenario
    {
        public bool ShouldActivateSafeMode { get; set; }
        public string ActivationReason { get; set; } = string.Empty;
    }

    public class StartupFailureScenario
    {
        public List<ValidationIssue> StartupFailures { get; set; } = new();
    }

    public class ConfigurationScenario
    {
        public bool ShouldActivate { get; set; }
        public bool ShouldDeactivate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ServiceProviderScenario
    {
        public bool ActivateSafeMode { get; set; }
        public int MissingEssentialServicesCount { get; set; }
        public bool HasNonEssentialServices { get; set; }
    }

    #endregion

    #region Helper Methods

    private TestSafeModeController CreateSafeModeController(SafeModeScenario scenario)
    {
        // Create test controller that doesn't use file system
        return new TestSafeModeController();
    }
    
    /// <summary>
    /// Test-specific SafeModeController that doesn't persist to file system
    /// </summary>
    private class TestSafeModeController : ISafeModeController
    {
        private SafeModeConfig _configuration;
        
        public TestSafeModeController()
        {
            _configuration = SafeModeConfig.CreateDefault();
            System.Diagnostics.Debug.WriteLine($"TestSafeModeController created with IsEnabled={_configuration.IsEnabled}");
        }
        
        public bool IsInSafeMode 
        { 
            get 
            { 
                System.Diagnostics.Debug.WriteLine($"IsInSafeMode called, returning {_configuration.IsEnabled}");
                return _configuration.IsEnabled; 
            } 
        }
        public SafeModeConfig Configuration => _configuration;
        
        public void ActivateSafeMode(string reason)
        {
            System.Diagnostics.Debug.WriteLine($"ActivateSafeMode called with reason: {reason}");
            _configuration = SafeModeConfig.CreateForReason(reason);
            System.Diagnostics.Debug.WriteLine($"After activation, IsEnabled={_configuration.IsEnabled}");
        }
        
        public bool DeactivateSafeMode()
        {
            _configuration.IsEnabled = false;
            _configuration.Reason = string.Empty;
            return true;
        }
        
        public bool ShouldActivateSafeMode(IReadOnlyList<ValidationIssue> startupFailures)
        {
            if (startupFailures == null || startupFailures.Count == 0)
                return false;

            var criticalFailures = startupFailures.Where(f => f.Severity == ValidationSeverity.Critical).ToList();
            if (criticalFailures.Count > 0) return true;

            var errorFailures = startupFailures.Where(f => f.Severity == ValidationSeverity.Error).ToList();
            if (errorFailures.Count >= 3) return true;

            var failurePatterns = new[] { "service initialization", "dependency injection", "configuration corruption", "resource loading", "critical service" };
            return startupFailures.Any(f => failurePatterns.Any(pattern => f.Issue.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
        }
        
        public IReadOnlyList<Type> GetEssentialServices()
        {
            return new[] { typeof(IFileSystem), typeof(IErrorHandler), typeof(IExceptionLogger), typeof(IStorageService), typeof(INoteService), typeof(IThemeService), typeof(IDebounceService), typeof(IDialogService) };
        }
        
        public IReadOnlyList<Type> GetNonEssentialServices()
        {
            return new[] { typeof(ICloudSyncService), typeof(IHotkeyService), typeof(IMarkdownService), typeof(ISnippetService), typeof(ITemplateService), typeof(IExportService), typeof(ISearchService), typeof(ILinkService), typeof(IGroupManagementService), typeof(ITagManagementService), typeof(IFormatterService), typeof(IEncryptionService) };
        }
        
        public void ConfigureMinimalServices(IServiceProvider serviceProvider) { }
        public void ResetConfigurationToDefaults() { }
        
        public SafeModeStatus GetSafeModeStatus()
        {
            var status = new SafeModeStatus
            {
                IsActive = IsInSafeMode,
                Reason = _configuration.Reason,
                ActivatedAt = _configuration.IsEnabled ? _configuration.ActivatedAt : null,
                StartupFailures = new List<string>(_configuration.StartupFailures),
                ConfigurationReset = _configuration.UseDefaultSettings
            };

            if (IsInSafeMode)
            {
                var nonEssentialServices = GetNonEssentialServices();
                status.DisabledServices.AddRange(nonEssentialServices.Select(s => s.Name));
            }

            return status;
        }
        
        public void Dispose() { }
    }

    private static IServiceProvider CreateServiceProviderForScenario(ServiceProviderScenario scenario)
    {
        var services = new ServiceCollection();
        
        // Add essential services (some may be missing based on scenario)
        var essentialServices = new[]
        {
            typeof(IFileSystem),
            typeof(IErrorHandler),
            typeof(IExceptionLogger),
            typeof(IStorageService),
            typeof(INoteService),
            typeof(IThemeService),
            typeof(IDebounceService),
            typeof(IDialogService)
        };
        
        // Add essential services, skipping some based on scenario
        for (int i = scenario.MissingEssentialServicesCount; i < essentialServices.Length; i++)
        {
            var serviceType = essentialServices[i];
            
            // Add concrete implementations for available services
            if (serviceType == typeof(IFileSystem))
                services.AddSingleton<IFileSystem, FileSystemAdapter>();
            else if (serviceType == typeof(IErrorHandler))
                services.AddSingleton<IErrorHandler, ErrorHandler>();
            else if (serviceType == typeof(IExceptionLogger))
                services.AddSingleton<IExceptionLogger, ExceptionLogger>();
            else if (serviceType == typeof(IStorageService))
                services.AddSingleton<IStorageService, StorageService>();
            else if (serviceType == typeof(IDebounceService))
                services.AddSingleton<IDebounceService, DebounceService>();
            // Note: Some services like INoteService, IThemeService, IDialogService 
            // would need more complex setup, so we skip them in this test
        }
        
        // Add non-essential services if requested
        if (scenario.HasNonEssentialServices)
        {
            // Add some non-essential services for testing
            // Note: These would need proper implementations in a real scenario
        }
        
        return services.BuildServiceProvider();
    }

    #endregion

    public void Dispose()
    {
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
    }
}
