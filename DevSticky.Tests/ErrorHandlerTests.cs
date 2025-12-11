using DevSticky.Services;
using System.IO;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for ErrorHandler
/// </summary>
public class ErrorHandlerTests
{
    private readonly ErrorHandler _errorHandler;

    public ErrorHandlerTests()
    {
        _errorHandler = new ErrorHandler();
    }

    [Fact]
    public void Handle_WithException_DoesNotThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert - Should not throw
        _errorHandler.Handle(exception, "Test context");
    }

    [Fact]
    public void Handle_WithNullException_ThrowsNullReferenceException()
    {
        // Act & Assert
        Assert.Throws<NullReferenceException>(() => _errorHandler.Handle(null!, "context"));
    }

    [Fact]
    public async Task HandleAsync_WithException_DoesNotThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert - Should not throw
        await _errorHandler.HandleAsync(exception, "Test context");
    }

    [Fact]
    public async Task HandleAsync_WithNullException_ThrowsNullReferenceException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => _errorHandler.HandleAsync(null!, "context"));
    }

    [Fact]
    public void HandleWithFallback_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = 42;

        // Act
        var result = _errorHandler.HandleWithFallback(() => expectedResult, 0, "Test operation");

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void HandleWithFallback_FailingOperation_ReturnsFallback()
    {
        // Arrange
        var fallbackValue = 99;

        // Act
        var result = _errorHandler.HandleWithFallback<int>(() => throw new InvalidOperationException("Test"), fallbackValue, "Test operation");

        // Assert
        Assert.Equal(fallbackValue, result);
    }

    [Fact]
    public void HandleWithFallback_WithNullOperation_ReturnsFallback()
    {
        // Act
        var result = _errorHandler.HandleWithFallback<int>(null!, 42, "context");
        
        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task HandleWithFallbackAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";

        // Act
        var result = await _errorHandler.HandleWithFallbackAsync(() => Task.FromResult(expectedResult), "fallback", "Test operation");

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task HandleWithFallbackAsync_FailingOperation_ReturnsFallback()
    {
        // Arrange
        var fallbackValue = "fallback";

        // Act
        var result = await _errorHandler.HandleWithFallbackAsync<string>(() => throw new InvalidOperationException("Test"), fallbackValue, "Test operation");

        // Assert
        Assert.Equal(fallbackValue, result);
    }

    [Fact]
    public async Task HandleWithFallbackAsync_WithNullOperation_ReturnsFallback()
    {
        // Act
        var result = await _errorHandler.HandleWithFallbackAsync<string>(null!, "fallback", "context");
        
        // Assert
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void HandleWithFallback_DifferentExceptionTypes_AllReturnFallback()
    {
        // Arrange
        var fallback = "fallback";

        // Act & Assert
        var result1 = _errorHandler.HandleWithFallback<string>(() => throw new ArgumentNullException(), fallback, "Test 1");
        var result2 = _errorHandler.HandleWithFallback<string>(() => throw new InvalidOperationException(), fallback, "Test 2");
        var result3 = _errorHandler.HandleWithFallback<string>(() => throw new IOException(), fallback, "Test 3");

        Assert.Equal(fallback, result1);
        Assert.Equal(fallback, result2);
        Assert.Equal(fallback, result3);
    }

    [Fact]
    public void Handle_WithEmptyContext_DoesNotThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert - Should not throw
        _errorHandler.Handle(exception, "");
        _errorHandler.Handle(exception);
    }

    [Fact]
    public void Handle_WithComplexContext_DoesNotThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var context = "Component.Service.Method";

        // Act & Assert - Should not throw
        _errorHandler.Handle(exception, context);
    }

    [Fact]
    public void HandleWithFallback_WithComplexTypes_WorksCorrectly()
    {
        // Arrange
        var fallbackList = new List<string> { "fallback" };

        // Act
        var result = _errorHandler.HandleWithFallback<List<string>>(() => throw new Exception(), fallbackList, "Test");

        // Assert
        Assert.Equal(fallbackList, result);
        Assert.Single(result);
        Assert.Equal("fallback", result[0]);
    }

    [Fact]
    public async Task HandleAsync_MultipleCallsConcurrently_DoesNotThrow()
    {
        // Arrange
        var tasks = new List<Task>();
        
        // Act
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(_errorHandler.HandleAsync(new InvalidOperationException($"Test {taskId}"), $"Context {taskId}"));
        }

        // Assert - Should not throw
        await Task.WhenAll(tasks);
    }
}