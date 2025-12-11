namespace DevSticky.Interfaces;

/// <summary>
/// Interface for a Least Recently Used (LRU) cache implementation.
/// Provides efficient caching with automatic eviction of least recently used items when capacity is reached.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache</typeparam>
/// <typeparam name="TValue">The type of values in the cache</typeparam>
public interface ILruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    /// <summary>
    /// Adds or updates a value in the cache.
    /// If the cache is at capacity, the least recently used item will be evicted.
    /// </summary>
    /// <param name="key">The key to add or update</param>
    /// <param name="value">The value to store</param>
    void Add(TKey key, TValue value);

    /// <summary>
    /// Attempts to get a value from the cache.
    /// If found, the item is marked as recently used.
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The value if found, default otherwise</param>
    /// <returns>True if the key was found, false otherwise</returns>
    bool TryGetValue(TKey key, out TValue value);

    /// <summary>
    /// Removes a specific item from the cache.
    /// </summary>
    /// <param name="key">The key to remove</param>
    void Remove(TKey key);

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the current number of items in the cache.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum number of items the cache can hold.
    /// </summary>
    int MaxSize { get; }
}
