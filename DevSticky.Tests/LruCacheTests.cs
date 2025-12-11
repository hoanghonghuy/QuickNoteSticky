using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for LruCache
/// </summary>
public class LruCacheTests : IDisposable
{
    private readonly LruCache<string, int> _cache;

    public LruCacheTests()
    {
        _cache = new LruCache<string, int>(3);
    }

    [Fact]
    public void Constructor_WithValidMaxSize_CreatesCache()
    {
        // Arrange & Act
        var cache = new LruCache<string, int>(5);

        // Assert
        Assert.Equal(5, cache.MaxSize);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Constructor_WithZeroMaxSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new LruCache<string, int>(0));
    }

    [Fact]
    public void Constructor_WithNegativeMaxSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new LruCache<string, int>(-1));
    }

    [Fact]
    public void Add_NewItem_IncreasesCount()
    {
        // Act
        _cache.Add("key1", 1);

        // Assert
        Assert.Equal(1, _cache.Count);
    }

    [Fact]
    public void Add_ExistingKey_UpdatesValue()
    {
        // Arrange
        _cache.Add("key1", 1);

        // Act
        _cache.Add("key1", 10);

        // Assert
        Assert.Equal(1, _cache.Count);
        Assert.True(_cache.TryGetValue("key1", out var value));
        Assert.Equal(10, value);
    }

    [Fact]
    public void Add_ExceedsCapacity_EvictsLeastRecentlyUsed()
    {
        // Arrange - Fill cache to capacity
        _cache.Add("key1", 1);
        _cache.Add("key2", 2);
        _cache.Add("key3", 3);

        // Act - Add one more item
        _cache.Add("key4", 4);

        // Assert - First item should be evicted
        Assert.Equal(3, _cache.Count);
        Assert.False(_cache.TryGetValue("key1", out _));
        Assert.True(_cache.TryGetValue("key2", out _));
        Assert.True(_cache.TryGetValue("key3", out _));
        Assert.True(_cache.TryGetValue("key4", out _));
    }

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrue()
    {
        // Arrange
        _cache.Add("key1", 42);

        // Act
        var result = _cache.TryGetValue("key1", out var value);

        // Assert
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_NonExistentKey_ReturnsFalse()
    {
        // Act
        var result = _cache.TryGetValue("nonexistent", out var value);

        // Assert
        Assert.False(result);
        Assert.Equal(0, value); // default int value
    }

    [Fact]
    public void TryGetValue_UpdatesLruOrder()
    {
        // Arrange - Fill cache
        _cache.Add("key1", 1);
        _cache.Add("key2", 2);
        _cache.Add("key3", 3);

        // Act - Access first item to make it most recently used
        _cache.TryGetValue("key1", out _);
        
        // Add new item to trigger eviction
        _cache.Add("key4", 4);

        // Assert - key2 should be evicted (was least recently used)
        Assert.False(_cache.TryGetValue("key2", out _));
        Assert.True(_cache.TryGetValue("key1", out _)); // Should still exist
        Assert.True(_cache.TryGetValue("key3", out _));
        Assert.True(_cache.TryGetValue("key4", out _));
    }

    [Fact]
    public void Remove_ExistingKey_RemovesItem()
    {
        // Arrange
        _cache.Add("key1", 1);
        _cache.Add("key2", 2);

        // Act
        _cache.Remove("key1");

        // Assert
        Assert.Equal(1, _cache.Count);
        Assert.False(_cache.TryGetValue("key1", out _));
        Assert.True(_cache.TryGetValue("key2", out _));
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNothing()
    {
        // Arrange
        _cache.Add("key1", 1);

        // Act
        _cache.Remove("nonexistent");

        // Assert
        Assert.Equal(1, _cache.Count);
        Assert.True(_cache.TryGetValue("key1", out _));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        _cache.Add("key1", 1);
        _cache.Add("key2", 2);
        _cache.Add("key3", 3);

        // Act
        _cache.Clear();

        // Assert
        Assert.Equal(0, _cache.Count);
        Assert.False(_cache.TryGetValue("key1", out _));
        Assert.False(_cache.TryGetValue("key2", out _));
        Assert.False(_cache.TryGetValue("key3", out _));
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentOperations_MaintainsConsistency()
    {
        // Arrange
        var cache = new LruCache<int, string>(100);
        var tasks = new List<Task>();

        // Act - Perform concurrent operations
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var key = taskId * 100 + j;
                    cache.Add(key, $"value{key}");
                    cache.TryGetValue(key, out _);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert - Cache should not exceed max size
        Assert.True(cache.Count <= 100);
    }

    [Fact]
    public void Dispose_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new LruCache<string, int>(5);
        cache.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => cache.Add("key", 1));
        Assert.Throws<ObjectDisposedException>(() => cache.TryGetValue("key", out _));
        Assert.Throws<ObjectDisposedException>(() => cache.Remove("key"));
        Assert.Throws<ObjectDisposedException>(() => cache.Clear());
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act & Assert - Should not throw
        _cache.Dispose();
        _cache.Dispose();
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}