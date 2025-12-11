using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for caching frequently accessed data to improve performance
/// </summary>
public class CacheService : IDisposable
{
    private Dictionary<Guid, NoteTag>? _tagCache;
    private Dictionary<Guid, NoteGroup>? _groupCache;
    private DateTime _lastTagUpdate = DateTime.MinValue;
    private DateTime _lastGroupUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Get tag by ID with caching
    /// </summary>
    public NoteTag? GetTag(Guid tagId, IEnumerable<NoteTag> tags)
    {
        RefreshTagCacheIfNeeded(tags);
        return _tagCache?.GetValueOrDefault(tagId);
    }

    /// <summary>
    /// Get group by ID with caching
    /// </summary>
    public NoteGroup? GetGroup(Guid groupId, IEnumerable<NoteGroup> groups)
    {
        RefreshGroupCacheIfNeeded(groups);
        return _groupCache?.GetValueOrDefault(groupId);
    }

    /// <summary>
    /// Get multiple tags by IDs efficiently
    /// </summary>
    public List<NoteTag> GetTags(IEnumerable<Guid> tagIds, IEnumerable<NoteTag> tags)
    {
        RefreshTagCacheIfNeeded(tags);
        if (_tagCache == null) return new List<NoteTag>();
        
        var result = new List<NoteTag>();
        foreach (var id in tagIds)
        {
            if (_tagCache.TryGetValue(id, out var tag))
                result.Add(tag);
        }
        return result;
    }

    /// <summary>
    /// Invalidate tag cache (call when tags are modified)
    /// </summary>
    public void InvalidateTagCache()
    {
        _tagCache = null;
        _lastTagUpdate = DateTime.MinValue;
    }

    /// <summary>
    /// Invalidate group cache (call when groups are modified)
    /// </summary>
    public void InvalidateGroupCache()
    {
        _groupCache = null;
        _lastGroupUpdate = DateTime.MinValue;
    }

    /// <summary>
    /// Invalidate all caches
    /// </summary>
    public void InvalidateAll()
    {
        InvalidateTagCache();
        InvalidateGroupCache();
    }

    private void RefreshTagCacheIfNeeded(IEnumerable<NoteTag> tags)
    {
        if (_tagCache == null || DateTime.UtcNow - _lastTagUpdate > _cacheExpiry)
        {
            _tagCache = tags.ToDictionary(t => t.Id);
            _lastTagUpdate = DateTime.UtcNow;
        }
    }

    private void RefreshGroupCacheIfNeeded(IEnumerable<NoteGroup> groups)
    {
        if (_groupCache == null || DateTime.UtcNow - _lastGroupUpdate > _cacheExpiry)
        {
            _groupCache = groups.ToDictionary(g => g.Id);
            _lastGroupUpdate = DateTime.UtcNow;
        }
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
        if (disposing)
        {
            _tagCache?.Clear();
            _groupCache?.Clear();
            _tagCache = null;
            _groupCache = null;
        }
    }
}
