# DevSticky Crash Handling Developer Guide

This guide provides detailed information for developers working with DevSticky's crash handling and recovery systems.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Core Components](#core-components)
- [Implementation Details](#implementation-details)
- [Testing Strategy](#testing-strategy)
- [Extension Points](#extension-points)
- [Best Practices](#best-practices)
- [Debugging and Diagnostics](#debugging-and-diagnostics)

## Architecture Overview

The crash handling system is built on several key principles:

1. **Proactive Detection**: Identify issues before they cause crashes
2. **Comprehensive Logging**: Capture detailed information for analysis
3. **Automatic Recovery**: Fix common issues without user intervention
4. **Graceful Degradation**: Provide safe mode when normal operation fails
5. **User Guidance**: Offer clear recovery options and troubleshooting steps

### System Layers

```
┌─────────────────────────────────────────┐
│           Application Layer             │
│  (Startup Sequence, Main Application)   │
├─────────────────────────────────────────┤
│         Crash Handling Layer           │
│  (Detection, Analysis, Recovery)        │
├─────────────────────────────────────────┤
│         Diagnostic Layer                │
│  (Logging, Monitoring, Reporting)       │
├─────────────────────────────────────────┤
│         Infrastructure Layer            │
│  (File System, Event Logs, Services)   │
└─────────────────────────────────────────┘
```

## Core Components

### CrashAnalyzer

Static class responsible for analyzing crashes and extracting meaningful information.

```csharp
public static class CrashAnalyzer
{
    public static async Task<CrashReport> AnalyzeCrashAsync(Exception exception, string context = "")
    {
        var report = new CrashReport
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.ToString(),
            Context = context,
            Component = ExtractComponentFromStackTrace(exception.StackTrace),
            Metadata = CollectSystemMetadata(),
            Severity = DetermineSeverity(exception)
        };

        await LogToEventLogAsync(report);
        await SaveCrashReportAsync(report);
        
        return report;
    }

    private static string ExtractComponentFromStackTrace(string stackTrace)
    {
        // Analyze stack trace to identify the component that failed
        // Look for DevSticky namespace patterns
        // Return the most likely component name
    }

    private static Dictionary<string, object> CollectSystemMetadata()
    {
        return new Dictionary<string, object>
        {
            ["OSVersion"] = Environment.OSVersion.ToString(),
            ["CLRVersion"] = Environment.Version.ToString(),
            ["MachineName"] = Environment.MachineName,
            ["UserName"] = Environment.UserName,
            ["WorkingSet"] = Environment.WorkingSet,
            ["ProcessorCount"] = Environment.ProcessorCount,
            ["TickCount"] = Environment.TickCount64
        };
    }
}
```

### StartupValidator

Validates all prerequisites and dependencies during application startup.

```csharp
public class StartupValidator : IStartupValidator
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<StartupValidator> _logger;
    private readonly IServiceProvider _serviceProvider;

    public async Task<ValidationResult> ValidateAsync()
    {
        var result = new ValidationResult { Issues = new List<ValidationIssue>() };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate in order of dependency
            await ValidateDirectoriesAsync(result);
            await ValidateConfigurationAsync(result);
            await ValidateDependenciesAsync(result);
            await ValidateServicesAsync(result);
            await ValidateResourcesAsync(result);

            result.IsValid = !result.Issues.Any(i => i.Severity >= ValidationSeverity.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation process failed");
            result.Issues.Add(new ValidationIssue
            {
                Component = "StartupValidator",
                Issue = $"Validation process failed: {ex.Message}",
                Severity = ValidationSeverity.Critical,
                SuggestedAction = "Restart application or contact support"
            });
        }
        finally
        {
            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;
            result.ValidationTime = DateTime.UtcNow;
        }

        return result;
    }

    private async Task ValidateDirectoriesAsync(ValidationResult result)
    {
        var requiredDirectories = new[]
        {
            AppConstants.DataDirectory,
            AppConstants.LogDirectory,
            AppConstants.CacheDirectory,
            AppConstants.BackupDirectory
        };

        foreach (var directory in requiredDirectories)
        {
            if (!Directory.Exists(directory))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Component = "FileSystem",
                    Issue = $"Required directory missing: {directory}",
                    Severity = ValidationSeverity.Error,
                    SuggestedAction = "Create directory structure",
                    IsAutoFixable = true,
                    Details = new Dictionary<string, object> { ["Path"] = directory }
                });
            }
            else if (!HasWriteAccess(directory))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Component = "FileSystem",
                    Issue = $"No write access to directory: {directory}",
                    Severity = ValidationSeverity.Critical,
                    SuggestedAction = "Check directory permissions",
                    IsAutoFixable = false
                });
            }
        }
    }
}
```

### RecoveryManager

Handles automatic recovery from common issues.

```csharp
public class RecoveryManager : IRecoveryManager
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<RecoveryManager> _logger;
    private readonly IServiceProvider _serviceProvider;

    public async Task<RecoveryResult> AttemptRecoveryAsync(ValidationResult validationResult)
    {
        var recoveryResult = new RecoveryResult();
        var autoFixableIssues = validationResult.Issues.Where(i => i.IsAutoFixable).ToList();

        foreach (var issue in autoFixableIssues)
        {
            try
            {
                var success = await ExecuteRecoveryActionAsync(issue);
                recoveryResult.Actions.Add(new RecoveryActionResult
                {
                    Issue = issue,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                });

                if (success)
                {
                    _logger.LogInformation("Successfully recovered from issue: {Issue}", issue.Issue);
                }
                else
                {
                    _logger.LogWarning("Failed to recover from issue: {Issue}", issue.Issue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery action failed for issue: {Issue}", issue.Issue);
                recoveryResult.Actions.Add(new RecoveryActionResult
                {
                    Issue = issue,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        recoveryResult.OverallSuccess = recoveryResult.Actions.All(a => a.Success);
        return recoveryResult;
    }

    private async Task<bool> ExecuteRecoveryActionAsync(ValidationIssue issue)
    {
        return issue.Component switch
        {
            "FileSystem" when issue.Issue.Contains("directory missing") => 
                await CreateMissingDirectoryAsync(issue),
            "Configuration" when issue.Issue.Contains("file missing") => 
                await CreateDefaultConfigurationAsync(issue),
            "Configuration" when issue.Issue.Contains("corrupted") => 
                await RepairCorruptedConfigurationAsync(issue),
            "Services" when issue.Issue.Contains("failed to register") => 
                await RestartFailedServiceAsync(issue),
            _ => false
        };
    }
}
```

### SafeModeController

Manages safe mode startup and configuration.

```csharp
public class SafeModeController : ISafeModeController
{
    private readonly ILogger<SafeModeController> _logger;
    private readonly IServiceCollection _services;
    private bool _isSafeModeActive;

    public bool IsSafeModeActive => _isSafeModeActive;

    public async Task<bool> StartSafeModeAsync()
    {
        try
        {
            _logger.LogInformation("Starting Safe Mode");
            _isSafeModeActive = true;

            // Register only essential services
            await RegisterMinimalServicesAsync(_services);

            // Use default configuration
            var safeModeConfig = await GetSafeModeConfigAsync();
            ApplySafeModeConfiguration(safeModeConfig);

            _logger.LogInformation("Safe Mode started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Safe Mode");
            return false;
        }
    }

    public async Task RegisterMinimalServicesAsync(IServiceCollection services)
    {
        // Register only essential services for safe mode
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        services.AddSingleton<IExceptionLogger, ExceptionLogger>();
        
        // Use fallback implementations for complex services
        services.AddSingleton<IStorageService, SafeModeStorageService>();
        services.AddSingleton<INoteService, SafeModeNoteService>();
        
        // Disable cloud sync and other advanced features
        // services.AddSingleton<ICloudSyncService, DisabledCloudSyncService>();
    }

    private void ApplySafeModeConfiguration(SafeModeConfig config)
    {
        // Apply safe mode settings
        // Disable non-essential features
        // Use default theme and settings
        // Limit functionality to core operations
    }
}
```

## Implementation Details

### Exception Handling Flow

1. **Exception Occurs**: Any unhandled exception in the application
2. **Context Collection**: Gather information about the current operation
3. **Stack Trace Analysis**: Identify the component and method that failed
4. **Severity Assessment**: Determine the impact level of the exception
5. **Logging**: Write to both file log and Windows Event Log
6. **Recovery Decision**: Determine if automatic recovery is possible
7. **User Notification**: Show appropriate error dialog or recovery options

### Startup Validation Process

1. **Directory Structure**: Verify all required directories exist and are writable
2. **Configuration Files**: Check JSON syntax and required properties
3. **Dependencies**: Verify DLLs and NuGet packages are available
4. **Service Registration**: Test dependency injection container setup
5. **Resource Loading**: Confirm themes and resources are accessible
6. **Performance Check**: Measure validation overhead

### Recovery Action Types

```csharp
public enum RecoveryActionType
{
    ConfigurationReset,     // Reset config files to defaults
    FileRecreation,         // Recreate missing files
    DirectoryCreation,      // Create missing directories
    ServiceRestart,         // Restart failed services
    BackupRestore,          // Restore from backup
    CacheClear,            // Clear corrupted cache
    IndexRebuild           // Rebuild search index
}
```

### Safe Mode Service Registration

Safe Mode uses a minimal set of services to ensure stability:

```csharp
// Essential services (always registered)
services.AddSingleton<IFileSystem, FileSystemAdapter>();
services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
services.AddSingleton<IErrorHandler, ErrorHandler>();

// Fallback services (simplified implementations)
services.AddSingleton<IStorageService, SafeModeStorageService>();
services.AddSingleton<INoteService, SafeModeNoteService>();

// Disabled services (not registered in safe mode)
// ICloudSyncService - Cloud sync disabled
// IHotkeyService - Global hotkeys disabled
// IThemeService - Uses default theme only
```

## Testing Strategy

### Property-Based Testing

The crash handling system uses property-based testing to verify correctness across many scenarios:

```csharp
[Property]
public bool CrashAnalysis_AlwaysProducesValidReport(Exception exception, string context)
{
    var report = CrashAnalyzer.AnalyzeCrash(exception, context);
    
    return report != null &&
           report.Id != Guid.Empty &&
           report.Timestamp > DateTime.MinValue &&
           !string.IsNullOrEmpty(report.ExceptionType) &&
           !string.IsNullOrEmpty(report.Message);
}

[Property]
public bool StartupValidation_NeverThrowsExceptions(ValidationScenario scenario)
{
    try
    {
        var validator = new StartupValidator(scenario.FileSystem, scenario.Logger, scenario.ServiceProvider);
        var result = validator.ValidateAsync().Result;
        return true; // If we get here, no exception was thrown
    }
    catch
    {
        return false; // Exception thrown, property violated
    }
}

[Property]
public bool RecoveryActions_PreserveDataIntegrity(ValidationIssue issue)
{
    var originalData = CaptureCurrentState();
    var recoveryManager = new RecoveryManager();
    
    var result = recoveryManager.AttemptRecoveryAsync(new ValidationResult { Issues = { issue } }).Result;
    var newData = CaptureCurrentState();
    
    // Recovery should fix the issue without losing existing data
    return DataIntegrityPreserved(originalData, newData);
}
```

### Unit Testing

```csharp
[Test]
public async Task StartupValidator_DetectsMissingDirectory()
{
    // Arrange
    var mockFileSystem = new Mock<IFileSystem>();
    mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
    
    var validator = new StartupValidator(mockFileSystem.Object, Mock.Of<ILogger<StartupValidator>>(), Mock.Of<IServiceProvider>());
    
    // Act
    var result = await validator.ValidateDirectoriesAsync();
    
    // Assert
    Assert.That(result.Issues, Has.Some.Matches<ValidationIssue>(i => 
        i.Component == "FileSystem" && 
        i.Issue.Contains("directory missing") &&
        i.IsAutoFixable));
}

[Test]
public async Task RecoveryManager_CreatesDefaultConfiguration()
{
    // Arrange
    var mockFileSystem = new Mock<IFileSystem>();
    var recoveryManager = new RecoveryManager(mockFileSystem.Object, Mock.Of<ILogger<RecoveryManager>>(), Mock.Of<IServiceProvider>());
    
    var issue = new ValidationIssue
    {
        Component = "Configuration",
        Issue = "Configuration file missing: config.json",
        IsAutoFixable = true
    };
    
    // Act
    var result = await recoveryManager.AttemptRecoveryAsync(new ValidationResult { Issues = { issue } });
    
    // Assert
    Assert.That(result.OverallSuccess, Is.True);
    mockFileSystem.Verify(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
}
```

### Integration Testing

```csharp
[Test]
public async Task CrashHandling_EndToEndRecovery()
{
    // Arrange: Create a scenario with multiple issues
    var testDirectory = CreateTemporaryTestDirectory();
    DeleteConfigurationFiles(testDirectory);
    
    // Act: Start application (should trigger crash handling)
    var app = new TestApplication(testDirectory);
    var startupResult = await app.StartAsync();
    
    // Assert: Application should start in safe mode and offer recovery
    Assert.That(startupResult.IsSafeModeActive, Is.True);
    Assert.That(startupResult.RecoveryOptionsAvailable, Is.True);
    
    // Act: Apply automatic recovery
    var recoveryResult = await app.ApplyAutomaticRecoveryAsync();
    
    // Assert: Recovery should succeed
    Assert.That(recoveryResult.Success, Is.True);
    
    // Act: Restart normally
    await app.RestartAsync();
    
    // Assert: Should start normally after recovery
    Assert.That(app.IsSafeModeActive, Is.False);
    Assert.That(app.IsRunning, Is.True);
}
```

## Extension Points

### Custom Recovery Actions

You can add custom recovery actions by implementing the recovery action interface:

```csharp
public class CustomRecoveryAction : IRecoveryAction
{
    public string Name => "Custom Database Repair";
    public string Description => "Repairs corrupted database files";
    public RecoveryActionType Type => RecoveryActionType.Custom;
    public RecoveryRiskLevel RiskLevel => RecoveryRiskLevel.Medium;
    public bool RequiresUserConfirmation => true;

    public async Task<bool> ExecuteAsync()
    {
        // Implement custom recovery logic
        try
        {
            await RepairDatabaseAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Custom recovery action failed");
            return false;
        }
    }
}

// Register the custom action
services.AddTransient<IRecoveryAction, CustomRecoveryAction>();
```

### Custom Validators

Add custom validation logic by implementing the validator interface:

```csharp
public class DatabaseValidator : IStartupValidator
{
    public async Task<ValidationResult> ValidateAsync()
    {
        var result = new ValidationResult { Issues = new List<ValidationIssue>() };
        
        // Check database connectivity
        if (!await CanConnectToDatabaseAsync())
        {
            result.Issues.Add(new ValidationIssue
            {
                Component = "Database",
                Issue = "Cannot connect to database",
                Severity = ValidationSeverity.Error,
                SuggestedAction = "Check database connection string",
                IsAutoFixable = false
            });
        }
        
        result.IsValid = !result.Issues.Any(i => i.Severity >= ValidationSeverity.Error);
        return result;
    }
}

// Register the custom validator
services.AddTransient<IStartupValidator, DatabaseValidator>();
```

### Custom Safe Mode Services

Provide alternative implementations for safe mode:

```csharp
public class SafeModeNoteService : INoteService
{
    // Simplified implementation for safe mode
    // Limited functionality, but guaranteed to work
    
    public async Task<Note> CreateNoteAsync(string content = null, NoteGroup group = null)
    {
        // Create note with minimal validation and no advanced features
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Content = content ?? "",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
            // No group assignment, tags, or other complex features in safe mode
        };
        
        await SaveNoteAsync(note);
        return note;
    }
    
    // Other methods with simplified implementations...
}

// Register for safe mode use
services.AddSingleton<INoteService, SafeModeNoteService>();
```

## Best Practices

### Exception Handling

1. **Always Provide Context**: Include operation details when logging exceptions
2. **Use Structured Logging**: Include relevant metadata for analysis
3. **Categorize by Severity**: Distinguish between recoverable and critical errors
4. **Preserve Stack Traces**: Don't lose information when re-throwing exceptions

```csharp
try
{
    await SaveNoteAsync(note);
}
catch (Exception ex)
{
    await _exceptionLogger.LogExceptionAsync(ex, new Dictionary<string, object>
    {
        ["Operation"] = "SaveNote",
        ["NoteId"] = note.Id,
        ["UserId"] = _currentUser.Id,
        ["Timestamp"] = DateTime.UtcNow
    });
    
    // Re-throw with additional context
    throw new NoteOperationException($"Failed to save note {note.Id}", ex);
}
```

### Validation Design

1. **Fail Fast**: Validate early in the startup process
2. **Provide Actionable Feedback**: Include specific steps to resolve issues
3. **Support Automatic Recovery**: Mark issues as auto-fixable when possible
4. **Measure Performance**: Track validation overhead

```csharp
public async Task<ValidationResult> ValidateConfigurationAsync()
{
    var issues = new List<ValidationIssue>();
    
    foreach (var configFile in GetRequiredConfigFiles())
    {
        if (!File.Exists(configFile.Path))
        {
            issues.Add(new ValidationIssue
            {
                Component = "Configuration",
                Issue = $"Configuration file missing: {configFile.Name}",
                Severity = ValidationSeverity.Error,
                SuggestedAction = $"Create default {configFile.Name} file",
                IsAutoFixable = true,
                Details = new Dictionary<string, object>
                {
                    ["FilePath"] = configFile.Path,
                    ["DefaultContent"] = configFile.DefaultContent
                }
            });
        }
    }
    
    return new ValidationResult { Issues = issues, IsValid = !issues.Any(i => i.Severity >= ValidationSeverity.Error) };
}
```

### Recovery Implementation

1. **Risk Assessment**: Clearly communicate the risk level of recovery actions
2. **Backup Before Changes**: Always backup before making modifications
3. **Atomic Operations**: Ensure recovery actions are all-or-nothing
4. **User Confirmation**: Require confirmation for high-risk actions

```csharp
public async Task<bool> ResetConfigurationAsync()
{
    // Create backup before making changes
    var backupPath = await CreateConfigurationBackupAsync();
    
    try
    {
        // Atomic operation: delete old and create new
        await DeleteExistingConfigurationAsync();
        await CreateDefaultConfigurationAsync();
        
        Logger.LogInformation("Configuration reset successfully. Backup saved to: {BackupPath}", backupPath);
        return true;
    }
    catch (Exception ex)
    {
        // Restore from backup on failure
        await RestoreConfigurationFromBackupAsync(backupPath);
        Logger.LogError(ex, "Configuration reset failed. Restored from backup.");
        return false;
    }
}
```

## Debugging and Diagnostics

### Logging Configuration

Configure detailed logging for crash handling components:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DevSticky.CrashHandling": "Debug",
      "DevSticky.Recovery": "Debug",
      "DevSticky.Validation": "Debug"
    },
    "Console": {
      "IncludeScopes": true
    },
    "File": {
      "Path": "logs/devsticky-{Date}.log",
      "IncludeScopes": true,
      "OutputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
    }
  }
}
```

### Diagnostic Commands

Use these commands for debugging crash handling:

```bash
# Enable verbose crash handling logging
set DEVSTICKY_CRASH_LOGGING=Verbose

# Force safe mode for testing
set DEVSTICKY_FORCE_SAFE_MODE=1

# Disable automatic recovery for debugging
set DEVSTICKY_NO_AUTO_RECOVERY=1

# Export diagnostic information
DevSticky.exe --export-diagnostics

# Validate configuration without starting
DevSticky.exe --validate-only
```

### Performance Monitoring

Monitor crash handling performance:

```csharp
public class CrashHandlingMetrics
{
    private static readonly Counter CrashCount = Metrics
        .CreateCounter("devsticky_crashes_total", "Total number of crashes");
    
    private static readonly Histogram ValidationDuration = Metrics
        .CreateHistogram("devsticky_validation_duration_seconds", "Startup validation duration");
    
    private static readonly Counter RecoveryAttempts = Metrics
        .CreateCounter("devsticky_recovery_attempts_total", "Recovery attempts", "action_type", "success");
    
    public static void RecordCrash(string component, CrashSeverity severity)
    {
        CrashCount.WithLabels(component, severity.ToString()).Inc();
    }
    
    public static void RecordValidationDuration(TimeSpan duration)
    {
        ValidationDuration.Observe(duration.TotalSeconds);
    }
    
    public static void RecordRecoveryAttempt(RecoveryActionType actionType, bool success)
    {
        RecoveryAttempts.WithLabels(actionType.ToString(), success.ToString()).Inc();
    }
}
```

This developer guide provides comprehensive information for working with DevSticky's crash handling system. The architecture is designed to be extensible, testable, and maintainable while providing robust error recovery capabilities.