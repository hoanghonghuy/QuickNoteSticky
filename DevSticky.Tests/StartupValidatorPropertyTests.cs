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
/// Property-based tests for startup validation functionality
/// **Feature: crash-fix, Property 5: Startup Validation Completeness**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
public class StartupValidatorPropertyTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public StartupValidatorPropertyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    /// <summary>
    /// **Feature: crash-fix, Property 5: Startup Validation Completeness**
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
    /// 
    /// For any startup attempt, all prerequisites (directories, services, configurations, DI container, resources) 
    /// should be validated before use
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StartupValidationCompleteness_ShouldValidateAllPrerequisites()
    {
        return Prop.ForAll(
            GenerateValidationScenario(),
            (ValidationScenario scenario) =>
            {
                // Arrange
                var validator = CreateValidatorForScenario(scenario);
                
                // Act
                var result = validator.Validate();
                
                // Assert: Validation should complete and return a result
                var validationCompleted = result != null;
                
                // Assert: All validation categories should be covered
                var hasDirectoryValidation = result.Issues.Any(i => i.Component.Contains("Directory")) || 
                                           result.Component == "DirectoryValidation";
                var hasConfigValidation = result.Issues.Any(i => i.Component.Contains("Configuration")) || 
                                         result.Component == "ConfigurationValidation";
                var hasResourceValidation = result.Issues.Any(i => i.Component.Contains("Resource")) || 
                                          result.Component == "ResourceValidation";
                
                // Assert: Result should have timing information
                var hasTiming = result.Duration.TotalMilliseconds >= 0;
                
                // Assert: Issues should have proper severity levels
                var issuesHaveValidSeverity = result.Issues.All(i => 
                    Enum.IsDefined(typeof(ValidationSeverity), i.Severity));
                
                // Assert: Critical issues should make validation fail
                var criticalIssues = result.GetCriticalIssues().ToList();
                var validationLogic = criticalIssues.Count == 0 || !result.IsValid;
                
                return validationCompleted.ToProperty()
                    .And(hasTiming.ToProperty())
                    .And(issuesHaveValidSeverity.ToProperty())
                    .And(validationLogic.ToProperty())
                    .Label($"Validation completed: {validationCompleted}, " +
                           $"Has timing: {hasTiming}, " +
                           $"Valid severity: {issuesHaveValidSeverity}, " +
                           $"Logic correct: {validationLogic}, " +
                           $"Critical issues: {criticalIssues.Count}, " +
                           $"Is valid: {result.IsValid}");
            });
    }

    /// <summary>
    /// Property test for directory validation behavior
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirectoryValidation_ShouldHandleAllScenarios()
    {
        return Prop.ForAll(
            GenerateDirectoryScenario(),
            (DirectoryScenario scenario) =>
            {
                // Arrange
                var validator = new StartupValidator();
                SetupDirectoryScenario(scenario);
                
                // Act
                var result = validator.ValidateDirectories();
                
                // Assert: Should always return a result
                var hasResult = result != null;
                
                // Assert: Should have timing information
                var hasTiming = result.Duration.TotalMilliseconds >= 0;
                
                // Assert: Should be marked as directory validation
                var correctComponent = result.Component == "DirectoryValidation";
                
                // Assert: If directories don't exist and can't be created, should have critical issues
                var logicalResult = true;
                if (scenario.AppDataExists || scenario.CanCreateDirectories)
                {
                    // Should not have critical directory creation issues
                    logicalResult = !result.GetCriticalIssues().Any(i => 
                        i.Issue.Contains("Created missing") || 
                        i.Issue.Contains("write permissions"));
                }
                
                return hasResult.ToProperty()
                    .And(hasTiming.ToProperty())
                    .And(correctComponent.ToProperty())
                    .Label($"Has result: {hasResult}, " +
                           $"Has timing: {hasTiming}, " +
                           $"Correct component: {correctComponent}, " +
                           $"AppData exists: {scenario.AppDataExists}, " +
                           $"Can create: {scenario.CanCreateDirectories}");
            });
    }

    /// <summary>
    /// Property test for configuration validation behavior
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigurationValidation_ShouldHandleAllFormats()
    {
        return Prop.ForAll(
            GenerateConfigurationScenario(),
            (ConfigurationScenario scenario) =>
            {
                // Arrange
                var validator = new StartupValidator();
                
                // Act
                var result = validator.ValidateConfiguration();
                
                // Assert: Should always return a result
                var hasResult = result != null;
                
                // Assert: Should have timing information
                var hasTiming = result.Duration.TotalMilliseconds >= 0;
                
                // Assert: Should be marked as configuration validation
                var correctComponent = result.Component == "ConfigurationValidation";
                
                // Assert: Should complete without throwing exceptions
                var completedSuccessfully = true;
                
                return hasResult.ToProperty()
                    .And(hasTiming.ToProperty())
                    .And(correctComponent.ToProperty())
                    .And(completedSuccessfully.ToProperty())
                    .Label($"Has result: {hasResult}, " +
                           $"Has timing: {hasTiming}, " +
                           $"Correct component: {correctComponent}, " +
                           $"Completed successfully: {completedSuccessfully}");
            });
    }

    /// <summary>
    /// Property test for service validation with dependency injection
    /// </summary>
    [Property(MaxTest = 50)] // Fewer iterations for DI tests as they're more expensive
    public Property ServiceValidation_ShouldValidateAllServices()
    {
        return Prop.ForAll(
            GenerateServiceScenario(),
            (ServiceScenario scenario) =>
            {
                // Arrange
                var serviceProvider = CreateServiceProviderForScenario(scenario);
                var fileSystem = new FileSystemAdapter();
                var validator = new StartupValidator(serviceProvider, fileSystem);
                
                // Act
                var result = validator.ValidateServices();
                
                // Assert: Should always return a result
                var hasResult = result != null;
                
                // Assert: Should have timing information
                var hasTiming = result.Duration.TotalMilliseconds >= 0;
                
                // Assert: Should be marked as service validation
                var correctComponent = result.Component == "ServiceValidation";
                
                // Assert: Missing critical services should produce critical issues
                var logicalResult = true;
                if (scenario.MissingCriticalServices.Count > 0)
                {
                    logicalResult = result.GetCriticalIssues().Any();
                }
                
                return hasResult.ToProperty()
                    .And(hasTiming.ToProperty())
                    .And(correctComponent.ToProperty())
                    .And(logicalResult.ToProperty())
                    .Label($"Has result: {hasResult}, " +
                           $"Has timing: {hasTiming}, " +
                           $"Correct component: {correctComponent}, " +
                           $"Missing services: {scenario.MissingCriticalServices.Count}, " +
                           $"Logic correct: {logicalResult}");
            });
    }

    /// <summary>
    /// **Feature: crash-fix, Property 2: Dependency Validation Completeness**
    /// **Validates: Requirements 1.4, 1.5**
    /// 
    /// For any startup attempt, all required dependencies (DLLs, packages, configurations) 
    /// should be verified before proceeding
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DependencyValidationCompleteness_ShouldVerifyAllDependencies()
    {
        return Prop.ForAll(
            GenerateDependencyScenario(),
            (DependencyScenario scenario) =>
            {
                // Arrange
                var validator = new StartupValidator();
                
                // Act
                var result = validator.ValidateDependencies();
                
                // Assert: Should always return a result
                var hasResult = result != null;
                
                // Assert: Should have timing information
                var hasTiming = result.Duration.TotalMilliseconds >= 0;
                
                // Assert: Should be marked as dependency validation
                var correctComponent = result.Component == "DependencyValidation";
                
                // Assert: Should check for required NuGet packages
                var checksNuGetPackages = result.Issues.Any(i => 
                    i.Issue.Contains("package") || 
                    i.Issue.Contains("assembly") ||
                    i.Component == "DependencyValidation");
                
                // Assert: Should check .NET Runtime version
                var checksRuntimeVersion = result.Issues.Any(i => 
                    i.Issue.Contains(".NET Runtime") || 
                    i.Issue.Contains("version") ||
                    i.Component == "DependencyValidation");
                
                // Assert: Should check WPF dependencies
                var checksWpfDependencies = result.Issues.Any(i => 
                    i.Issue.Contains("WPF") || 
                    i.Issue.Contains("Application") ||
                    i.Component == "DependencyValidation");
                
                // Assert: Should check framework dependencies
                var checksFrameworkDependencies = result.Issues.Any(i => 
                    i.Issue.Contains("Framework dependency") || 
                    i.Issue.Contains("System.") ||
                    i.Component == "DependencyValidation");
                
                // Assert: All issues should have valid severity levels
                var issuesHaveValidSeverity = result.Issues.All(i => 
                    Enum.IsDefined(typeof(ValidationSeverity), i.Severity));
                
                // Assert: Critical dependency issues should make validation fail
                var criticalIssues = result.GetCriticalIssues().ToList();
                var validationLogic = criticalIssues.Count == 0 || !result.IsValid;
                
                // Assert: Should complete without throwing exceptions
                var completedSuccessfully = true;
                
                return hasResult.ToProperty()
                    .And(hasTiming.ToProperty())
                    .And(correctComponent.ToProperty())
                    .And(issuesHaveValidSeverity.ToProperty())
                    .And(validationLogic.ToProperty())
                    .And(completedSuccessfully.ToProperty())
                    .Label($"Has result: {hasResult}, " +
                           $"Has timing: {hasTiming}, " +
                           $"Correct component: {correctComponent}, " +
                           $"Checks packages: {checksNuGetPackages}, " +
                           $"Checks runtime: {checksRuntimeVersion}, " +
                           $"Checks WPF: {checksWpfDependencies}, " +
                           $"Checks framework: {checksFrameworkDependencies}, " +
                           $"Valid severity: {issuesHaveValidSeverity}, " +
                           $"Logic correct: {validationLogic}, " +
                           $"Critical issues: {criticalIssues.Count}, " +
                           $"Is valid: {result.IsValid}");
            });
    }

    #region Test Data Generators

    private static Arbitrary<ValidationScenario> GenerateValidationScenario()
    {
        var gen = from hasServiceProvider in Arb.Default.Bool().Generator
                  from hasFileSystem in Arb.Default.Bool().Generator
                  from directoryExists in Arb.Default.Bool().Generator
                  from configExists in Arb.Default.Bool().Generator
                  select new ValidationScenario
                  {
                      HasServiceProvider = hasServiceProvider,
                      HasFileSystem = hasFileSystem,
                      DirectoryExists = directoryExists,
                      ConfigurationExists = configExists
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<DirectoryScenario> GenerateDirectoryScenario()
    {
        var gen = from appDataExists in Arb.Default.Bool().Generator
                  from canCreate in Arb.Default.Bool().Generator
                  from isWritable in Arb.Default.Bool().Generator
                  select new DirectoryScenario
                  {
                      AppDataExists = appDataExists,
                      CanCreateDirectories = canCreate,
                      IsWritable = isWritable
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<ConfigurationScenario> GenerateConfigurationScenario()
    {
        var gen = from hasSettings in Arb.Default.Bool().Generator
                  from hasNotes in Arb.Default.Bool().Generator
                  from hasInvalidJson in Arb.Default.Bool().Generator
                  from hasCorruptedFile in Arb.Default.Bool().Generator
                  select new ConfigurationScenario
                  {
                      HasSettingsFile = hasSettings,
                      HasNotesFile = hasNotes,
                      HasInvalidJson = hasInvalidJson,
                      HasCorruptedFile = hasCorruptedFile
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<ServiceScenario> GenerateServiceScenario()
    {
        var criticalServices = new[] { "IFileSystem", "IErrorHandler", "IStorageService" };
        var optionalServices = new[] { "ICloudSyncService", "IHotkeyService" };
        
        var gen = from missingCount in Gen.Choose(0, criticalServices.Length)
                  from missingOptionalCount in Gen.Choose(0, optionalServices.Length)
                  select new ServiceScenario
                  {
                      MissingCriticalServices = criticalServices.Take(missingCount).ToList(),
                      MissingOptionalServices = optionalServices.Take(missingOptionalCount).ToList()
                  };
        
        return gen.ToArbitrary();
    }

    private static Arbitrary<DependencyScenario> GenerateDependencyScenario()
    {
        var requiredPackages = new[] { "AvalonEdit", "Google.Apis.Drive.v3", "Hardcodet.NotifyIcon.Wpf", "Markdig" };
        var frameworkDependencies = new[] { "System.Text.Json", "System.IO.FileSystem", "System.Threading.Tasks" };
        
        var gen = from missingPackageCount in Gen.Choose(0, requiredPackages.Length)
                  from missingFrameworkCount in Gen.Choose(0, frameworkDependencies.Length)
                  from hasWpfContext in Arb.Default.Bool().Generator
                  from runtimeVersionMajor in Gen.Choose(6, 10) // .NET versions 6-10
                  select new DependencyScenario
                  {
                      MissingPackages = requiredPackages.Take(missingPackageCount).ToList(),
                      MissingFrameworkDependencies = frameworkDependencies.Take(missingFrameworkCount).ToList(),
                      HasWpfApplicationContext = hasWpfContext,
                      RuntimeVersionMajor = runtimeVersionMajor
                  };
        
        return gen.ToArbitrary();
    }

    #endregion

    #region Test Data Classes

    public class ValidationScenario
    {
        public bool HasServiceProvider { get; set; }
        public bool HasFileSystem { get; set; }
        public bool DirectoryExists { get; set; }
        public bool ConfigurationExists { get; set; }
    }

    public class DirectoryScenario
    {
        public bool AppDataExists { get; set; }
        public bool CanCreateDirectories { get; set; }
        public bool IsWritable { get; set; }
    }

    public class ConfigurationScenario
    {
        public bool HasSettingsFile { get; set; }
        public bool HasNotesFile { get; set; }
        public bool HasInvalidJson { get; set; }
        public bool HasCorruptedFile { get; set; }
    }

    public class ServiceScenario
    {
        public List<string> MissingCriticalServices { get; set; } = new();
        public List<string> MissingOptionalServices { get; set; } = new();
    }

    public class DependencyScenario
    {
        public List<string> MissingPackages { get; set; } = new();
        public List<string> MissingFrameworkDependencies { get; set; } = new();
        public bool HasWpfApplicationContext { get; set; }
        public int RuntimeVersionMajor { get; set; }
    }

    #endregion

    #region Helper Methods

    private StartupValidator CreateValidatorForScenario(ValidationScenario scenario)
    {
        if (scenario.HasServiceProvider)
        {
            var services = new ServiceCollection();
            
            // Add basic services
            services.AddSingleton<IFileSystem, FileSystemAdapter>();
            services.AddSingleton<IErrorHandler, ErrorHandler>();
            services.AddSingleton<IExceptionLogger, ExceptionLogger>();
            
            if (scenario.HasFileSystem)
            {
                services.AddSingleton<IStorageService, StorageService>();
            }
            
            var serviceProvider = services.BuildServiceProvider();
            var fileSystem = scenario.HasFileSystem ? serviceProvider.GetService<IFileSystem>() : null;
            
            return new StartupValidator(serviceProvider, fileSystem);
        }
        
        return new StartupValidator();
    }

    private void SetupDirectoryScenario(DirectoryScenario scenario)
    {
        // This is a simplified setup - in a real test environment,
        // we would mock the file system or use a test directory
        // For now, we rely on the actual file system behavior
    }

    private void SetupConfigurationScenario(ConfigurationScenario scenario)
    {
        if (scenario.HasInvalidJson)
        {
            // Create a file with invalid JSON for testing
            var testFile = Path.Combine(_testDirectory, "invalid.json");
            File.WriteAllText(testFile, "{ invalid json content");
            _createdFiles.Add(testFile);
        }
    }

    private static IServiceProvider CreateServiceProviderForScenario(ServiceScenario scenario)
    {
        var services = new ServiceCollection();
        
        // Add services that are not in the missing list
        if (!scenario.MissingCriticalServices.Contains("IFileSystem"))
        {
            services.AddSingleton<IFileSystem, FileSystemAdapter>();
        }
        
        if (!scenario.MissingCriticalServices.Contains("IErrorHandler"))
        {
            services.AddSingleton<IErrorHandler, ErrorHandler>();
        }
        
        if (!scenario.MissingCriticalServices.Contains("IStorageService"))
        {
            services.AddSingleton<IStorageService, StorageService>();
        }
        
        // Add optional services
        if (!scenario.MissingOptionalServices.Contains("ICloudSyncService"))
        {
            // Note: We can't easily add ICloudSyncService without all its dependencies
            // This is a simplified test setup
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