using DevSticky.Interfaces;
using DevSticky.Models;
using System.Threading;

namespace DevSticky.Services;

/// <summary>
/// Enhanced cache service with LRU caching and statistics tracking.
/// Provides efficient caching of tags and groups with automatic eviction and hit/miss tracking.
/// </summary>
public class EnhancedCacheService : ICacheService
{
    private readonly ILruCache<Guid, NoteTag> _tagCache;
    private readonly ILruCache<Guid, NoteGroup> _groupCache;
    private readonly Func<IEnumerable<NoteTag>> _tagProvider;
    private readonly Func<IEnumerable<NoteGroup>> _groupProvider;
    
    // Statistics tracking (using Interlocked for thread-safety)
    private long _totalHits;
    private long _totalMisses;
    private long _tagCacheInvalidations;
    private long _groupCacheInvalidations;
    
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the EnhancedCacheService.
    /// </summary>
    /// <param name="tagProvider">Function to provide all tags when cache miss occurs</param>
    /// <param name="groupProvider">Function to provide all groups when cache miss occurs</param>
    /// <param name="tagCacheSize">Maximum size of the tag cache (default: 100)</param>
    /// <param name="groupCacheSize">Maximum size of the group cache (default: 50)</param>
    public EnhancedCacheService(
        Func<IEnumerable<NoteTag>> tagProvider,
        Func<IEnumerable<NoteGroup>> groupProvider,
        int tagCacheSize = 100,
        int groupCacheSize = 50)
    {
        _tagProvider = tagProvider ?? throw new ArgumentNullException(nameof(tagProvider));
        _groupProvider = groupProvider ?? throw new ArgumentNullException(nameof(groupProvider));
        _tagCache = new LruCache<Guid, NoteTag>(tagCacheSize);
        _groupCache = new LruCache<Guid, NoteGroup>(groupCacheSize);
    }

    /// <inheritdoc/>
    public NoteTag? GetTag(Guid tagId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try to get from cache first
        if (_tagCache.TryGetValue(tagId, out var cachedTag))
        {
            Interlocked.Increment(ref _totalHits);
            return cachedTag;
        }

        // Cache miss - fetch from tag provider
        Interlocked.Increment(ref _totalMisses);
        
        // Optimized: Direct search without LINQ
        NoteTag? tag = null;
        foreach (var t in _tagProvider())
        {
            if (t.Id == tagId)
            {
                tag = t;
                break;
            }
        }
        if (tag != null)
        {
            _tagCache.Add(tagId, tag);
        }

        return tag;
    }

    /// <inheritdoc/>
    public NoteGroup? GetGroup(Guid groupId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try to get from cache first
        if (_groupCache.TryGetValue(groupId, out var cachedGroup))
        {
            Interlocked.Increment(ref _totalHits);
            return cachedGroup;
        }

        // Cache miss - fetch from group provider
        Interlocked.Increment(ref _totalMisses);
        
        // Optimized: Direct search without LINQ
        NoteGroup? group = null;
        foreach (var g in _groupProvider())
        {
            if (g.Id == groupId)
            {
                group = g;
                break;
            }
        }
        if (group != null)
        {
            _groupCache.Add(groupId, group);
        }

        return group;
    }

    /// <inheritdoc/>
    public List<NoteTag> GetTags(IEnumerable<Guid> tagIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new List<NoteTag>();
        var missingIds = new List<Guid>();

        // Try to get each tag from cache
        foreach (var tagId in tagIds)
        {
            if (_tagCache.TryGetValue(tagId, out var cachedTag))
            {
                Interlocked.Increment(ref _totalHits);
                result.Add(cachedTag);
            }
            else
            {
                Interlocked.Increment(ref _totalMisses);
                missingIds.Add(tagId);
            }
        }

        // Fetch missing tags from tag provider
        if (missingIds.Count > 0)
        {
            var allTags = _tagProvider();
            foreach (var tagId in missingIds)
            {
                // Optimized: Direct search without LINQ
                NoteTag? tag = null;
                foreach (var t in allTags)
                {
                    if (t.Id == tagId)
                    {
                        tag = t;
                        break;
                    }
                }
                if (tag != null)
                {
                    _tagCache.Add(tagId, tag);
                    result.Add(tag);
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public void InvalidateTagCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _tagCache.Clear();
            Interlocked.Increment(ref _tagCacheInvalidations);
        }
    }

    /// <inheritdoc/>
    public void InvalidateGroupCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _groupCache.Clear();
            Interlocked.Increment(ref _groupCacheInvalidations);
        }
    }

    /// <inheritdoc/>
    public void InvalidateAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _tagCache.Clear();
            _groupCache.Clear();
            Interlocked.Increment(ref _tagCacheInvalidations);
            Interlocked.Increment(ref _groupCacheInvalidations);
        }
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new CacheStatistics
        {
            TotalHits = Interlocked.Read(ref _totalHits),
            TotalMisses = Interlocked.Read(ref _totalMisses),
            TagCacheSize = _tagCache.Count,
            GroupCacheSize = _groupCache.Count,
            TagCacheMaxSize = _tagCache.MaxSize,
            GroupCacheMaxSize = _groupCache.MaxSize,
            TagCacheInvalidations = Interlocked.Read(ref _tagCacheInvalidations),
            GroupCacheInvalidations = Interlocked.Read(ref _groupCacheInvalidations)
        };
    }

    /// <summary>
    /// Disposes the cache service and releases all resources.
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
            _tagCache?.Dispose();
            _groupCache?.Dispose();
        }

        _disposed = true;
    }
}
