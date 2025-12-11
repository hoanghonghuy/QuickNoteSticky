using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for CrashAnalyzer static class
/// Tests crash analysis, cause identification, and recovery suggestions
/// </summary>
public class CrashAnalyzerUnitTests
{
    #region Crash Report Creation Tests

    [Fact]
    public void FromException_WithValidException_ShouldCreateCompleteCrashReport()
    {
        // Arrange
        var exception = new ArgumentNullException("testParam", "Test message");
        var component = "TestComponent";

        // Act
        var crashReport = CrashReport.FromException(exception, component);

        // Assert
        Assert.NotNull(crashReport);
        Assert.Equal(component, crashReport.Component);
        Assert.Equal("ArgumentNullException", crashReport.ExceptionType);
        Assert.Contains("Test message", crashReport.Message);
        Assert.Contains("ArgumentNullException", crashReport.StackTrace);
        Assert.True(crashReport.Timestamp <= DateTime.UtcNow);
        Assert.True(crashReport.Timestamp > DateTime.UtcNow.AddMinutes(-1));
        Assert.False(string.IsNullOrEmpty(crashReport.ApplicationVersion));
        Assert.False(string.IsNullOrEmpty(crashReport.OperatingSystem));
        Assert.False(string.IsNullOrEmpty(crashReport.RuntimeVersion));
        Assert.True(crashReport.MemoryUsageMB >= 0);
        Assert.NotNull(crashReport.Context);
    }

    [Fact]
    public void FromException_WithInnerException_ShouldIncludeInnerExceptionDetails()
    {
        // Arrange
        var innerException = new FileNotFoundException("Inner file not found");
        var outerException = new InvalidOperationException("Outer exception", innerException);
        var component = "TestComponent";

        // Act
        var crashReport = CrashReport.FromException(outerException, component);

        // Assert
        Assert.Contains("InvalidOperationException", crashReport.ExceptionType);
        Assert.Contains("FileNotFoundException", crashReport.StackTrace);
        Assert.Contains("Inner file not found", crashReport.Message);
    }

