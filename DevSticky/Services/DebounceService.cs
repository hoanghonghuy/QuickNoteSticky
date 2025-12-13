using System.Collections.Concurrent;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service for debouncing operations (e.g., auto-save)
/// Ensures that only the latest debounced action executes for each key.
/// </summary>
public class DebounceService : IDebounceService, IDisposable
{
    private readonly ConcurrentDictionary<string, DebounceEntry> _entries = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Debounces an action by key. If called again before the delay expires,
    /// the previous action is cancelled and a new one is scheduled.
    /// </summary>
    public void Debounce(string key, Action action, int milliseconds)
    {
        if (_disposed) return;

        lock (_lock)
        {
            // Cancel any existing entry for this key
            if (_entries.TryGetValue(key, out var existingEntry))
            {
                existingEntry.CancellationTokenSource.Cancel();
                existingEntry.CancellationTokenSource.Dispose();
            }

            // Create new entry
            var cts = new CancellationTokenSource();
            var entry = new DebounceEntry(action, cts);
            _entries[key] = entry;

            // Schedule the action
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(milliseconds, cts.Token);
                    
                    // Check if this entry is still current and execute
                    lock (_lock)
                    {
                        if (_entries.TryGetValue(key, out var currentEntry) && currentEntry == entry)
                        {
                            _entries.TryRemove(key, out _);
                            action?.Invoke();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled, do nothing
                }
                catch
                {
                    // Swallow other exceptions to prevent one failing action from affecting others
                }
                finally
                {
                    cts.Dispose();
                }
            }, cts.Token);
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
                entry.CancellationTokenSource.Cancel();
                entry.CancellationTokenSource.Dispose();
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
                // Cancel all pending operations
                foreach (var entry in _entries.Values)
                {
                    entry.CancellationTokenSource.Cancel();
                    entry.CancellationTokenSource.Dispose();
                }
                _entries.Clear();
            }
        }
    }

    private class DebounceEntry
    {
        public Action Action { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public DebounceEntry(Action action, CancellationTokenSource cancellationTokenSource)
        {
            Action = action;
            CancellationTokenSource = cancellationTokenSource;
        }
    }
}
