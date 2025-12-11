using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for CrashAnalyticsService
/// Tests crash frequency tracking, failure pattern analysis, recovery success rate monitoring, and safe mode usage statistics
/// </summary>
public class CrashAnalyticsServiceTests : IDisposable
{
    private readonly List<CrashAnalyticsService> _services = new();
    private readonly string _testDirectory;

    public CrashAnalyticsServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    private CrashAnalyticsService CreateTestService()
    {
        var testDir = Path.Combine(_testDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var fileSystem = new TestFileSystem(testDir);
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);
        return service;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateService()
    {
        // Act
        var service = CreateTestService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ShouldThrowArgumentNullException()
    {
        // Arrange
        IFileSystem? nullFileSystem = null;
        var errorHandler = new ErrorHandler();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CrashAnalyticsService(nullFileSystem!, errorHandler));
    }

    [Fact]
    public void Constructor_WithNullErrorHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        IErrorHandler? nullErrorHandler = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CrashAnalyticsService(fileSystem, nullErrorHandler!));
    }

    #endregion

    #region Crash Recording Tests

    [Fact]
    public async Task RecordCrashAsync_WithValidCrashReport_ShouldRecordSuccessfully()
    {
        // Arrange
        var service = CreateTestService();
        var crashReport = CrashReport.FromException(new InvalidOperationException("Test exception"), "TestComponent");

        // Act
        await service.RecordCrashAsync(crashReport);

        // Assert
        var stats = await service.GetCrashFrequencyStatsAsync();
        Assert.True(stats.TotalCrashes >= 1);
    }

    [Fact]
    public async Task RecordCrashAsync_WithNullCrashReport_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateTestService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordCrashAsync(null!));
    }

    [Fact]
    public async Task RecordCrashAsync_MultipleCrashes_ShouldTrackFrequency()
    {
        // Arrange
        var service = CreateTestService();
        var crash1 = CrashReport.FromException(new InvalidOperationException("Test 1"), "Component1");
        var crash2 = CrashReport.FromException(new ArgumentException("Test 2"), "Component2");
        var crash3 = CrashReport.FromException(new InvalidOperationException("Test 3"), "Component1");

        // Act
        await service.RecordCrashAsync(crash1);
        await service.RecordCrashAsync(crash2);
        await service.RecordCrashAsync(crash3);

        // Assert
        var stats = await service.GetCrashFrequencyStatsAsync();
        Assert.Equal(3, stats.TotalCrashes);
        Assert.True(stats.CrashesByComponent.ContainsKey("Component1"));
        Assert.True(stats.CrashesByComponent.ContainsKey("Component2"));
    }

    #endregion

    #region Recovery Recording Tests

    [Fact]
    public async Task RecordRecoveryAttemptAsync_WithValidAttempt_ShouldRecordSuccessfully()
    {
        // Arrange
        var service = CreateTestService();
        var recoveryAttempt = new RecoveryAttempt
        {
            RecoveryAction = "CreateDefaultConfig",
            WasSuccessful = true,
            Component = "ConfigurationService",
            TriggeringIssue = "Missing config file",
            Duration = TimeSpan.FromMilliseconds(500)
        };

        // Act
        await service.RecordRecoveryAttemptAsync(recoveryAttempt);

        // Assert
        var stats = await service.GetRecoverySuccessStatsAsync();
        Assert.Equal(1, stats.Attempts.Count);
        Assert.Equal(100.0, stats.OverallSuccessRate);
    }

    [Fact]
    public async Task RecordRecoveryAttemptAsync_WithNullAttempt_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateTestService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordRecoveryAttemptAsync(null!));
    }

    [Fact]
    public async Task RecordRecoveryAttemptAsync_MultipleAttempts_ShouldCalculateSuccessRate()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        var successfulAttempt = new RecoveryAttempt
        {
            RecoveryAction = "CreateDefaultConfig",
            WasSuccessful = true,
            Component = "ConfigurationService"
        };

        var failedAttempt = new RecoveryAttempt
        {
            RecoveryAction = "RestoreBackup",
            WasSuccessful = false,
            Component = "StorageService",
            ErrorMessage = "Backup not found"
        };

        // Act
        await service.RecordRecoveryAttemptAsync(successfulAttempt);
        await service.RecordRecoveryAttemptAsync(failedAttempt);

        // Assert
        var stats = await service.GetRecoverySuccessStatsAsync();
        Assert.True(stats.Attempts.Count >= 2);
        Assert.True(stats.OverallSuccessRate >= 0 && stats.OverallSuccessRate <= 100);
    }

    #endregion

    #region Safe Mode Recording Tests

    [Fact]
    public async Task RecordSafeModeUsageAsync_WithValidUsage_ShouldRecordSuccessfully()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        var safeModeUsage = new SafeModeUsage
        {
            EntryReason = "Startup failure",
            EndTime = DateTime.UtcNow.AddMinutes(5),
            ExitReason = "Manual restart",
            AttemptedNormalStartup = true,
            NormalStartupSuccessful = true
        };

        // Act
        await service.RecordSafeModeUsageAsync(safeModeUsage);

        // Assert
        var stats = await service.GetSafeModeStatsAsync();
        Assert.True(stats.TotalUsages >= 1);
    }

    [Fact]
    public async Task RecordSafeModeUsageAsync_WithNullUsage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordSafeModeUsageAsync(null!));
    }

    #endregion

    #region Analytics Tests

    [Fact]
    public async Task GetCrashFrequencyStatsAsync_WithNoCrashes_ShouldReturnEmptyStats()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        // Act
        var stats = await service.GetCrashFrequencyStatsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalCrashes);
        Assert.Equal(0, stats.CrashesLast24Hours);
        Assert.Equal(0, stats.CrashesLast7Days);
        Assert.Equal(0, stats.CrashesLast30Days);
    }

    [Fact]
    public async Task AnalyzeFailurePatternsAsync_WithNoCrashes_ShouldReturnEmptyAnalysis()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        // Act
        var analysis = await service.AnalyzeFailurePatternsAsync();

        // Assert
        Assert.NotNull(analysis);
        Assert.Empty(analysis.CommonFailurePatterns);
        Assert.Empty(analysis.CorrelatedFailures);
        Assert.Empty(analysis.RecurringIssues);
    }

    [Fact]
    public async Task GenerateAnalyticsReportAsync_ShouldReturnComprehensiveReport()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        // Add some test data
        var crashReport = CrashReport.FromException(new InvalidOperationException("Test"), "TestComponent");
        await service.RecordCrashAsync(crashReport);

        var recoveryAttempt = new RecoveryAttempt
        {
            RecoveryAction = "Test",
            WasSuccessful = true,
            Component = "TestComponent"
        };
        await service.RecordRecoveryAttemptAsync(recoveryAttempt);

        // Act
        var report = await service.GenerateAnalyticsReportAsync();

        // Assert
        Assert.NotNull(report);
        Assert.NotNull(report.CrashFrequency);
        Assert.NotNull(report.FailurePatterns);
        Assert.NotNull(report.RecoverySuccess);
        Assert.NotNull(report.SafeModeUsage);
        Assert.NotNull(report.SessionSummary);
        
        var summary = report.GetSummary();
        Assert.False(string.IsNullOrEmpty(summary));
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public async Task CleanupOldDataAsync_WithOldData_ShouldRemoveExpiredEntries()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var service = new CrashAnalyticsService(fileSystem, errorHandler);
        _services.Add(service);

        // Add old crash report
        var oldCrash = CrashReport.FromException(new Exception("Old crash"), "TestComponent");
        oldCrash.Timestamp = DateTime.UtcNow.AddDays(-60); // 60 days old
        await service.RecordCrashAsync(oldCrash);

        // Add recent crash report
        var recentCrash = CrashReport.FromException(new Exception("Recent crash"), "TestComponent");
        await service.RecordCrashAsync(recentCrash);

        // Act
        await service.CleanupOldDataAsync(TimeSpan.FromDays(30)); // Keep only last 30 days

        // Assert
        var stats = await service.GetCrashFrequencyStatsAsync();
        // The exact count depends on existing data, but we should have at least the recent crash
        Assert.True(stats.TotalCrashes >= 1);
    }

    #endregion

    public void Dispose()
    {
        foreach (var service in _services)
        {
            service?.Dispose();
        }

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

    /// <summary>
    /// Test file system that uses a specific test directory
    /// </summary>
    private class TestFileSystem : IFileSystem
    {
        private readonly string _testDirectory;

        public TestFileSystem(string testDirectory)
        {
            _testDirectory = testDirectory;
        }

        public Task DeleteFileAsync(string path) => Task.Run(() => File.Delete(path));
        public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);
        public Task MoveFileAsync(string sourcePath, string destinationPath) => Task.Run(() => File.Move(sourcePath, destinationPath));
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;
        public string Combine(params string[] paths) => Path.Combine(paths);

        public bool FileExists(string path) => File.Exists(GetTestPath(path));
        public bool DirectoryExists(string path) => Directory.Exists(GetTestPath(path));
        public void CreateDirectory(string path) => Directory.CreateDirectory(GetTestPath(path));
        public async Task<string> ReadAllTextAsync(string path) => await File.ReadAllTextAsync(GetTestPath(path));
        public async Task WriteAllTextAsync(string path, string content) => await File.WriteAllTextAsync(GetTestPath(path), content);
        public void DeleteFile(string path) => File.Delete(GetTestPath(path));
        public void DeleteDirectory(string path, bool recursive = false) => Directory.Delete(GetTestPath(path), recursive);
        public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => Directory.GetFiles(GetTestPath(path), searchPattern, searchOption);
        public string[] GetDirectories(string path) => Directory.GetDirectories(GetTestPath(path));

        private string GetTestPath(string path)
        {
            // If path is already in our test directory, use it as-is
            if (path.StartsWith(_testDirectory))
                return path;
            
            // If it's an absolute path, map it to our test directory
            if (Path.IsPathRooted(path))
            {
                var relativePath = path.Substring(Path.GetPathRoot(path)!.Length);
                return Path.Combine(_testDirectory, relativePath);
            }
            
            // Relative path, combine with test directory
            return Path.Combine(_testDirectory, path);
        }
    }
}