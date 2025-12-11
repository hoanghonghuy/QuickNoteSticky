using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for ExceptionLogger class
/// Tests exception logging, resource tracking, and cleanup functionality
/// </summary>
public class ExceptionLoggerUnitTests : IDisposable
{
    private readonly List<ExceptionLogger> _loggers = new();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidErrorHandler_ShouldCreateLogger()
    {
        // Arrange
        var errorHandler = new ErrorHandler();

        // Act
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void Constructor_WithNullErrorHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        IErrorHandler nullErrorHandler = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ExceptionLogger(nullErrorHandler));
    }

    #endregion

    #region LogStartupException Tests

    [Fact]
    public void LogStartupException_WithValidException_ShouldLogWithoutThrowing()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var exception = new InvalidOperationException("Test exception");
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation",
            Parameters = new Dictionary<string, object> { { "testParam", "testValue" } }
        };

        // Act & Assert - Should not throw
        logger.LogStartupException(exception, context);
    }

    [Fact]
    public void LogStartupException_WithNullException_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => logger.LogStartupException(null!, context));
    }

    [Fact]
    public void LogStartupException_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => logger.LogStartupException(exception, null!));
    }

    [Fact]
    public void LogStartupException_WithComplexContext_ShouldHandleAllProperties()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var exception = new FileNotFoundException("Config file not found", "settings.json");
        var context = new StartupExceptionContext
        {
            Phase = "ConfigurationLoading",
            Component = "ConfigurationService",
            Operation = "LoadSettings",
            Parameters = new Dictionary<string, object>
            {
                { "filePath", "settings.json" },
                { "retryCount", 3 },
                { "timeout", TimeSpan.FromSeconds(30) }
            },
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert - Should not throw
        logger.LogStartupException(exception, context);
    }

    #endregion

    #region LogStartupExceptionAsync Tests

    [Fact]
    public async Task LogStartupExceptionAsync_WithValidException_ShouldCompleteSuccessfully()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var exception = new InvalidOperationException("Test async exception");
        var context = new StartupExceptionContext
        {
            Phase = "AsyncTestPhase",
            Component = "AsyncTestComponent",
            Operation = "AsyncTestOperation"
        };

        // Act
        await logger.LogStartupExceptionAsync(exception, context);

        // Assert - Should complete without throwing
        Assert.True(true, "Async logging should complete successfully");
    }

    [Fact]
    public async Task LogStartupExceptionAsync_WithNullException_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            logger.LogStartupExceptionAsync(null!, context));
    }

    [Fact]
    public async Task LogStartupExceptionAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            logger.LogStartupExceptionAsync(exception, null!));
    }

    #endregion

    #region TrackResource Tests

    [Fact]
    public void TrackResource_WithValidResource_ShouldTrackResource()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var resource = new TestDisposableResource();

        // Act & Assert - Should not throw
        logger.TrackResource(resource);
    }

    [Fact]
    public void TrackResource_WithNullResource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => logger.TrackResource(null!));
    }

    [Fact]
    public void TrackResource_WithMultipleResources_ShouldTrackAll()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var resource1 = new TestDisposableResource();
        var resource2 = new TestDisposableResource();
        var resource3 = new TestDisposableResource();

        // Act & Assert - Should not throw
        logger.TrackResource(resource1);
        logger.TrackResource(resource2);
        logger.TrackResource(resource3);
    }

    #endregion

    #region ExecuteWithResourceTracking Tests

    [Fact]
    public void ExecuteWithResourceTracking_WithSuccessfulOperation_ShouldReturnResult()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "SuccessfulOperation"
        };

        // Act
        var result = logger.ExecuteWithResourceTracking(() => 42, context);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void ExecuteWithResourceTracking_WithFailingOperation_ShouldDisposeTrackedResources()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var resource1 = new TestDisposableResource();
        var resource2 = new TestDisposableResource();
        
        logger.TrackResource(resource1);
        logger.TrackResource(resource2);
        
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "FailingOperation"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            logger.ExecuteWithResourceTracking<int>(() => throw new InvalidOperationException("Test failure"), context));
        
        // Resources should be disposed
        Assert.True(resource1.IsDisposed, "Resource 1 should be disposed after failure");
        Assert.True(resource2.IsDisposed, "Resource 2 should be disposed after failure");
    }

    [Fact]
    public void ExecuteWithResourceTracking_WithSuccessfulOperation_ShouldNotDisposeResources()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var resource1 = new TestDisposableResource();
        var resource2 = new TestDisposableResource();
        
        logger.TrackResource(resource1);
        logger.TrackResource(resource2);
        
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "SuccessfulOperation"
        };

        // Act
        var result = logger.ExecuteWithResourceTracking(() => "success", context);

        // Assert
        Assert.Equal("success", result);
        Assert.False(resource1.IsDisposed, "Resource 1 should not be disposed after success");
        Assert.False(resource2.IsDisposed, "Resource 2 should not be disposed after success");
    }

    [Fact]
    public void ExecuteWithResourceTracking_WithNullOperation_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "NullOperation"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            logger.ExecuteWithResourceTracking<int>(null!, context));
    }

    [Fact]
    public void ExecuteWithResourceTracking_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            logger.ExecuteWithResourceTracking(() => 42, null!));
    }

    #endregion

    #region ExecuteWithResourceTrackingAsync Tests

    [Fact]
    public async Task ExecuteWithResourceTrackingAsync_WithSuccessfulOperation_ShouldReturnResult()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var context = new StartupExceptionContext
        {
            Phase = "AsyncTestPhase",
            Component = "AsyncTestComponent",
            Operation = "AsyncSuccessfulOperation"
        };

        // Act
        var result = await logger.ExecuteWithResourceTrackingAsync(() => Task.FromResult(42), context);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteWithResourceTrackingAsync_WithFailingOperation_ShouldDisposeTrackedResources()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var resource1 = new TestDisposableResource();
        var resource2 = new TestDisposableResource();
        
        logger.TrackResource(resource1);
        logger.TrackResource(resource2);
        
        var context = new StartupExceptionContext
        {
            Phase = "AsyncTestPhase",
            Component = "AsyncTestComponent",
            Operation = "AsyncFailingOperation"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            logger.ExecuteWithResourceTrackingAsync<int>(() => 
                Task.FromException<int>(new InvalidOperationException("Async test failure")), context));
        
        // Resources should be disposed
        Assert.True(resource1.IsDisposed, "Resource 1 should be disposed after async failure");
        Assert.True(resource2.IsDisposed, "Resource 2 should be disposed after async failure");
    }

    [Fact]
    public async Task ExecuteWithResourceTrackingAsync_WithSuccessfulOperation_ShouldNotDisposeResources()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var resource1 = new TestDisposableResource();
        var resource2 = new TestDisposableResource();
        
        logger.TrackResource(resource1);
        logger.TrackResource(resource2);
        
        var context = new StartupExceptionContext
        {
            Phase = "AsyncTestPhase",
            Component = "AsyncTestComponent",
            Operation = "AsyncSuccessfulOperation"
        };

        // Act
        var result = await logger.ExecuteWithResourceTrackingAsync(() => Task.FromResult("async success"), context);

        // Assert
        Assert.Equal("async success", result);
        Assert.False(resource1.IsDisposed, "Resource 1 should not be disposed after async success");
        Assert.False(resource2.IsDisposed, "Resource 2 should not be disposed after async success");
    }

    [Fact]
    public async Task ExecuteWithResourceTrackingAsync_WithNullOperation_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);
        
        var context = new StartupExceptionContext
        {
            Phase = "AsyncTestPhase",
            Component = "AsyncTestComponent",
            Operation = "AsyncNullOperation"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            logger.ExecuteWithResourceTrackingAsync<int>(null!, context));
    }

    [Fact]
    public async Task ExecuteWithResourceTrackingAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var errorHandler = new ErrorHandler();
        var logger = new ExceptionLogger(errorHandler);
        _loggers.Add(logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            logger.ExecuteWithResourceTrackingAsync(() => Task.FromResult(42), null!));
    }

    #endregion

    #region StartupExceptionContext Tests

    [Fact]
    public void StartupExceptionContext_ShouldHaveDefaultTimestamp()
    {
        // Act
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation"
        };

        // Assert
        Assert.True(context.Timestamp > DateTime.MinValue);
        Assert.True(context.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void StartupExceptionContext_ShouldAllowCustomTimestamp()
    {
        // Arrange
        var customTimestamp = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation",
            Timestamp = customTimestamp
        };

        // Assert
        Assert.Equal(customTimestamp, context.Timestamp);
    }

    [Fact]
    public void StartupExceptionContext_ShouldHandleEmptyParameters()
    {
        // Act
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation",
            Parameters = new Dictionary<string, object>()
        };

        // Assert
        Assert.NotNull(context.Parameters);
        Assert.Empty(context.Parameters);
    }

    [Fact]
    public void StartupExceptionContext_ShouldHandleNullParameters()
    {
        // Act
        var context = new StartupExceptionContext
        {
            Phase = "TestPhase",
            Component = "TestComponent",
            Operation = "TestOperation",
            Parameters = null!
        };

        // Assert
        Assert.Null(context.Parameters);
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Test disposable resource for tracking cleanup
    /// </summary>
    private class TestDisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    #endregion

    public void Dispose()
    {
        foreach (var logger in _loggers)
        {
            try
            {
                logger?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
    }
}