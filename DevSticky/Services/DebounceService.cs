using System.Collections.Concurrent;
using System.Timers;
using DevSticky.Interfaces;
using Timer = System.Timers.Timer;

namespace DevSticky.Services;

/// <summary>
/// Service for debouncing operations (e.g., auto-save)
/// Supports multiple debounce keys for different operations.
/// </summary>
public class DebounceService : IDebounceService, IDisposable
{
    private readonly ConcurrentDictionary<string, DebounceEntry> _timers = new();
    private bool _disposed;

    /// <summary>
    /// Debounces an action by key. If called again before the delay expires,
    /// the timer resets and the action is delayed again.
    /// </summary>
    public void Debounce(string key, Action action, int milliseconds)
    {
        if (_disposed) return;
        
        // Cancel existing timer for this key
        Cancel(key);

        var timer = new Timer(milliseconds)
        {
            AutoReset = false
        };

        var entry = new DebounceEntry(timer, action);
        
        timer.Elapsed += (_, _) =>
        {
            if (_timers.TryRemove(key, out var removed))
            {
                removed.Timer.Dispose();
                removed.Action?.Invoke();
            }
        };

        _timers[key] = entry;
        timer.Start();
    }

    /// <summary>
    /// Cancels a pending debounced action by key.
    /// </summary>
    public void Cancel(string key)
    {
        if (_timers.TryRemove(key, out var entry))
        {
            entry.Timer.Stop();
            entry.Timer.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _timers.Values)
        {
            entry.Timer.Stop();
            entry.Timer.Dispose();
        }
        _timers.Clear();
    }

    private class DebounceEntry
    {
        public Timer Timer { get; }
        public Action Action { get; }

        public DebounceEntry(Timer timer, Action action)
        {
            Timer = timer;
            Action = action;
        }
    }
}
