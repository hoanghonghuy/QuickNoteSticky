using System.Collections.Concurrent;
using System.Timers;
using DevSticky.Interfaces;
using Timer = System.Timers.Timer;

namespace DevSticky.Services;

/// <summary>
/// Optimized service for debouncing operations (e.g., auto-save)
/// Uses a single timer and PriorityQueue for efficient management of multiple debounce operations.
/// </summary>
public class DebounceService : IDebounceService, IDisposable
{
    private readonly PriorityQueue<DebounceEntry, DateTime> _queue = new();
    private readonly ConcurrentDictionary<string, DebounceEntry> _entries = new();
    private readonly Timer _timer;
    private readonly object _lock = new();
    private bool _disposed;

    public DebounceService()
    {
        _timer = new Timer
        {
            AutoReset = false
        };
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>
    /// Debounces an action by key. If called again before the delay expires,
    /// the timer resets and the action is delayed again.
    /// </summary>
    public void Debounce(string key, Action action, int milliseconds)
    {
        if (_disposed) return;

        lock (_lock)
        {
            var executeAt = DateTime.UtcNow.AddMilliseconds(milliseconds);
            var entry = new DebounceEntry(key, action, executeAt);

            // Remove old entry if exists
            if (_entries.TryRemove(key, out var oldEntry))
            {
                oldEntry.IsCancelled = true;
            }

            // Add new entry
            _entries[key] = entry;
            _queue.Enqueue(entry, executeAt);

            // Update timer to fire at the earliest scheduled time
            UpdateTimer();
        }
    }

    /// <summary>
    /// Cancels a pending debounced action by key.
    /// </summary>
    public void Cancel(string key)
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_entries.TryRemove(key, out var entry))
            {
                entry.IsCancelled = true;
                UpdateTimer();
            }
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed) return;

        List<Action> actionsToExecute;

        lock (_lock)
        {
            var now = DateTime.UtcNow.AddMilliseconds(10); // Add small buffer for timing precision
            actionsToExecute = new List<Action>();

            // Process all entries that are due
            while (_queue.Count > 0 && _queue.Peek().ExecuteAt <= now)
            {
                var entry = _queue.Dequeue();

                // Skip cancelled entries
                if (entry.IsCancelled)
                    continue;

                // Remove from dictionary and collect action
                if (_entries.TryRemove(entry.Key, out _))
                {
                    actionsToExecute.Add(entry.Action);
                }
            }

            // Update timer for next scheduled entry
            UpdateTimer();
        }

        // Execute actions outside the lock to avoid blocking
        foreach (var action in actionsToExecute)
        {
            try
            {
                action?.Invoke();
            }
            catch
            {
                // Swallow exceptions to prevent one failing action from affecting others
            }
        }
    }

    private void UpdateTimer()
    {
        // Must be called within lock
        _timer.Stop();

        // Find the next non-cancelled entry
        while (_queue.Count > 0 && _queue.Peek().IsCancelled)
        {
            _queue.Dequeue();
        }

        if (_queue.Count > 0)
        {
            var nextEntry = _queue.Peek();
            var delay = (nextEntry.ExecuteAt - DateTime.UtcNow).TotalMilliseconds;

            if (delay > 0)
            {
                _timer.Interval = delay;
                _timer.Start();
            }
            else
            {
                // Entry is already due, trigger immediately
                _timer.Interval = 1;
                _timer.Start();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _entries.Clear();
                _queue.Clear();
            }
        }
    }

    private class DebounceEntry
    {
        public string Key { get; }
        public Action Action { get; }
        public DateTime ExecuteAt { get; }
        public bool IsCancelled { get; set; }

        public DebounceEntry(string key, Action action, DateTime executeAt)
        {
            Key = key;
            Action = action;
            ExecuteAt = executeAt;
            IsCancelled = false;
        }
    }
}
