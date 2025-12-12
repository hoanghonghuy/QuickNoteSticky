using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// LRU cache with Time-To-Live (TTL) support.
/// Items automatically expire after the specified TTL.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache</typeparam>
/// <typeparam name="TValue">The type of values in the cache</typeparam>
public class TtlLruCache<TKey, TValue> : ILruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _maxSize;
    private readonly TimeSpan _defaultTtl;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new();
    private bool _disposed;

    // Statistics
    private long _hits;
    private long _misses;
    private long _evictions;
    private long _expirations;

    /// <summary>
    /// Initializes a new instance with specified max size and TTL.
    /// </summary>
    /// <param name="maxSize">Maximum number of items</param>
    /// <param name="defaultTtl">Default time-to-live for items</param>
    public TtlLruCache(int maxSize, TimeSpan defaultTtl)
    {
        if (maxSize <= 0)
            throw new ArgumentException("Max size must be greater than 0", nameof(maxSize));
        
        _maxSize = maxSize;
        _defaultTtl = defaultTtl;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// Initializes with default TTL of 5 minutes.
    /// </summary>
    public TtlLruCache(int maxSize) : this(maxSize, TimeSpan.FromMinutes(5))
    {
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                CleanupExpired();
                return _cache.Count;
            }
        }
    }

    public int MaxSize => _maxSize;

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStats GetStats()
    {
        lock (_lock)
        {
            return new CacheStats
            {
                Hits = _hits,
                Misses = _misses,
                Evictions = _evictions,
                Expirations = _expirations,
                CurrentSize = _cache.Count,
                MaxSize = _maxSize,
                HitRate = _hits + _misses > 0 
                    ? (double)_hits / (_hits + _misses) 
                    : 0
            };
        }
    }

    public void Add(TKey key, TValue value)
    {
        Add(key, value, _defaultTtl);
    }

    /// <summary>
    /// Adds an item with custom TTL
    /// </summary>
    public void Add(TKey key, TValue value, TimeSpan ttl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            CleanupExpired();

            // Update existing
            if (_cache.TryGetValue(key, out var existingNode))
            {
                existingNode.Value.Value = value;
                existingNode.Value.ExpiresAt = DateTime.UtcNow.Add(ttl);
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // Evict if at capacity
            if (_cache.Count >= _maxSize)
            {
                var lruNode = _lruList.Last;
                if (lruNode != null)
                {
                    _cache.Remove(lruNode.Value.Key);
                    _lruList.RemoveLast();
                    _evictions++;
                }
            }

            // Add new item
            var cacheItem = new CacheItem(key, value, DateTime.UtcNow.Add(ttl));
            var newNode = _lruList.AddFirst(cacheItem);
            _cache[key] = newNode;
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Check if expired
                if (node.Value.ExpiresAt < DateTime.UtcNow)
                {
                    _cache.Remove(key);
                    _lruList.Remove(node);
                    _expirations++;
                    _misses++;
                    value = default!;
                    return false;
                }

                // Move to front
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                _hits++;
                value = node.Value.Value;
                return true;
            }

            _misses++;
            value = default!;
            return false;
        }
    }

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
    /// Removes all expired items from the cache
    /// </summary>
    public int CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var removed = 0;
        var toRemove = new List<TKey>();

        foreach (var kvp in _cache)
        {
            if (kvp.Value.Value.ExpiresAt < now)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
                _expirations++;
                removed++;
            }
        }

        return removed;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

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

    private class CacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; set; }
        public DateTime ExpiresAt { get; set; }

        public CacheItem(TKey key, TValue value, DateTime expiresAt)
        {
            Key = key;
            Value = value;
            ExpiresAt = expiresAt;
        }
    }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStats
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long Evictions { get; set; }
    public long Expirations { get; set; }
    public int CurrentSize { get; set; }
    public int MaxSize { get; set; }
    public double HitRate { get; set; }
}
