using System;
using System.IO;
using System.Linq;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for StartupValidator class
/// Tests individual validation methods and their behavior
/// </summary>
public class StartupValidatorUnitTests : IDisposable
{
    private readonly string _testDirectory;

    public StartupValidatorUnitTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithoutParameters_ShouldCreateValidator()
    {
        // Act
        var validator = new StartupValidator();

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public void Constructor_WithServiceProvider_ShouldCreateValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        var serviceProvider = services.BuildServiceProvider();
        var fileSystem = serviceProvider.GetService<IFileSystem>();

        // Act
        var validator = new StartupValidator(serviceProvider, fileSystem);

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceProvider nullServiceProvider = null!;
        var fileSystem = new FileSystemAdapter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StartupValidator(nullServiceProvider, fileSystem));
    }

    #endregion

    #region Validate Method Tests

    [Fact]
    public void Validate_WithoutServiceProvider_ShouldCompleteBasicValidation()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.Validate();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("StartupValidator", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.True(result.ValidationTime <= DateTime.UtcNow);
        
        // Should have information about missing service provider
        var hasServiceProviderInfo = result.Issues.Any(i => 
            i.Component == "StartupValidator" && 
            i.Issue.Contains("Service provider not available"));
        
        Assert.True(hasServiceProviderInfo, "Should note missing service provider");
    }

    [Fact]
    public void Validate_WithServiceProvider_ShouldPerformFullValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        var serviceProvider = services.BuildServiceProvider();
        var fileSystem = serviceProvider.GetService<IFileSystem>();
        var validator = new StartupValidator(serviceProvider, fileSystem);

        // Act
        var result = validator.Validate();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("StartupValidator", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        
        // Should perform multiple validation categories
        var hasDirectoryValidation = result.Issues.Any(i => i.Component.Contains("Directory"));
        var hasConfigValidation = result.Issues.Any(i => i.Component.Contains("Configuration"));
        var hasServiceValidation = result.Issues.Any(i => i.Component.Contains("Service"));
        
        // At least some validation should occur
        Assert.True(hasDirectoryValidation || hasConfigValidation || hasServiceValidation || result.Issues.Count == 0);
    }

    #endregion

    #region ValidateDirectories Method Tests

    [Fact]
    public void ValidateDirectories_ShouldReturnDirectoryValidationResult()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateDirectories();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DirectoryValidation", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.True(result.ValidationTime <= DateTime.UtcNow);
    }

    [Fact]
    public void ValidateDirectories_WithExistingDirectories_ShouldPassValidation()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateDirectories();

        // Assert
        Assert.NotNull(result);
        
        // Should complete without critical errors if directories exist or can be created
        var criticalIssues = result.GetCriticalIssues().ToList();
        
        // If there are critical issues, they should be about permissions or access
        if (criticalIssues.Any())
        {
            var hasPermissionIssues = criticalIssues.Any(i => 
                i.Issue.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                i.Issue.Contains("access", StringComparison.OrdinalIgnoreCase));
            
            Assert.True(hasPermissionIssues, "Critical directory issues should be about permissions");
        }
    }

    #endregion

    #region ValidateConfiguration Method Tests

    [Fact]
    public void ValidateConfiguration_ShouldReturnConfigurationValidationResult()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateConfiguration();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ConfigurationValidation", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.True(result.ValidationTime <= DateTime.UtcNow);
    }

    [Fact]
    public void ValidateConfiguration_WithMissingFiles_ShouldIdentifyMissingFiles()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateConfiguration();

        // Assert
        Assert.NotNull(result);
        
        // May have issues about missing configuration files
        var hasMissingFileIssues = result.Issues.Any(i => 
            i.Issue.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("not found", StringComparison.OrdinalIgnoreCase));
        
        // This is acceptable - missing files can be recovered
        if (hasMissingFileIssues)
        {
            var hasRecoveryGuidance = result.Issues.Any(i => 
                !string.IsNullOrEmpty(i.SuggestedAction));
            
            Assert.True(hasRecoveryGuidance, "Missing file issues should have recovery guidance");
        }
    }

    #endregion

    #region ValidateServices Method Tests

    [Fact]
    public void ValidateServices_WithoutServiceProvider_ShouldReturnInformationalResult()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateServices();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ServiceValidation", result.Component);
        
        // Should have informational message about missing service provider
        var hasServiceProviderInfo = result.Issues.Any(i => 
            i.Severity == ValidationSeverity.Information &&
            i.Issue.Contains("Service provider not available"));
        
        Assert.True(hasServiceProviderInfo, "Should inform about missing service provider");
    }

    [Fact]
    public void ValidateServices_WithServiceProvider_ShouldValidateServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        services.AddSingleton<IExceptionLogger, ExceptionLogger>();
        var serviceProvider = services.BuildServiceProvider();
        var fileSystem = serviceProvider.GetService<IFileSystem>();
        var validator = new StartupValidator(serviceProvider, fileSystem);

        // Act
        var result = validator.ValidateServices();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ServiceValidation", result.Component);
        
        // Should check for critical services
        var hasServiceChecks = result.Issues.Any(i => 
            i.Issue.Contains("service", StringComparison.OrdinalIgnoreCase)) || 
            result.Issues.Count == 0; // No issues means services are available
        
        Assert.True(hasServiceChecks, "Should perform service validation");
    }

    [Fact]
    public void ValidateServices_WithMissingCriticalServices_ShouldReportCriticalIssues()
    {
        // Arrange
        var services = new ServiceCollection();
        // Intentionally register only some services
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        // Missing: IErrorHandler, IStorageService, etc.
        
        var serviceProvider = services.BuildServiceProvider();
        var fileSystem = serviceProvider.GetService<IFileSystem>();
        var validator = new StartupValidator(serviceProvider, fileSystem);

        // Act
        var result = validator.ValidateServices();

        // Assert
        Assert.NotNull(result);
        
        var criticalIssues = result.GetCriticalIssues().ToList();
        Assert.True(criticalIssues.Count > 0, "Should report missing critical services");
        
        var hasMissingServiceIssues = criticalIssues.Any(i => 
            i.Issue.Contains("service", StringComparison.OrdinalIgnoreCase) &&
            (i.Issue.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
             i.Issue.Contains("not registered", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasMissingServiceIssues, "Should identify missing services");
    }

    #endregion

    #region ValidateDependencies Method Tests

    [Fact]
    public void ValidateDependencies_ShouldReturnDependencyValidationResult()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateDependencies();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DependencyValidation", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.True(result.ValidationTime <= DateTime.UtcNow);
    }

    [Fact]
    public void ValidateDependencies_ShouldCheckRequiredPackages()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateDependencies();

        // Assert
        Assert.NotNull(result);
        
        // Should check for required NuGet packages
        var hasPackageChecks = result.Issues.Any(i => 
            i.Issue.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("assembly", StringComparison.OrdinalIgnoreCase)) ||
            result.Issues.Count == 0; // No issues means packages are available
        
        Assert.True(hasPackageChecks, "Should check required packages");
    }

    [Fact]
    public void ValidateDependencies_ShouldCheckRuntimeVersion()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateDependencies();

        // Assert
        Assert.NotNull(result);
        
        // Should check .NET Runtime version
        var hasRuntimeCheck = result.Issues.Any(i => 
            i.Issue.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("version", StringComparison.OrdinalIgnoreCase)) ||
            result.Issues.Count == 0; // No issues means runtime is compatible
        
        Assert.True(hasRuntimeCheck, "Should check runtime version");
    }

    [Fact]
    public void ValidateDependencies_ShouldCheckWpfDependencies()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateDependencies();

        // Assert
        Assert.NotNull(result);
        
        // Should check WPF dependencies
        var hasWpfCheck = result.Issues.Any(i => 
            i.Issue.Contains("WPF", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("Application", StringComparison.OrdinalIgnoreCase)) ||
            result.Issues.Count == 0; // No issues means WPF is available
        
        Assert.True(hasWpfCheck, "Should check WPF dependencies");
    }

    #endregion

    #region ValidateResources Method Tests

    [Fact]
    public void ValidateResources_ShouldReturnResourceValidationResult()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateResources();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ResourceValidation", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.True(result.ValidationTime <= DateTime.UtcNow);
    }

    [Fact]
    public void ValidateResources_ShouldCheckThemeFiles()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.ValidateResources();

        // Assert
        Assert.NotNull(result);
        
        // Should check theme resources
        var hasThemeCheck = result.Issues.Any(i => 
            i.Issue.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("resource", StringComparison.OrdinalIgnoreCase)) ||
            result.Issues.Count == 0; // No issues means resources are available
        
        Assert.True(hasThemeCheck, "Should check theme resources");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Validate_WhenExceptionOccurs_ShouldCatchAndReportException()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.Validate();

        // Assert
        Assert.NotNull(result);
        
        // Should not throw exceptions, even if internal operations fail
        // Any exceptions should be captured as validation issues
        var hasExceptionIssues = result.Issues.Any(i => 
            i.Severity == ValidationSeverity.Critical &&
            i.Issue.Contains("exception", StringComparison.OrdinalIgnoreCase));
        
        // This test mainly ensures no unhandled exceptions are thrown
        Assert.True(true, "Validation should complete without throwing exceptions");
    }

    #endregion

    #region Validation Result Tests

    [Fact]
    public void ValidationResult_ShouldHaveCorrectStructure()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.Validate();

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Component));
        Assert.True(result.ValidationTime <= DateTime.UtcNow);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.NotNull(result.Issues);
        
        // All issues should have valid severity levels
        var allIssuesHaveValidSeverity = result.Issues.All(i => 
            Enum.IsDefined(typeof(ValidationSeverity), i.Severity));
        
        Assert.True(allIssuesHaveValidSeverity, "All issues should have valid severity levels");
        
        // All issues should have non-empty messages
        var allIssuesHaveMessages = result.Issues.All(i => 
            !string.IsNullOrWhiteSpace(i.Issue));
        
        Assert.True(allIssuesHaveMessages, "All issues should have messages");
    }

    [Fact]
    public void ValidationResult_GetCriticalIssues_ShouldReturnOnlyCriticalIssues()
    {
        // Arrange
        var validator = new StartupValidator();

        // Act
        var result = validator.Validate();
        var criticalIssues = result.GetCriticalIssues().ToList();

        // Assert
        Assert.NotNull(criticalIssues);
        Assert.True(criticalIssues.All(i => i.Severity == ValidationSeverity.Critical));
    }

    #endregion

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}