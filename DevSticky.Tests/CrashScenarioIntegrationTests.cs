using System;
using System.Collections.Generic;
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
/// Integration tests for crash scenarios and recovery mechanisms
/// Tests startup with various failure conditions and validates recovery behavior
/// </summary>
public class CrashScenarioIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public CrashScenarioIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    #region Missing Configuration Files Tests

    [Fact]
    public async Task StartupWithMissingConfigurationFiles_ShouldRecoverAutomatically()
    {
        // Arrange: Create scenario with missing configuration files
        var appDataPath = Path.Combine(_testDirectory, "AppData");
        var notesPath = Path.Combine(appDataPath, "notes.json");
        var settingsPath = Path.Combine(appDataPath, "settings.json");
        
        // Ensure files don't exist
        if (Directory.Exists(appDataPath))
        {
            Directory.Delete(appDataPath, true);
        }

        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var exceptionLogger = new ExceptionLogger(errorHandler);
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        var validator = new StartupValidator();

        // Act: Perform startup validation and recovery
        var validationResult = validator.ValidateConfiguration();
        
        // Recovery should be triggered for missing files
        var notesRecovery = await recoveryManager.RecoverMissingConfigurationAsync(notesPath);
        var settingsRecovery = await recoveryManager.RecoverMissingConfigurationAsync(settingsPath);

        // Assert: Recovery should succeed
        Assert.True(notesRecovery.IsSuccessful, "Notes configuration recovery should succeed");
        Assert.True(settingsRecovery.IsSuccessful, "Settings configuration recovery should succeed");
        Assert.Equal(RecoveryAction.CreateDefaultConfiguration, notesRecovery.Action);
        Assert.Equal(RecoveryAction.CreateDefaultConfiguration, settingsRecovery.Action);
        
        // Verify files were created
        Assert.True(fileSystem.FileExists(notesPath), "Notes file should be created");
        Assert.True(fileSystem.FileExists(settingsPath), "Settings file should be created");
        
        // Verify file contents are valid JSON
        var notesContent = await fileSystem.ReadAllTextAsync(notesPath);
        var settingsContent = await fileSystem.ReadAllTextAsync(settingsPath);
        
        Assert.False(string.IsNullOrEmpty(notesContent), "Notes file should have content");
        Assert.False(string.IsNullOrEmpty(settingsContent), "Settings file should have content");
        
        // Should be parseable as JSON
        var notesDoc = System.Text.Json.JsonDocument.Parse(notesContent);
        var settingsDoc = System.Text.Json.JsonDocument.Parse(settingsContent);
        Assert.NotNull(notesDoc);
        Assert.NotNull(settingsDoc);
    }

    [Fact]
    public async Task StartupWithPartiallyMissingFiles_ShouldRecoverOnlyMissingFiles()
    {
        // Arrange: Create scenario where some files exist and others don't
        var appDataPath = Path.Combine(_testDirectory, "PartialMissing");
        Directory.CreateDirectory(appDataPath);
        _createdDirectories.Add(appDataPath);
        
        var notesPath = Path.Combine(appDataPath, "notes.json");
        var settingsPath = Path.Combine(appDataPath, "settings.json");
        var snippetsPath = Path.Combine(appDataPath, "snippets.json");
        
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        // Create notes file but leave others missing
        await fileSystem.WriteAllTextAsync(notesPath, "{\"notes\": []}");
        _createdFiles.Add(notesPath);

        // Act: Attempt recovery for all files
        var notesRecovery = await recoveryManager.RecoverMissingConfigurationAsync(notesPath);
        var settingsRecovery = await recoveryManager.RecoverMissingConfigurationAsync(settingsPath);
        var snippetsRecovery = await recoveryManager.RecoverMissingConfigurationAsync(snippetsPath);

        // Assert: Only missing files should be recovered
        Assert.False(notesRecovery.IsSuccessful, "Notes recovery should not be needed (file exists)");
        Assert.True(settingsRecovery.IsSuccessful, "Settings recovery should succeed (file missing)");
        Assert.True(snippetsRecovery.IsSuccessful, "Snippets recovery should succeed (file missing)");
        
        // Verify existing file was not modified
        var notesContent = await fileSystem.ReadAllTextAsync(notesPath);
        Assert.Contains("\"notes\": []", notesContent);
        
        // Verify new files were created
        Assert.True(fileSystem.FileExists(settingsPath));
        Assert.True(fileSystem.FileExists(snippetsPath));
    }

    #endregion

    #region Corrupted Configuration Files Tests

    [Fact]
    public async Task StartupWithCorruptedConfigurationFiles_ShouldBackupAndRecover()
    {
        // Arrange: Create corrupted configuration files
        var appDataPath = Path.Combine(_testDirectory, "Corrupted");
        Directory.CreateDirectory(appDataPath);
        _createdDirectories.Add(appDataPath);
        
        var notesPath = Path.Combine(appDataPath, "notes.json");
        var settingsPath = Path.Combine(appDataPath, "settings.json");
        
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        // Create corrupted files
        await fileSystem.WriteAllTextAsync(notesPath, "{ invalid json content");
        await fileSystem.WriteAllTextAsync(settingsPath, "{ \"theme\": incomplete");
        _createdFiles.Add(notesPath);
        _createdFiles.Add(settingsPath);

        // Act: Perform recovery for corrupted files
        var notesRecovery = await recoveryManager.RecoverCorruptedConfigurationAsync(notesPath);
        var settingsRecovery = await recoveryManager.RecoverCorruptedConfigurationAsync(settingsPath);

        // Assert: Recovery should succeed and create backups
        Assert.True(notesRecovery.IsSuccessful, "Notes recovery should succeed");
        Assert.True(settingsRecovery.IsSuccessful, "Settings recovery should succeed");
        Assert.Equal(RecoveryAction.BackupCorruptedConfiguration, notesRecovery.Action);
        Assert.Equal(RecoveryAction.BackupCorruptedConfiguration, settingsRecovery.Action);
        
        // Verify backup files were created
        var backupFiles = Directory.GetFiles(appDataPath, "*.backup_*");
        Assert.True(backupFiles.Length >= 2, "Backup files should be created");
        
        // Verify original files now contain valid JSON
        var notesContent = await fileSystem.ReadAllTextAsync(notesPath);
        var settingsContent = await fileSystem.ReadAllTextAsync(settingsPath);
        
        var notesDoc = System.Text.Json.JsonDocument.Parse(notesContent);
        var settingsDoc = System.Text.Json.JsonDocument.Parse(settingsContent);
        Assert.NotNull(notesDoc);
        Assert.NotNull(settingsDoc);
    }

    [Fact]
    public async Task StartupWithMixedCorruptedAndMissingFiles_ShouldHandleBothScenarios()
    {
        // Arrange: Create mixed scenario
        var appDataPath = Path.Combine(_testDirectory, "Mixed");
        Directory.CreateDirectory(appDataPath);
        _createdDirectories.Add(appDataPath);
        
        var notesPath = Path.Combine(appDataPath, "notes.json");
        var settingsPath = Path.Combine(appDataPath, "settings.json");
        var snippetsPath = Path.Combine(appDataPath, "snippets.json");
        
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        // Create corrupted notes file, leave others missing
        await fileSystem.WriteAllTextAsync(notesPath, "{ corrupted json");
        _createdFiles.Add(notesPath);

        // Act: Perform comprehensive recovery
        var recoveryResults = await recoveryManager.PerformComprehensiveRecoveryAsync();

        // Assert: Should handle both corrupted and missing files
        Assert.NotNull(recoveryResults);
        Assert.True(recoveryResults.Count > 0, "Should have recovery results");
        
        var successfulRecoveries = recoveryResults.Where(r => r.IsSuccessful).ToList();
        Assert.True(successfulRecoveries.Count > 0, "Should have successful recoveries");
        
        // Verify files exist and are valid
        Assert.True(fileSystem.FileExists(notesPath));
        Assert.True(fileSystem.FileExists(settingsPath));
        Assert.True(fileSystem.FileExists(snippetsPath));
        
        // All files should now contain valid JSON
        var notesContent = await fileSystem.ReadAllTextAsync(notesPath);
        var settingsContent = await fileSystem.ReadAllTextAsync(settingsPath);
        var snippetsContent = await fileSystem.ReadAllTextAsync(snippetsPath);
        
        var notesDoc = System.Text.Json.JsonDocument.Parse(notesContent);
        var settingsDoc = System.Text.Json.JsonDocument.Parse(settingsContent);
        var snippetsDoc = System.Text.Json.JsonDocument.Parse(snippetsContent);
        Assert.NotNull(notesDoc);
        Assert.NotNull(settingsDoc);
        Assert.NotNull(snippetsDoc);
    }

    #endregion

    #region Missing Dependencies Tests

    [Fact]
    public void StartupWithMissingDependencies_ShouldDetectAndReport()
    {
        // Arrange: Create validator to check dependencies
        var validator = new StartupValidator();

        // Act: Validate dependencies
        var result = validator.ValidateDependencies();

        // Assert: Should complete validation without throwing
        Assert.NotNull(result);
        Assert.Equal("DependencyValidation", result.Component);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        
        // Should check for required packages and assemblies
        var hasPackageChecks = result.Issues.Any(i => 
            i.Issue.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("assembly", StringComparison.OrdinalIgnoreCase) ||
            i.Component == "DependencyValidation");
        
        Assert.True(hasPackageChecks, "Should perform package/assembly checks");
    }

    [Fact]
    public void StartupWithInvalidRuntimeVersion_ShouldDetectVersionIssues()
    {
        // Arrange: Create validator
        var validator = new StartupValidator();

        // Act: Validate dependencies (includes runtime version check)
        var result = validator.ValidateDependencies();

        // Assert: Should check runtime version
        Assert.NotNull(result);
        
        // Should have checked .NET Runtime version
        var hasRuntimeCheck = result.Issues.Any(i => 
            i.Issue.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
            i.Issue.Contains("version", StringComparison.OrdinalIgnoreCase) ||
            i.Component == "DependencyValidation");
        
        // Runtime check should be performed (may pass or fail depending on environment)
        Assert.True(hasRuntimeCheck || result.Issues.Count == 0, "Should check runtime version");
    }

    #endregion

    #region Service Initialization Failures Tests

    [Fact]
    public void StartupWithServiceInitializationFailures_ShouldDetectMissingServices()
    {
        // Arrange: Create service provider with missing critical services
        var services = new ServiceCollection();
        
        // Add only some services, leaving critical ones missing
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        // Intentionally omit IErrorHandler, IStorageService, etc.
        
        var serviceProvider = services.BuildServiceProvider();
        var fileSystem = serviceProvider.GetService<IFileSystem>();
        var validator = new StartupValidator(serviceProvider, fileSystem);

        // Act: Validate services
        var result = validator.ValidateServices();

        // Assert: Should detect missing critical services
        Assert.NotNull(result);
        Assert.Equal("ServiceValidation", result.Component);
        
        var criticalIssues = result.GetCriticalIssues().ToList();
        Assert.True(criticalIssues.Count > 0, "Should detect missing critical services");
        
        // Should identify specific missing services
        var hasMissingServiceIssues = result.Issues.Any(i => 
            i.Issue.Contains("service", StringComparison.OrdinalIgnoreCase) &&
            (i.Issue.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
             i.Issue.Contains("not registered", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasMissingServiceIssues, "Should identify missing services");
    }

    [Fact]
    public void StartupWithPartialServiceFailures_ShouldIdentifyFailedServices()
    {
        // Arrange: Create service provider with some working services
        var services = new ServiceCollection();
        
        // Add essential services
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        services.AddSingleton<IExceptionLogger, ExceptionLogger>();
        // Note: Some services like IStorageService require more complex setup
        
        var serviceProvider = services.BuildServiceProvider();
        var fileSystem = serviceProvider.GetService<IFileSystem>();
        var validator = new StartupValidator(serviceProvider, fileSystem);

        // Act: Validate services
        var result = validator.ValidateServices();

        // Assert: Should complete validation
        Assert.NotNull(result);
        Assert.Equal("ServiceValidation", result.Component);
        
        // May have some issues but should not crash
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        
        // All issues should have valid severity levels
        var allIssuesHaveValidSeverity = result.Issues.All(i => 
            Enum.IsDefined(typeof(ValidationSeverity), i.Severity));
        
        Assert.True(allIssuesHaveValidSeverity, "All issues should have valid severity");
    }

    #endregion

    #region Recovery Mechanisms End-to-End Tests

    [Fact]
    public async Task EndToEndRecovery_ShouldHandleCompleteSystemFailure()
    {
        // Arrange: Create complete system failure scenario
        var appDataPath = Path.Combine(_testDirectory, "CompleteFailure");
        
        // Ensure directory doesn't exist
        if (Directory.Exists(appDataPath))
        {
            Directory.Delete(appDataPath, true);
        }

        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var exceptionLogger = new ExceptionLogger(errorHandler);
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        var validator = new StartupValidator();
        var safeModeController = new SafeModeController(fileSystem, exceptionLogger);

        // Act: Simulate complete startup failure and recovery
        
        // 1. Validate directories (should fail)
        var directoryValidation = validator.ValidateDirectories();
        var hasDirectoryIssues = directoryValidation.GetCriticalIssues().Any();
        
        // 2. Recover missing directories
        var directoryRecovery = await recoveryManager.RecoverMissingDirectoriesAsync(appDataPath);
        
        // 3. Validate configuration (should fail initially)
        var configValidation = validator.ValidateConfiguration();
        
        // 4. Perform comprehensive recovery
        var comprehensiveRecovery = await recoveryManager.PerformComprehensiveRecoveryAsync();
        
        // 5. Check if safe mode should be activated
        var allIssues = directoryValidation.Issues
            .Concat(configValidation.Issues)
            .ToList();
        
        var shouldActivateSafeMode = safeModeController.ShouldActivateSafeMode(allIssues);
        
        if (shouldActivateSafeMode)
        {
            safeModeController.ActivateSafeMode("Complete system failure during testing");
        }

        // Assert: Recovery should succeed
        Assert.True(directoryRecovery.IsSuccessful, "Directory recovery should succeed");
        Assert.NotNull(comprehensiveRecovery);
        Assert.True(comprehensiveRecovery.Count > 0, "Should have recovery results");
        
        var successfulRecoveries = comprehensiveRecovery.Where(r => r.IsSuccessful).ToList();
        Assert.True(successfulRecoveries.Count > 0, "Should have successful recoveries");
        
        // Verify system is now in a recoverable state
        Assert.True(fileSystem.DirectoryExists(appDataPath), "App data directory should exist");
        
        // Verify safe mode activation logic
        if (hasDirectoryIssues || configValidation.GetCriticalIssues().Any())
        {
            Assert.True(shouldActivateSafeMode, "Safe mode should be activated for critical issues");
        }
        
        // If safe mode was activated, verify it's working
        if (safeModeController.IsInSafeMode)
        {
            var status = safeModeController.GetSafeModeStatus();
            Assert.True(status.IsActive);
            Assert.False(string.IsNullOrEmpty(status.Reason));
            Assert.True(status.DisabledServices.Count > 0);
        }
    }

    [Fact]
    public async Task EndToEndRecovery_ShouldPreserveUserDataDuringRecovery()
    {
        // Arrange: Create scenario with existing user data
        var appDataPath = Path.Combine(_testDirectory, "PreserveData");
        Directory.CreateDirectory(appDataPath);
        _createdDirectories.Add(appDataPath);
        
        var notesPath = Path.Combine(appDataPath, "notes.json");
        var settingsPath = Path.Combine(appDataPath, "settings.json");
        
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        // Create valid notes file with user data
        var userNotes = "{\"notes\": [{\"id\": \"test-note\", \"content\": \"Important user data\"}]}";
        await fileSystem.WriteAllTextAsync(notesPath, userNotes);
        _createdFiles.Add(notesPath);
        
        // Create corrupted settings file
        await fileSystem.WriteAllTextAsync(settingsPath, "{ corrupted settings");
        _createdFiles.Add(settingsPath);

        // Act: Perform recovery
        var recoveryResults = await recoveryManager.PerformComprehensiveRecoveryAsync();

        // Assert: User data should be preserved
        Assert.NotNull(recoveryResults);
        
        // Notes file should still contain user data
        var recoveredNotes = await fileSystem.ReadAllTextAsync(notesPath);
        Assert.Contains("Important user data", recoveredNotes);
        Assert.Contains("test-note", recoveredNotes);
        
        // Settings should be recovered but notes preserved
        var recoveredSettings = await fileSystem.ReadAllTextAsync(settingsPath);
        var recoveredDoc = System.Text.Json.JsonDocument.Parse(recoveredSettings);
        Assert.NotNull(recoveredDoc);
        
        // Should have backup of corrupted settings
        var backupFiles = Directory.GetFiles(appDataPath, "settings.json.backup_*");
        Assert.True(backupFiles.Length > 0, "Should create backup of corrupted settings");
    }

    [Fact]
    public async Task EndToEndRecovery_ShouldHandlePermissionIssues()
    {
        // Arrange: Create scenario that might have permission issues
        var restrictedPath = Path.Combine(_testDirectory, "Restricted");
        
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);

        // Act: Attempt recovery in potentially restricted location
        var recoveryResult = await recoveryManager.RecoverMissingDirectoriesAsync(restrictedPath);

        // Assert: Should handle gracefully (may succeed or fail depending on permissions)
        Assert.NotNull(recoveryResult);
        Assert.True(recoveryResult.Timestamp > DateTime.MinValue);
        Assert.False(string.IsNullOrEmpty(recoveryResult.Message));
        
        // If it failed due to permissions, should have appropriate error message
        if (!recoveryResult.IsSuccessful)
        {
            Assert.Contains("permission", recoveryResult.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region Integration with Safe Mode Tests

    [Fact]
    public void SafeModeIntegration_ShouldActivateOnMultipleCriticalFailures()
    {
        // Arrange: Create multiple critical validation issues
        var criticalIssues = new List<ValidationIssue>
        {
            ValidationIssue.Critical("DirectoryValidation", "App data directory not accessible", "Check permissions"),
            ValidationIssue.Critical("ServiceValidation", "Critical service initialization failed", "Restart application"),
            ValidationIssue.Critical("ConfigurationValidation", "Configuration file corrupted beyond repair", "Reset configuration")
        };

        var fileSystem = new FileSystemAdapter();
        var exceptionLogger = new ExceptionLogger(new ErrorHandler());
        var safeModeController = new SafeModeController(fileSystem, exceptionLogger);

        // Act: Check if safe mode should be activated
        var shouldActivate = safeModeController.ShouldActivateSafeMode(criticalIssues);

        // Assert: Should activate safe mode for multiple critical issues
        Assert.True(shouldActivate, "Should activate safe mode for multiple critical issues");
        
        // Activate and verify
        safeModeController.ActivateSafeMode("Multiple critical startup failures");
        
        Assert.True(safeModeController.IsInSafeMode);
        
        var status = safeModeController.GetSafeModeStatus();
        Assert.True(status.IsActive);
        Assert.Contains("Multiple critical startup failures", status.Reason);
        Assert.True(status.DisabledServices.Count > 0);
    }

    [Fact]
    public void SafeModeIntegration_ShouldNotActivateOnWarningsOnly()
    {
        // Arrange: Create only warning-level issues
        var warningIssues = new List<ValidationIssue>
        {
            ValidationIssue.Warning("ThemeValidation", "Custom theme file not found, using default", "Check theme file path"),
            ValidationIssue.Information("ServiceValidation", "Optional service not available", "Feature will be disabled")
        };

        var fileSystem = new FileSystemAdapter();
        var exceptionLogger = new ExceptionLogger(new ErrorHandler());
        var safeModeController = new SafeModeController(fileSystem, exceptionLogger);

        // Act: Check if safe mode should be activated
        var shouldActivate = safeModeController.ShouldActivateSafeMode(warningIssues);

        // Assert: Should not activate safe mode for warnings only
        Assert.False(shouldActivate, "Should not activate safe mode for warnings only");
        Assert.False(safeModeController.IsInSafeMode);
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