using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for event handler cleanup to prevent memory leaks
/// **Feature: code-refactor, Property 4: Event Handler Cleanup**
/// **Validates: Requirements 4.1**
/// </summary>
public class EventHandlerCleanupPropertyTests
{
    /// <summary>
    /// Property: For any service that subscribes to events, all event handlers should be unsubscribed when disposed
    /// **Feature: code-refactor, Property 4: Event Handler Cleanup**
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EventHandlersAreCleanedUpOnDispose()
    {
        return Prop.ForAll(
            Gen.Elements(new[] { "ThemeService", "MonitorService", "HotkeyService", "CloudSyncService" }).ToArbitrary(),
            (string serviceName) =>
            {
                // Arrange
                var services = CreateTestServiceCollection();
                using var serviceProvider = services.BuildServiceProvider();
                
                var memoryBefore = MemoryLeakDetector.GetMemoryUsage();
                
                // Act - Create and dispose service
                IDisposable? service = serviceName switch
                {
                    "ThemeService" => serviceProvider.GetService<IThemeService>() as IDisposable,
                    "MonitorService" => serviceProvider.GetService<IMonitorService>() as IDisposable,
                    "HotkeyService" => serviceProvider.GetService<IHotkeyService>() as IDisposable,
                    "CloudSyncService" => serviceProvider.GetService<ICloudSyncService>() as IDisposable,
                    _ => null
                };
                
                if (service == null)
                    return true; // Skip if service not available
                
                // Subscribe to events if available
                SubscribeToServiceEvents(service);
                
                // Dispose the service
                service.Dispose();
                
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var memoryAfter = MemoryLeakDetector.GetMemoryUsage();
                
                // Assert - Check that service is properly disposed
                var isProperlyDisposed = MemoryLeakDetector.IsProperlyDisposed(service);
                
                // Check for memory leaks
                var issues = MemoryLeakDetector.AnalyzeObject(service);
                var hasEventLeaks = issues.Any(i => i.Type == MemoryLeakType.EventSubscription);
                
                return isProperlyDisposed && !hasEventLeaks;
            });
    }
    
    /// <summary>
    /// Property: WeakEventManager should automatically clean up all subscriptions when disposed
    /// **Feature: code-refactor, Property 4: Event Handler Cleanup**
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WeakEventManagerCleansUpSubscriptions()
    {
        return Prop.ForAll(
            Gen.Choose(1, 10).ToArbitrary(), // Number of subscriptions
            (int subscriptionCount) =>
            {
                // Arrange
                var eventManager = new EventSubscriptionManager();
                var testEventSource = new TestEventSource();
                var handlers = new List<EventHandler<EventArgs>>();
                
                // Act - Subscribe to multiple events
                for (int i = 0; i < subscriptionCount; i++)
                {
                    var handler = new EventHandler<EventArgs>((s, e) => { });
                    handlers.Add(handler);
                    eventManager.Subscribe<EventArgs>(testEventSource, nameof(testEventSource.TestEvent), handler);
                }
                
                // Verify subscriptions are active
                var subscriptionsBefore = testEventSource.GetSubscriptionCount();
                
                // Dispose event manager
                eventManager.Dispose();
                
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var subscriptionsAfter = testEventSource.GetSubscriptionCount();
                
                // Assert - All subscriptions should be cleaned up
                return subscriptionsBefore == subscriptionCount && subscriptionsAfter == 0;
            });
    }
    
    /// <summary>
    /// Property: Memory usage should not continuously increase when creating and disposing services
    /// **Feature: code-refactor, Property 4: Event Handler Cleanup**
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MemoryUsageStabilizesAfterServiceDisposal()
    {
        return Prop.ForAll(
            Gen.Choose(5, 20).ToArbitrary(), // Number of iterations
            (int iterationCount) =>
            {
                var initialMemory = MemoryLeakDetector.GetMemoryUsage();
                var services = CreateTestServiceCollection();
                
                // Act - Create and dispose services multiple times
                for (int i = 0; i < iterationCount; i++)
                {
                    using var serviceProvider = services.BuildServiceProvider();
                    
                    // Create services that use events
                    var themeService = serviceProvider.GetService<IThemeService>() as IDisposable;
                    var monitorService = serviceProvider.GetService<IMonitorService>() as IDisposable;
                    
                    // Subscribe to events
                    if (themeService != null) SubscribeToServiceEvents(themeService);
                    if (monitorService != null) SubscribeToServiceEvents(monitorService);
                    
                    // Services are disposed when serviceProvider is disposed
                }
                
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var finalMemory = MemoryLeakDetector.GetMemoryUsage();
                
                // Assert - Memory growth should be reasonable (less than 10MB increase)
                var memoryGrowth = finalMemory.ManagedMemory - initialMemory.ManagedMemory;
                return memoryGrowth < 10 * 1024 * 1024; // 10MB threshold
            });
    }
    
    private static ServiceCollection CreateTestServiceCollection()
    {
        var services = new ServiceCollection();
        
        // Add minimal services needed for testing
        services.AddSingleton<IWindowsThemeDetector, TestWindowsThemeDetector>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        
        return services;
    }
    
    private static void SubscribeToServiceEvents(IDisposable service)
    {
        // Subscribe to events based on service type
        switch (service)
        {
            case IThemeService themeService:
                themeService.ThemeChanged += (s, e) => { };
                break;
            case IMonitorService monitorService:
                monitorService.MonitorsChanged += (s, e) => { };
                break;
            case IHotkeyService hotkeyService:
                hotkeyService.HotkeyPressed += (s, e) => { };
                break;
        }
    }
}

/// <summary>
/// Test event source for testing weak event patterns
/// </summary>
internal class TestEventSource
{
    public event EventHandler<EventArgs>? TestEvent;
    
    public int GetSubscriptionCount()
    {
        return TestEvent?.GetInvocationList().Length ?? 0;
    }
    
    public void RaiseEvent()
    {
        TestEvent?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Test implementation of IWindowsThemeDetector
/// </summary>
internal class TestWindowsThemeDetector : IWindowsThemeDetector
{
#pragma warning disable CS0414 // Field is assigned but never used - this is a test implementation
    public event EventHandler<DevSticky.Models.Theme>? SystemThemeChanged;
#pragma warning restore CS0414
    
    public DevSticky.Models.Theme GetSystemTheme()
    {
        return DevSticky.Models.Theme.Dark;
    }
    
    public void StartMonitoring()
    {
        // No-op for testing
    }
    
    public void StopMonitoring()
    {
        // No-op for testing
    }
    
    public void Dispose()
    {
        SystemThemeChanged = null;
    }
}