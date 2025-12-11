using System;
using System.Collections.Generic;
using System.Linq;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for SafeModeController class
/// Tests safe mode activation, deactivation, and service management
/// </summary>
public class SafeModeControllerUnitTests : IDisposable
{
    private readonly List<SafeModeController> _controllers = new();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithoutParameters_ShouldCreateController()
    {
        // Act
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Assert
        Assert.NotNull(controller);
        Assert.False(controller.IsInSafeMode, "Should start in normal mode");
        Assert.NotNull(controller.Configuration);
        Assert.False(controller.Configuration.IsEnabled);
    }

    [Fact]
    public void Constructor_WithFileSystemAndLogger_ShouldCreateController()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var exceptionLogger = new ExceptionLogger(new ErrorHandler());

        // Act
        var controller = new SafeModeController(fileSystem, exceptionLogger);
        _controllers.Add(controller);

        // Assert
        Assert.NotNull(controller);
        Assert.False(controller.IsInSafeMode);
        Assert.NotNull(controller.Configuration);
    }

    [Fact]
    public void Constructor_WithNullParameters_ShouldCreateController()
    {
        // Act
        var controller = new SafeModeController(null, null);
        _controllers.Add(controller);

        // Assert
        Assert.NotNull(controller);
        Assert.False(controller.IsInSafeMode);
    }

    #endregion

    #region ActivateSafeMode Tests

    [Fact]
    public void ActivateSafeMode_WithValidReason_ShouldActivateSafeMode()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var reason = "Test activation reason";

        // Act
        controller.ActivateSafeMode(reason);

        // Assert
        Assert.True(controller.IsInSafeMode, "Safe mode should be activated");
        Assert.True(controller.Configuration.IsEnabled);
        Assert.Equal(reason, controller.Configuration.Reason);
        Assert.True(controller.Configuration.ActivatedAt > DateTime.MinValue);
        Assert.True(controller.Configuration.UseDefaultSettings);
        Assert.True(controller.Configuration.DisableCloudSync);
        Assert.True(controller.Configuration.DisableHotkeys);
        Assert.True(controller.Configuration.DisableMarkdownPreview);
        Assert.True(controller.Configuration.DisableSnippetsAndTemplates);
        Assert.True(controller.Configuration.ShowSafeModeIndicator);
    }

    [Fact]
    public void ActivateSafeMode_WithEmptyReason_ShouldUseDefaultReason()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        controller.ActivateSafeMode("");

        // Assert
        Assert.True(controller.IsInSafeMode);
        Assert.False(string.IsNullOrEmpty(controller.Configuration.Reason));
        Assert.Contains("Unknown", controller.Configuration.Reason);
    }

    [Fact]
    public void ActivateSafeMode_WhenAlreadyActive_ShouldUpdateReason()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        controller.ActivateSafeMode("First reason");

        // Act
        controller.ActivateSafeMode("Second reason");

        // Assert
        Assert.True(controller.IsInSafeMode);
        Assert.Equal("Second reason", controller.Configuration.Reason);
    }

    [Fact]
    public void ActivateSafeMode_ShouldUpdateActivationTimestamp()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var beforeActivation = DateTime.UtcNow;

        // Act
        controller.ActivateSafeMode("Test reason");

        // Assert
        var afterActivation = DateTime.UtcNow;
        Assert.True(controller.Configuration.ActivatedAt >= beforeActivation);
        Assert.True(controller.Configuration.ActivatedAt <= afterActivation);
    }

    #endregion

    #region DeactivateSafeMode Tests

    [Fact]
    public void DeactivateSafeMode_WhenActive_ShouldDeactivateSafeMode()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        controller.ActivateSafeMode("Test reason");

        // Act
        var result = controller.DeactivateSafeMode();

        // Assert
        Assert.True(result, "Deactivation should succeed");
        Assert.False(controller.IsInSafeMode, "Safe mode should be deactivated");
        Assert.False(controller.Configuration.IsEnabled);
        Assert.True(string.IsNullOrEmpty(controller.Configuration.Reason));
    }

    [Fact]
    public void DeactivateSafeMode_WhenNotActive_ShouldReturnFalse()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var result = controller.DeactivateSafeMode();

        // Assert
        Assert.False(result, "Deactivation should return false when not active");
        Assert.False(controller.IsInSafeMode);
    }

    #endregion

    #region ShouldActivateSafeMode Tests

    [Fact]
    public void ShouldActivateSafeMode_WithCriticalIssues_ShouldReturnTrue()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var criticalIssues = new List<ValidationIssue>
        {
            ValidationIssue.Critical("TestComponent", "Critical failure", "Fix immediately"),
            ValidationIssue.Error("TestComponent", "Error occurred", "Fix error")
        };

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(criticalIssues);

        // Assert
        Assert.True(shouldActivate, "Should activate safe mode for critical issues");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithMultipleErrors_ShouldReturnTrue()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var multipleErrors = new List<ValidationIssue>
        {
            ValidationIssue.Error("Component1", "Error 1", "Fix 1"),
            ValidationIssue.Error("Component2", "Error 2", "Fix 2"),
            ValidationIssue.Error("Component3", "Error 3", "Fix 3")
        };

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(multipleErrors);

        // Assert
        Assert.True(shouldActivate, "Should activate safe mode for multiple errors");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithServiceInitializationFailure_ShouldReturnTrue()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var serviceFailures = new List<ValidationIssue>
        {
            ValidationIssue.Error("ServiceValidation", "service initialization failed", "Restart service")
        };

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(serviceFailures);

        // Assert
        Assert.True(shouldActivate, "Should activate safe mode for service initialization failures");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithDependencyInjectionFailure_ShouldReturnTrue()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var diFailures = new List<ValidationIssue>
        {
            ValidationIssue.Error("DIContainer", "dependency injection container failed", "Check registrations")
        };

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(diFailures);

        // Assert
        Assert.True(shouldActivate, "Should activate safe mode for DI failures");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithConfigurationCorruption_ShouldReturnTrue()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var configFailures = new List<ValidationIssue>
        {
            ValidationIssue.Error("ConfigValidation", "configuration corruption detected", "Reset config")
        };

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(configFailures);

        // Assert
        Assert.True(shouldActivate, "Should activate safe mode for configuration corruption");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithWarningsOnly_ShouldReturnFalse()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var warningsOnly = new List<ValidationIssue>
        {
            ValidationIssue.Warning("TestComponent", "Minor warning", "Consider fixing"),
            ValidationIssue.Information("TestComponent", "Info message", "No action needed")
        };

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(warningsOnly);

        // Assert
        Assert.False(shouldActivate, "Should not activate safe mode for warnings only");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithEmptyList_ShouldReturnFalse()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var emptyList = new List<ValidationIssue>();

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(emptyList);

        // Assert
        Assert.False(shouldActivate, "Should not activate safe mode for empty issue list");
    }

    [Fact]
    public void ShouldActivateSafeMode_WithNullList_ShouldReturnFalse()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var shouldActivate = controller.ShouldActivateSafeMode(null!);

        // Assert
        Assert.False(shouldActivate, "Should not activate safe mode for null issue list");
    }

    #endregion

    #region GetEssentialServices Tests

    [Fact]
    public void GetEssentialServices_ShouldReturnRequiredServices()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var essentialServices = controller.GetEssentialServices();

        // Assert
        Assert.NotNull(essentialServices);
        Assert.True(essentialServices.Count > 0, "Should have essential services");
        
        // Should include critical services
        var serviceNames = essentialServices.Select(s => s.Name).ToList();
        Assert.Contains("IFileSystem", serviceNames);
        Assert.Contains("IErrorHandler", serviceNames);
        Assert.Contains("IExceptionLogger", serviceNames);
        Assert.Contains("IStorageService", serviceNames);
        Assert.Contains("INoteService", serviceNames);
        Assert.Contains("IThemeService", serviceNames);
    }

    [Fact]
    public void GetEssentialServices_ShouldReturnUniqueServices()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var essentialServices = controller.GetEssentialServices();

        // Assert
        var uniqueServices = essentialServices.Distinct().ToList();
        Assert.Equal(essentialServices.Count, uniqueServices.Count);
    }

    #endregion

    #region GetNonEssentialServices Tests

    [Fact]
    public void GetNonEssentialServices_ShouldReturnOptionalServices()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var nonEssentialServices = controller.GetNonEssentialServices();

        // Assert
        Assert.NotNull(nonEssentialServices);
        Assert.True(nonEssentialServices.Count > 0, "Should have non-essential services");
        
        // Should include optional services
        var serviceNames = nonEssentialServices.Select(s => s.Name).ToList();
        Assert.Contains("ICloudSyncService", serviceNames);
        Assert.Contains("IHotkeyService", serviceNames);
        Assert.Contains("IMarkdownService", serviceNames);
        Assert.Contains("ISnippetService", serviceNames);
        Assert.Contains("ITemplateService", serviceNames);
    }

    [Fact]
    public void GetNonEssentialServices_ShouldNotOverlapWithEssentialServices()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var essentialServices = controller.GetEssentialServices();
        var nonEssentialServices = controller.GetNonEssentialServices();

        // Assert
        var overlap = essentialServices.Intersect(nonEssentialServices).ToList();
        Assert.Empty(overlap);
    }

    #endregion

    #region ConfigureMinimalServices Tests

    [Fact]
    public void ConfigureMinimalServices_WithValidServiceProvider_ShouldComplete()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should not throw
        controller.ConfigureMinimalServices(serviceProvider);
    }

    [Fact]
    public void ConfigureMinimalServices_WithNullServiceProvider_ShouldHandleGracefully()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act & Assert - Should not throw
        controller.ConfigureMinimalServices(null!);
    }

    #endregion

    #region ResetConfigurationToDefaults Tests

    [Fact]
    public void ResetConfigurationToDefaults_ShouldResetToDefaults()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act & Assert - Should not throw
        controller.ResetConfigurationToDefaults();
    }

    #endregion

    #region GetSafeModeStatus Tests

    [Fact]
    public void GetSafeModeStatus_WhenNotActive_ShouldReturnInactiveStatus()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var status = controller.GetSafeModeStatus();

        // Assert
        Assert.NotNull(status);
        Assert.False(status.IsActive);
        Assert.True(string.IsNullOrEmpty(status.Reason));
        Assert.Null(status.ActivatedAt);
        Assert.Empty(status.DisabledServices);
        Assert.Empty(status.StartupFailures);
        Assert.False(status.ConfigurationReset);
    }

    [Fact]
    public void GetSafeModeStatus_WhenActive_ShouldReturnActiveStatus()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var reason = "Test activation";
        controller.ActivateSafeMode(reason);

        // Act
        var status = controller.GetSafeModeStatus();

        // Assert
        Assert.NotNull(status);
        Assert.True(status.IsActive);
        Assert.Equal(reason, status.Reason);
        Assert.NotNull(status.ActivatedAt);
        Assert.True(status.DisabledServices.Count > 0);
        Assert.True(status.ConfigurationReset);
        
        // Should list disabled non-essential services
        var nonEssentialServices = controller.GetNonEssentialServices();
        var expectedDisabledServices = nonEssentialServices.Select(s => s.Name).ToList();
        
        foreach (var expectedService in expectedDisabledServices)
        {
            Assert.Contains(expectedService, status.DisabledServices);
        }
    }

    #endregion

    #region Configuration Property Tests

    [Fact]
    public void Configuration_ShouldReturnCurrentConfiguration()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        var config = controller.Configuration;

        // Assert
        Assert.NotNull(config);
        Assert.False(config.IsEnabled);
        Assert.True(string.IsNullOrEmpty(config.Reason));
    }

    [Fact]
    public void Configuration_AfterActivation_ShouldReflectChanges()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        var reason = "Test configuration";

        // Act
        controller.ActivateSafeMode(reason);
        var config = controller.Configuration;

        // Assert
        Assert.NotNull(config);
        Assert.True(config.IsEnabled);
        Assert.Equal(reason, config.Reason);
        Assert.True(config.UseDefaultSettings);
        Assert.True(config.DisableCloudSync);
        Assert.True(config.DisableHotkeys);
        Assert.True(config.DisableMarkdownPreview);
        Assert.True(config.DisableSnippetsAndTemplates);
        Assert.True(config.ShowSafeModeIndicator);
    }

    #endregion

    #region IsInSafeMode Property Tests

    [Fact]
    public void IsInSafeMode_InitialState_ShouldBeFalse()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act & Assert
        Assert.False(controller.IsInSafeMode);
    }

    [Fact]
    public void IsInSafeMode_AfterActivation_ShouldBeTrue()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);

        // Act
        controller.ActivateSafeMode("Test");

        // Assert
        Assert.True(controller.IsInSafeMode);
    }

    [Fact]
    public void IsInSafeMode_AfterDeactivation_ShouldBeFalse()
    {
        // Arrange
        var controller = new SafeModeController();
        _controllers.Add(controller);
        controller.ActivateSafeMode("Test");

        // Act
        controller.DeactivateSafeMode();

        // Assert
        Assert.False(controller.IsInSafeMode);
    }

    #endregion

    public void Dispose()
    {
        foreach (var controller in _controllers)
        {
            try
            {
                controller?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
    }
}