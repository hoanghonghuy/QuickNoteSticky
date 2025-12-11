using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Thread-safe Least Recently Used (LRU) cache implementation.
/// Uses a combination of Dictionary for O(1) lookups and LinkedList for O(1) eviction.
/// When capacity is reached, the least recently used item is automatically evicted.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache</typeparam>
/// <typeparam name="TValue">The type of values in the cache</typeparam>
public class LruCache<TKey, TValue> : ILruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _maxSize;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the LruCache with the specified maximum size.
    /// </summary>
    /// <param name="maxSize">The maximum number of items the cache can hold. Must be greater than 0.</param>
    /// <exception cref="ArgumentException">Thrown when maxSize is less than or equal to 0</exception>
    public LruCache(int maxSize)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentException("Max size must be greater than 0", nameof(maxSize));
        }

        _maxSize = maxSize;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
        _lruList = new LinkedList<CacheItem>();
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <inheritdoc/>
    public int MaxSize => _maxSize;

    /// <inheritdoc/>
    public void Add(TKey key, TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            // If key already exists, update it and move to front
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update the value
                existingNode.Value.Value = value;
                
                // Move to front (most recently used)
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // If at capacity, remove least recently used item (last in list)
            if (_cache.Count >= _maxSize)
            {
                var lruNode = _lruList.Last;
                if (lruNode != null)
                {
                    _cache.Remove(lruNode.Value.Key);
                    _lruList.RemoveLast();
                }
            }

            // Add new item to front (most recently used)
            var cacheItem = new CacheItem(key, value);
            var newNode = _lruList.AddFirst(cacheItem);
            _cache[key] = newNode;
        }
    }

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Remove(TKey key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
            }
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Disposes the cache and releases all resources.
    /// </summary>
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
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Internal class to hold cache items with their key-value pairs.
    /// </summary>
    private class CacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; set; }

        public CacheItem(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}
