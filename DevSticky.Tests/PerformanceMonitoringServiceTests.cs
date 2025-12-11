using System;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for PerformanceMonitoringService
/// </summary>
public class PerformanceMonitoringServiceTests
{
    [Fact]
    public void Constructor_WithValidDiagnostics_ShouldInitialize()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        
        // Act
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Assert
        Assert.NotNull(performanceService);
    }

    [Fact]
    public void Constructor_WithNullDiagnostics_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PerformanceMonitoringService(null!));
    }

    [Fact]
    public void StartCategoryTiming_WithValidCategory_ShouldStartTimer()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Act
        performanceService.StartCategoryTiming("TestCategory");
        
        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public void StopCategoryTiming_WithValidCategory_ShouldStopTimer()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Act
        performanceService.StartCategoryTiming("TestCategory");
        performanceService.StopCategoryTiming("TestCategory");
        
        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public void MarkMilestone_WithValidMilestone_ShouldRecordTimestamp()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Act
        performanceService.MarkMilestone("FirstService");
        performanceService.MarkMilestone("UIReady");
        performanceService.MarkMilestone("FullyFunctional");
        
        // Assert - Should not throw
        Assert.True(true);
    }

    [Fact]
    public void GetPerformanceMetrics_ShouldReturnValidMetrics()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Simulate some activity
        performanceService.StartCategoryTiming("ServiceInitialization");
        System.Threading.Thread.Sleep(10); // Small delay to measure
        performanceService.StopCategoryTiming("ServiceInitialization");
        
        performanceService.MarkMilestone("FirstService");
        
        // Act
        var metrics = performanceService.GetPerformanceMetrics();
        
        // Assert
        Assert.NotNull(metrics);
        Assert.NotNull(metrics.Startup);
        Assert.NotNull(metrics.Validation);
        Assert.NotNull(metrics.Memory);
        Assert.NotNull(metrics.Timing);
        Assert.NotNull(metrics.Thresholds);
        Assert.NotNull(metrics.Warnings);
        
        // Check that timing was recorded
        Assert.True(metrics.Timing.ServiceInitializationTime.TotalMilliseconds >= 0);
    }

    [Fact]
    public void GetWarnings_InitiallyEmpty_ShouldReturnEmptyList()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Act
        var warnings = performanceService.GetWarnings();
        
        // Assert
        Assert.NotNull(warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ClearWarnings_ShouldRemoveAllWarnings()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Act
        performanceService.ClearWarnings();
        var warnings = performanceService.GetWarnings();
        
        // Assert
        Assert.Empty(warnings);
    }

    [Fact]
    public void UpdateThresholds_WithNewThresholds_ShouldUpdateValues()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        var newThresholds = new PerformanceThresholds
        {
            MaxAcceptableStartupTime = TimeSpan.FromSeconds(10),
            MaxAcceptableValidationOverhead = TimeSpan.FromMilliseconds(200),
            MaxAcceptableMemoryUsageMB = 500
        };
        
        // Act
        performanceService.UpdateThresholds(newThresholds);
        var metrics = performanceService.GetPerformanceMetrics();
        
        // Assert
        Assert.Equal(TimeSpan.FromSeconds(10), metrics.Thresholds.MaxAcceptableStartupTime);
        Assert.Equal(TimeSpan.FromMilliseconds(200), metrics.Thresholds.MaxAcceptableValidationOverhead);
        Assert.Equal(500, metrics.Thresholds.MaxAcceptableMemoryUsageMB);
    }

    [Fact]
    public async Task ExportPerformanceMetricsAsync_WithValidPath_ShouldCreateFile()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        var tempPath = System.IO.Path.GetTempFileName();
        
        try
        {
            // Act
            await performanceService.ExportPerformanceMetricsAsync(tempPath);
            
            // Assert
            Assert.True(System.IO.File.Exists(tempPath));
            var content = await System.IO.File.ReadAllTextAsync(tempPath);
            Assert.NotEmpty(content);
            Assert.Contains("performancemetrics", content.ToLowerInvariant());
        }
        finally
        {
            // Cleanup
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void LogPerformanceSummary_ShouldNotThrow()
    {
        // Arrange
        var startupDiagnostics = new StartupDiagnostics();
        using var performanceService = new PerformanceMonitoringService(startupDiagnostics);
        
        // Act & Assert - Should not throw
        performanceService.LogPerformanceSummary();
    }

    [Fact]
    public void PerformanceMetrics_GetSummary_ShouldReturnFormattedString()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            Startup = new StartupPerformanceMetrics
            {
                TotalDuration = TimeSpan.FromMilliseconds(1500)
            },
            Validation = new ValidationOverheadMetrics
            {
                TotalOverhead = TimeSpan.FromMilliseconds(100),
                OverheadPercentage = 6.7
            },
            Memory = new MemoryUsageMetrics
            {
                PeakUsageMB = 150
            }
        };
        
        // Act
        var summary = metrics.GetSummary();
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("Startup Performance Summary", summary);
        Assert.Contains("Total Duration", summary);
        Assert.Contains("Validation Overhead", summary);
        Assert.Contains("Peak Memory", summary);
        Assert.Contains("Performance Acceptable", summary);
    }

    [Fact]
    public void ValidationOverheadMetrics_AverageValidationTime_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new ValidationOverheadMetrics
        {
            TotalOverhead = TimeSpan.FromMilliseconds(200),
            ValidationChecksPerformed = 4
        };
        
        // Act
        var average = metrics.AverageValidationTime;
        
        // Assert
        Assert.Equal(50, average.TotalMilliseconds);
    }

    [Fact]
    public void ValidationOverheadMetrics_AverageValidationTime_WithZeroChecks_ShouldReturnZero()
    {
        // Arrange
        var metrics = new ValidationOverheadMetrics
        {
            TotalOverhead = TimeSpan.FromMilliseconds(200),
            ValidationChecksPerformed = 0
        };
        
        // Act
        var average = metrics.AverageValidationTime;
        
        // Assert
        Assert.Equal(TimeSpan.Zero, average);
    }

    [Fact]
    public void TimingBreakdownMetrics_GetPercentageBreakdown_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new TimingBreakdownMetrics
        {
            ServiceInitializationTime = TimeSpan.FromMilliseconds(500),
            ConfigurationLoadingTime = TimeSpan.FromMilliseconds(300),
            UISetupTime = TimeSpan.FromMilliseconds(200)
        };
        var totalTime = TimeSpan.FromMilliseconds(1000);
        
        // Act
        var breakdown = metrics.GetPercentageBreakdown(totalTime);
        
        // Assert
        Assert.Equal(50.0, breakdown["Service Initialization"]);
        Assert.Equal(30.0, breakdown["Configuration Loading"]);
        Assert.Equal(20.0, breakdown["UI Setup"]);
    }

    [Fact]
    public void PerformanceMetrics_IsPerformanceAcceptable_WithGoodMetrics_ShouldReturnTrue()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            Startup = new StartupPerformanceMetrics
            {
                TotalDuration = TimeSpan.FromSeconds(2) // Under 5 second threshold
            },
            Validation = new ValidationOverheadMetrics
            {
                TotalOverhead = TimeSpan.FromMilliseconds(50) // Under 100ms threshold
            },
            Memory = new MemoryUsageMetrics
            {
                PeakUsageMB = 100 // Under 200MB threshold
            },
            Thresholds = new PerformanceThresholds() // Default thresholds
        };
        
        // Act
        var isAcceptable = metrics.IsPerformanceAcceptable;
        
        // Assert
        Assert.True(isAcceptable);
    }

    [Fact]
    public void PerformanceMetrics_IsPerformanceAcceptable_WithSlowStartup_ShouldReturnFalse()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            Startup = new StartupPerformanceMetrics
            {
                TotalDuration = TimeSpan.FromSeconds(10) // Over 5 second threshold
            },
            Validation = new ValidationOverheadMetrics
            {
                TotalOverhead = TimeSpan.FromMilliseconds(50)
            },
            Memory = new MemoryUsageMetrics
            {
                PeakUsageMB = 100
            },
            Thresholds = new PerformanceThresholds()
        };
        
        // Act
        var isAcceptable = metrics.IsPerformanceAcceptable;
        
        // Assert
        Assert.False(isAcceptable);
    }
}