using System;
using System.Collections.Generic;
using System.Linq;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Thread-safe implementation of dirty tracking for items
/// </summary>
/// <typeparam name="T">The type of items to track</typeparam>
public class DirtyTracker<T> : IDirtyTracker<T> where T : class
{
    private readonly Dictionary<T, bool> _trackedItems = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the count of tracked items
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _trackedItems.Count;
            }
        }
    }

    /// <summary>
    /// Gets the count of dirty items
    /// </summary>
    public int DirtyCount
    {
        get
        {
            lock (_lock)
            {
                return _trackedItems.Count(kvp => kvp.Value);
            }
        }
    }

    /// <summary>
    /// Starts tracking an item
    /// </summary>
    /// <param name="item">The item to track</param>
    public void Track(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        lock (_lock)
        {
            if (!_trackedItems.ContainsKey(item))
            {
                _trackedItems[item] = false;
            }
        }
    }

    /// <summary>
    /// Marks an item as dirty (modified)
    /// </summary>
    /// <param name="item">The item to mark as dirty</param>
    public void MarkDirty(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        lock (_lock)
        {
            _trackedItems[item] = true;
        }
    }

    /// <summary>
    /// Marks an item as clean (not modified)
    /// </summary>
    /// <param name="item">The item to mark as clean</param>
    public void MarkClean(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        lock (_lock)
        {
            if (_trackedItems.ContainsKey(item))
            {
                _trackedItems[item] = false;
            }
        }
    }

    /// <summary>
    /// Gets all items that are currently marked as dirty
    /// </summary>
    /// <returns>Collection of dirty items</returns>
    public IEnumerable<T> GetDirtyItems()
    {
        lock (_lock)
        {
            return _trackedItems
                .Where(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList(); // Create a copy to avoid lock issues
        }
    }

    /// <summary>
    /// Clears all tracked items
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _trackedItems.Clear();
        }
    }
}
