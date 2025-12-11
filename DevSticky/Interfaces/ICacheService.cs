using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for caching service with statistics tracking.
/// Provides efficient caching of tags and groups with hit/miss tracking.
/// </summary>
public interface ICacheService : IDisposable
{
    /// <summary>
    /// Gets a tag by ID from the cache.
    /// </summary>
    /// <param name="tagId">The tag ID to look up</param>
    /// <returns>The tag if found, null otherwise</returns>
    NoteTag? GetTag(Guid tagId);

    /// <summary>
    /// Gets a group by ID from the cache.
    /// </summary>
    /// <param name="groupId">The group ID to look up</param>
    /// <returns>The group if found, null otherwise</returns>
    NoteGroup? GetGroup(Guid groupId);

    /// <summary>
    /// Gets multiple tags by IDs efficiently.
    /// </summary>
    /// <param name="tagIds">The tag IDs to look up</param>
    /// <returns>List of found tags</returns>
    List<NoteTag> GetTags(IEnumerable<Guid> tagIds);

    /// <summary>
    /// Invalidates the tag cache, forcing a refresh on next access.
    /// </summary>
    void InvalidateTagCache();

    /// <summary>
    /// Invalidates the group cache, forcing a refresh on next access.
    /// </summary>
    void InvalidateGroupCache();

    /// <summary>
    /// Invalidates all caches.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Gets cache statistics including hit/miss rates.
    /// </summary>
    /// <returns>Cache statistics</returns>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Statistics about cache performance.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total number of cache hits (successful lookups).
    /// </summary>
    public long TotalHits { get; set; }

    /// <summary>
    /// Total number of cache misses (failed lookups).
    /// </summary>
    public long TotalMisses { get; set; }

    /// <summary>
    /// Total number of cache requests (hits + misses).
    /// </summary>
    public long TotalRequests => TotalHits + TotalMisses;

    /// <summary>
    /// Cache hit rate as a percentage (0-100).
    /// </summary>
    public double HitRate => TotalRequests > 0 ? (double)TotalHits / TotalRequests * 100 : 0;

    /// <summary>
    /// Current number of items in the tag cache.
    /// </summary>
    public int TagCacheSize { get; set; }

    /// <summary>
    /// Current number of items in the group cache.
    /// </summary>
    public int GroupCacheSize { get; set; }

    /// <summary>
    /// Maximum size of the tag cache.
    /// </summary>
    public int TagCacheMaxSize { get; set; }

    /// <summary>
    /// Maximum size of the group cache.
    /// </summary>
    public int GroupCacheMaxSize { get; set; }

    /// <summary>
    /// Number of times the tag cache was invalidated.
    /// </summary>
    public long TagCacheInvalidations { get; set; }

    /// <summary>
    /// Number of times the group cache was invalidated.
    /// </summary>
    public long GroupCacheInvalidations { get; set; }
}
