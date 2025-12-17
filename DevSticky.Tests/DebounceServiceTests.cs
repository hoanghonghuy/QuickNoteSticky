using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for the optimized DebounceService
/// </summary>
public class DebounceServiceTests
{
    [Fact]
    public void Debounce_ExecutesActionAfterDelay()
    {
        // Arrange
        using var service = new DebounceService();
        using var resetEvent = new ManualResetEventSlim(false);
        var delay = 300;

        // Act
        service.Debounce("test", () => resetEvent.Set(), delay);
        var executed = resetEvent.Wait(delay + 200);

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public void Debounce_CancelsAndRestartsOnMultipleCalls()
    {
        // Arrange
        using var service = new DebounceService();
        using var resetEvent = new ManualResetEventSlim(false);
        var executionCount = 0;
        var delay = 300;
        var latch = new object();

        // Act
        service.Debounce("test", () =>
        {
            lock (latch)
            {
                executionCount++;
                resetEvent.Set();
            }
        }, delay);
        Thread.Sleep(150); // Wait half the delay
        service.Debounce("test", () =>
        {
            lock (latch)
            {
                executionCount++;
                resetEvent.Set();
            }
        }, delay); // Reset timer
        resetEvent.Wait(delay + 200); // Wait for action to execute

        // Assert - should only execute once (the second call)
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void Cancel_PreventsPendingActionFromExecuting()
    {
        // Arrange
        using var service = new DebounceService();
        var executed = false;
        var delay = 300;

        // Act
        service.Debounce("test", () => executed = true, delay);
        service.Cancel("test");
        Thread.Sleep(delay + 100); // Wait past the delay

        // Assert
        Assert.False(executed);
    }

    [Fact]
    public void Debounce_HandlesMultipleKeysIndependently()
    {
        // Arrange
        using var service = new DebounceService();
        using var resetEvent1 = new ManualResetEventSlim(false);
        using var resetEvent2 = new ManualResetEventSlim(false);
        var executed1 = false;
        var executed2 = false;
        var latch = new object();
        var delay = 300;

        // Act
        service.Debounce("key1", () =>
        {
            lock (latch)
            {
                executed1 = true;
                resetEvent1.Set();
            }
        }, delay);
        service.Debounce("key2", () =>
        {
            lock (latch)
            {
                executed2 = true;
                resetEvent2.Set();
            }
        }, delay);
        var waitResult1 = resetEvent1.Wait(delay + 200);
        var waitResult2 = resetEvent2.Wait(delay + 200);

        // Wait a bit more to ensure both have had time to execute
        Thread.Sleep(50);

        // Assert
        Assert.True(waitResult1 && executed1, "Key1 should execute");
        Assert.True(waitResult2 && executed2, "Key2 should execute");
    }

    [Fact]
    public void Dispose_StopsAllPendingActions()
    {
        // Arrange
        var service = new DebounceService();
        var executed = false;
        var delay = 300;

        // Act
        service.Debounce("test", () => executed = true, delay);
        service.Dispose();
        Thread.Sleep(delay + 100); // Wait past the delay

        // Assert
        Assert.False(executed);
    }

    [Fact]
    public void Debounce_HandlesExceptionsGracefully()
    {
        // Arrange
        using var service = new DebounceService();
        using var resetEvent = new ManualResetEventSlim(false);
        var delay = 300;

        // Act - first action throws, second should still execute
        service.Debounce("key1", () => throw new Exception("Test exception"), delay);
        Thread.Sleep(20); // Small delay to ensure different execution times
        service.Debounce("key2", () => resetEvent.Set(), delay);
        var executed = resetEvent.Wait(delay + 200);

        // Assert - second action should execute despite first throwing
        Assert.True(executed);
    }
}
