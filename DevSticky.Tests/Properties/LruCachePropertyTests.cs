using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for LRU Cache eviction behavior
/// **Feature: code-refactor, Property 1: LRU Cache Eviction Order**
/// **Validates: Requirements 4.3**
/// </summary>
public class LruCachePropertyTests
{
    /// <summary>
    /// Property 1: LRU Cache Eviction Order
    /// For any LRU cache with max size N, when adding N+1 items, 
    /// the first item added should be evicted (least recently used).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LruCache_WhenExceedingCapacity_ShouldEvictLeastRecentlyUsedItem()
    {
        return Prop.ForAll(
            CacheSizeAndItemsGenerator(),
            data =>
            {
                var (maxSize, items) = data;
                
                // Create cache with specified max size
                using var cache = new LruCache<int, string>(maxSize);
                
                // Add N items (fill to capacity)
                for (int i = 0; i < maxSize; i++)
                {
                    cache.Add(items[i].Key, items[i].Value);
                }
                
                // Verify cache is at capacity
                if (cache.Count != maxSize)
                {
                    return false.ToProperty().Label($"Cache should be at capacity {maxSize}, but was {cache.Count}");
                }
                
                // Store the first key that should be evicted
                var firstKey = items[0].Key;
                
                // Add one more item (N+1), which should evict the first item
                var newItem = items[maxSize];
                cache.Add(newItem.Key, newItem.Value);
                
                // Verify the first item was evicted
                var firstItemExists = cache.TryGetValue(firstKey, out _);
                
                // Verify the new item exists
                var newItemExists = cache.TryGetValue(newItem.Key, out var retrievedValue);
                
                // Verify cache size is still at max
                var sizeCorrect = cache.Count == maxSize;
                
                return (!firstItemExists && newItemExists && retrievedValue == newItem.Value && sizeCorrect)
                    .ToProperty()
                    .Label($"First item (key={firstKey}) should be evicted, new item (key={newItem.Key}) should exist");
            });
    }
    
    /// <summary>
    /// Property 2: LRU Cache Access Updates Recency
    /// For any LRU cache, accessing an item should move it to the most recently used position,
    /// preventing it from being evicted when new items are added.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LruCache_WhenAccessingItem_ShouldUpdateRecency()
    {
        return Prop.ForAll(
            CacheSizeAndItemsGenerator(),
            data =>
            {
                var (maxSize, items) = data;
                
                // Need at least 2 items to test this property
                if (maxSize < 2)
                {
                    return true.ToProperty();
                }
                
                using var cache = new LruCache<int, string>(maxSize);
                
                // Add N items (fill to capacity)
                for (int i = 0; i < maxSize; i++)
                {
                    cache.Add(items[i].Key, items[i].Value);
                }
                
                // Access the first item (should move it to most recently used)
                var firstKey = items[0].Key;
                cache.TryGetValue(firstKey, out _);
                
                // Add one more item, which should evict the second item (now least recently used)
                var newItem = items[maxSize];
                cache.Add(newItem.Key, newItem.Value);
                
                // The first item should still exist (was accessed, so not LRU)
                var firstItemExists = cache.TryGetValue(firstKey, out _);
                
                // The second item should be evicted (was LRU after first was accessed)
                var secondKey = items[1].Key;
                var secondItemExists = cache.TryGetValue(secondKey, out _);
                
                return (firstItemExists && !secondItemExists)
                    .ToProperty()
                    .Label($"First item (key={firstKey}) should exist after access, second item (key={secondKey}) should be evicted");
            });
    }
    
    /// <summary>
    /// Property 3: LRU Cache Size Never Exceeds Maximum
    /// For any sequence of operations, the cache size should never exceed the specified maximum.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LruCache_AfterAnyOperations_ShouldNeverExceedMaxSize()
    {
        return Prop.ForAll(
            CacheSizeAndItemsGenerator(),
            data =>
            {
                var (maxSize, items) = data;
                
                using var cache = new LruCache<int, string>(maxSize);
                
                // Add more items than capacity
                var itemsToAdd = Math.Min(items.Count, maxSize * 2);
                for (int i = 0; i < itemsToAdd; i++)
                {
                    cache.Add(items[i].Key, items[i].Value);
                    
                    // Check size after each add
                    if (cache.Count > maxSize)
                    {
                        return false.ToProperty().Label($"Cache size {cache.Count} exceeded max size {maxSize}");
                    }
                }
                
                return (cache.Count <= maxSize)
                    .ToProperty()
                    .Label($"Cache size {cache.Count} should not exceed max size {maxSize}");
            });
    }
    
    /// <summary>
    /// Property 4: LRU Cache Update Does Not Evict
    /// For any item in the cache, updating its value should not cause eviction
    /// and should maintain the cache size.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LruCache_WhenUpdatingExistingItem_ShouldNotEvict()
    {
        return Prop.ForAll(
            CacheSizeAndItemsGenerator(),
            data =>
            {
                var (maxSize, items) = data;
                
                using var cache = new LruCache<int, string>(maxSize);
                
                // Add N items (fill to capacity)
                for (int i = 0; i < maxSize; i++)
                {
                    cache.Add(items[i].Key, items[i].Value);
                }
                
                var initialCount = cache.Count;
                
                // Update the first item with a new value
                var firstKey = items[0].Key;
                var newValue = "UPDATED_" + items[0].Value;
                cache.Add(firstKey, newValue);
                
                // Verify count hasn't changed
                var countUnchanged = cache.Count == initialCount;
                
                // Verify the value was updated
                var valueUpdated = cache.TryGetValue(firstKey, out var retrievedValue) && retrievedValue == newValue;
                
                return (countUnchanged && valueUpdated)
                    .ToProperty()
                    .Label($"Update should not change count, and value should be updated");
            });
    }

    /// <summary>
    /// Generates a cache size and a list of unique key-value pairs for testing.
    /// Returns (maxSize, items) where items.Count = maxSize + 1 to test eviction.
    /// </summary>
    private static Arbitrary<(int maxSize, List<(int Key, string Value)> items)> CacheSizeAndItemsGenerator()
    {
        var gen = from maxSize in Gen.Choose(1, 20)
                  from itemCount in Gen.Constant(maxSize + 1)
                  from keys in Gen.ArrayOf(itemCount, Arb.Generate<int>())
                  from values in Gen.ArrayOf(itemCount, Arb.Generate<NonEmptyString>())
                  let uniqueKeys = keys.Distinct().ToArray()
                  where uniqueKeys.Length >= itemCount
                  let items = uniqueKeys.Take(itemCount)
                      .Zip(values.Take(itemCount), (k, v) => (Key: k, Value: v.Get))
                      .ToList()
                  select (maxSize, items);

        return Arb.From(gen);
    }
}
