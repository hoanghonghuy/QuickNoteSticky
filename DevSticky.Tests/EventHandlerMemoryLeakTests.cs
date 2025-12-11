using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using DevSticky.Views;
using DevSticky.Helpers;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Tests to verify event handler memory leaks are fixed
/// Requirements: 4.1
/// </summary>
public class EventHandlerMemoryLeakTests
{
    [Fact]
    public void EventSubscriptionManager_DisposesAllSubscriptions()
    {
        // Arrange
        var eventManager = new EventSubscriptionManager();
        var testObject = new TestEventSource();
        var handlerCalled = false;

        // Act
        eventManager.Subscribe<EventArgs>(testObject, nameof(testObject.TestEvent), (_, _) => handlerCalled = true);
        
        // Trigger event to verify subscription works
        testObject.RaiseTestEvent();
        Assert.True(handlerCalled);

        // Dispose and verify cleanup
        eventManager.Dispose();
        
        // Reset flag and trigger again
        handlerCalled = false;
        testObject.RaiseTestEvent();
        
        // Assert - handler should not be called after disposal
        Assert.False(handlerCalled);
    }

    [Fact]
    public void TemplateSelectionDialog_ImplementsIDisposable()
    {
        // Verify the class implements IDisposable interface
        var type = typeof(TemplateSelectionDialog);
        Assert.True(typeof(IDisposable).IsAssignableFrom(type));
    }

    [Fact]
    public void SettingsWindow_ImplementsIDisposable()
    {
        // Verify the class implements IDisposable interface
        var type = typeof(SettingsWindow);
        Assert.True(typeof(IDisposable).IsAssignableFrom(type));
    }

    [Fact]
    public void GraphViewWindow_ImplementsIDisposable()
    {
        // Verify the class implements IDisposable interface
        var type = typeof(GraphViewWindow);
        Assert.True(typeof(IDisposable).IsAssignableFrom(type));
    }

    /// <summary>
    /// Test class for event subscription testing
    /// </summary>
    private class TestEventSource
    {
        public event EventHandler<EventArgs>? TestEvent;

        public void RaiseTestEvent()
        {
            TestEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}