using System.Runtime.CompilerServices;

namespace DevSticky.Helpers;

/// <summary>
/// Helper class for implementing weak event patterns to prevent memory leaks
/// </summary>
public static class WeakEventHelper
{
    /// <summary>
    /// Subscribe to an event using a weak reference to prevent memory leaks
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments</typeparam>
    /// <param name="source">The event source</param>
    /// <param name="eventName">Name of the event</param>
    /// <param name="handler">The event handler</param>
    /// <param name="target">The target object that owns the handler</param>
    public static void Subscribe<TEventArgs>(
        object source, 
        string eventName, 
        EventHandler<TEventArgs> handler,
        object target) where TEventArgs : EventArgs
    {
        var weakHandler = new WeakEventHandler<TEventArgs>(handler, target);
        var eventInfo = source.GetType().GetEvent(eventName);
        eventInfo?.AddEventHandler(source, weakHandler.Handler);
    }
    
    /// <summary>
    /// Unsubscribe from an event using weak reference
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments</typeparam>
    /// <param name="source">The event source</param>
    /// <param name="eventName">Name of the event</param>
    /// <param name="handler">The event handler</param>
    public static void Unsubscribe<TEventArgs>(
        object source, 
        string eventName, 
        EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
    {
        var eventInfo = source.GetType().GetEvent(eventName);
        eventInfo?.RemoveEventHandler(source, handler);
    }
}

/// <summary>
/// Weak event handler implementation to prevent memory leaks
/// </summary>
/// <typeparam name="TEventArgs">Type of event arguments</typeparam>
internal class WeakEventHandler<TEventArgs> where TEventArgs : EventArgs
{
    private readonly WeakReference _targetRef;
    private readonly string _methodName;

    public WeakEventHandler(EventHandler<TEventArgs> handler, object target)
    {
        _targetRef = new WeakReference(target);
        _methodName = handler.Method.Name;
        Handler = OnEvent;
    }

    public EventHandler<TEventArgs> Handler { get; }

    private void OnEvent(object? sender, TEventArgs e)
    {
        var target = _targetRef.Target;
        if (target == null)
        {
            // Target has been garbage collected, remove this handler
            return;
        }

        // Use reflection to invoke the method on the target
        var method = target.GetType().GetMethod(_methodName, 
            System.Reflection.BindingFlags.Instance | 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Public);
        
        method?.Invoke(target, new object?[] { sender, e });
    }
}

/// <summary>
/// Manages event subscriptions for a specific object with automatic cleanup
/// </summary>
public class EventSubscriptionManager : IDisposable
{
    private readonly List<WeakEventSubscription> _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// Subscribe to an event with automatic cleanup
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments</typeparam>
    /// <param name="source">Event source</param>
    /// <param name="eventName">Event name</param>
    /// <param name="handler">Event handler</param>
    public void Subscribe<TEventArgs>(object source, string eventName, EventHandler<TEventArgs> handler) 
        where TEventArgs : EventArgs
    {
        if (_disposed) return;
        
        var eventInfo = source.GetType().GetEvent(eventName);
        if (eventInfo != null)
        {
            eventInfo.AddEventHandler(source, handler);
            _subscriptions.Add(new WeakEventSubscription(source, eventInfo, handler));
        }
    }

    /// <summary>
    /// Unsubscribe from a specific event
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments</typeparam>
    /// <param name="source">Event source</param>
    /// <param name="eventName">Event name</param>
    /// <param name="handler">Event handler</param>
    public void Unsubscribe<TEventArgs>(object source, string eventName, EventHandler<TEventArgs> handler) 
        where TEventArgs : EventArgs
    {
        var eventInfo = source.GetType().GetEvent(eventName);
        if (eventInfo != null)
        {
            eventInfo.RemoveEventHandler(source, handler);
            _subscriptions.RemoveAll(s => 
                ReferenceEquals(s.Source, source) && 
                s.EventInfo == eventInfo && 
                ReferenceEquals(s.Handler, handler));
        }
    }

    /// <summary>
    /// Dispose and clean up all event subscriptions
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from all events
        foreach (var subscription in _subscriptions)
        {
            try
            {
                subscription.EventInfo.RemoveEventHandler(subscription.Source, subscription.Handler);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
        
        _subscriptions.Clear();
    }

    private record WeakEventSubscription(object Source, System.Reflection.EventInfo EventInfo, Delegate Handler);
}