    [Fact]
    public void FromException_WithNullException_ShouldThrowArgumentNullException()
    {
        // Arrange
        Exception nullException = null!;
        var component = "TestComponent";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CrashReport.FromException(nullException, component));
    }

    [Fact]
    public void FromException_WithEmptyComponent_ShouldUseUnknownComponent()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var emptyComponent = "";

        // Act
        var crashReport = CrashReport.FromException(exception, emptyComponent);

        // Assert
        Assert.Equal("Unknown", crashReport.Component);
    }

    #endregion

    #region Crash Cause Analysis Tests

    [Fact]
    public void AnalyzeCrashCause_WithFileNotFoundException_ShouldIdentifyMissingFileIssue()
    {
        // Arrange
        var exception = new FileNotFoundException("Configuration file not found", "settings.json");
        var crashReport = CrashReport.FromException(exception, "ConfigurationLoader");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.Contains("missing", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeCrashCause_WithDirectoryNotFoundException_ShouldIdentifyMissingDirectoryIssue()
    {
        // Arrange
        var exception = new DirectoryNotFoundException("App data directory not found");
        var crashReport = CrashReport.FromException(exception, "StorageService");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.Contains("directory", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeCrashCause_WithUnauthorizedAccessException_ShouldIdentifyPermissionIssue()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied to configuration file");
        var crashReport = CrashReport.FromException(exception, "FileSystem");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.Contains("permission", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("access", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeCrashCause_WithJsonException_ShouldIdentifyConfigurationCorruption()
    {
        // Arrange
        var exception = new System.Text.Json.JsonException("Invalid JSON format");
        var crashReport = CrashReport.FromException(exception, "ConfigurationService");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.Contains("configuration", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("corrupted", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeCrashCause_WithArgumentNullException_ShouldIdentifyDependencyIssue()
    {
        // Arrange
        var exception = new ArgumentNullException("serviceProvider", "Service provider is null");
        var crashReport = CrashReport.FromException(exception, "DependencyInjection");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.Contains("dependency", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("service", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeCrashCause_WithOutOfMemoryException_ShouldIdentifyMemoryIssue()
    {
        // Arrange
        var exception = new OutOfMemoryException("Insufficient memory");
        var crashReport = CrashReport.FromException(exception, "Application");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.Contains("memory", analysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("insufficient", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeCrashCause_WithUnknownException_ShouldProvideGenericAnalysis()
    {
        // Arrange
        var exception = new CustomTestException("Unknown error type");
        var crashReport = CrashReport.FromException(exception, "UnknownComponent");

        // Act
        var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);

        // Assert
        Assert.NotNull(analysis);
        Assert.False(string.IsNullOrEmpty(analysis));
        Assert.Contains("unknown", analysis, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Recovery Action Suggestions Tests

    [Fact]
    public void GetSuggestedRecoveryActions_WithFileNotFoundException_ShouldSuggestFileRecovery()
    {
        // Arrange
        var exception = new FileNotFoundException("Configuration file not found");
        var crashReport = CrashReport.FromException(exception, "ConfigurationLoader");

        // Act
        var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);

        // Assert
        Assert.NotNull(actions);
        Assert.True(actions.Count > 0);
        
        var hasFileRecoveryAction = actions.Any(a => 
            a.Contains("file", StringComparison.OrdinalIgnoreCase) &&
            (a.Contains("create", StringComparison.OrdinalIgnoreCase) || 
             a.Contains("restore", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasFileRecoveryAction, "Should suggest file recovery action");
        
        var hasSafeModeAction = actions.Any(a => 
            a.Contains("safe mode", StringComparison.OrdinalIgnoreCase));
        
        Assert.True(hasSafeModeAction, "Should suggest safe mode as fallback");
    }

    [Fact]
    public void GetSuggestedRecoveryActions_WithDirectoryNotFoundException_ShouldSuggestDirectoryCreation()
    {
        // Arrange
        var exception = new DirectoryNotFoundException("App data directory not found");
        var crashReport = CrashReport.FromException(exception, "StorageService");

        // Act
        var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);

        // Assert
        Assert.NotNull(actions);
        Assert.True(actions.Count > 0);
        
        var hasDirectoryAction = actions.Any(a => 
            a.Contains("directory", StringComparison.OrdinalIgnoreCase) &&
            a.Contains("create", StringComparison.OrdinalIgnoreCase));
        
        Assert.True(hasDirectoryAction, "Should suggest directory creation");
    }

    [Fact]
    public void GetSuggestedRecoveryActions_WithUnauthorizedAccessException_ShouldSuggestPermissionFix()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");
        var crashReport = CrashReport.FromException(exception, "FileSystem");

        // Act
        var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);

        // Assert
        Assert.NotNull(actions);
        Assert.True(actions.Count > 0);
        
        var hasPermissionAction = actions.Any(a => 
            a.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            a.Contains("administrator", StringComparison.OrdinalIgnoreCase));
        
        Assert.True(hasPermissionAction, "Should suggest permission fix");
    }

    [Fact]
    public void GetSuggestedRecoveryActions_WithJsonException_ShouldSuggestConfigurationReset()
    {
        // Arrange
        var exception = new System.Text.Json.JsonException("Invalid JSON");
        var crashReport = CrashReport.FromException(exception, "ConfigurationService");

        // Act
        var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);

        // Assert
        Assert.NotNull(actions);
        Assert.True(actions.Count > 0);
        
        var hasConfigAction = actions.Any(a => 
            a.Contains("configuration", StringComparison.OrdinalIgnoreCase) &&
            (a.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
             a.Contains("restore", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasConfigAction, "Should suggest configuration reset");
    }

    [Fact]
    public void GetSuggestedRecoveryActions_WithAnyException_ShouldAlwaysIncludeSafeMode()
    {
        // Arrange
        var exceptions = new Exception[]
        {
            new FileNotFoundException("File not found"),
            new DirectoryNotFoundException("Directory not found"),
            new UnauthorizedAccessException("Access denied"),
            new System.Text.Json.JsonException("Invalid JSON"),
            new ArgumentNullException("Null argument"),
            new InvalidOperationException("Invalid operation")
        };

        foreach (var exception in exceptions)
        {
            var crashReport = CrashReport.FromException(exception, "TestComponent");

            // Act
            var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);

            // Assert
            Assert.NotNull(actions);
            Assert.True(actions.Count > 0);
            
            var hasSafeModeAction = actions.Any(a => 
                a.Contains("safe mode", StringComparison.OrdinalIgnoreCase));
            
            Assert.True(hasSafeModeAction, $"Should always suggest safe mode for {exception.GetType().Name}");
        }
    }

    [Fact]
    public void GetSuggestedRecoveryActions_ShouldReturnUniqueActions()
    {
        // Arrange
        var exception = new FileNotFoundException("Configuration file not found");
        var crashReport = CrashReport.FromException(exception, "ConfigurationLoader");

        // Act
        var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);

        // Assert
        Assert.NotNull(actions);
        Assert.True(actions.Count > 0);
        
        var uniqueActions = actions.Distinct().ToList();
        Assert.Equal(actions.Count, uniqueActions.Count);
    }

    #endregion

    #region Event Log Analysis Tests

    [Fact]
    public void AnalyzeCrashesFromEventLog_WithValidHours_ShouldReturnList()
    {
        // Arrange
        var hoursBack = 24;

        // Act
        var crashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(hoursBack);

        // Assert
        Assert.NotNull(crashes);
        // Note: The actual content depends on system state and permissions
        // We just verify it returns a list without throwing
    }

    [Fact]
    public void AnalyzeCrashesFromEventLog_WithZeroHours_ShouldReturnEmptyList()
    {
        // Arrange
        var hoursBack = 0;

        // Act
        var crashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(hoursBack);

        // Assert
        Assert.NotNull(crashes);
        Assert.Empty(crashes);
    }

    [Fact]
    public void AnalyzeCrashesFromEventLog_WithNegativeHours_ShouldReturnEmptyList()
    {
        // Arrange
        var hoursBack = -5;

        // Act
        var crashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(hoursBack);

        // Assert
        Assert.NotNull(crashes);
        Assert.Empty(crashes);
    }

    [Fact]
    public void AnalyzeCrashesFromEventLog_WithLargeHours_ShouldHandleGracefully()
    {
        // Arrange
        var hoursBack = 8760; // 1 year

        // Act
        var crashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(hoursBack);

        // Assert
        Assert.NotNull(crashes);
        // Should not throw, regardless of how many entries exist
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Custom exception for testing unknown exception types
    /// </summary>
    private class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }

    #endregion
}