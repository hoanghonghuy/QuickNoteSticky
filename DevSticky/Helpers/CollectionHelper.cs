namespace DevSticky.Helpers;

/// <summary>
/// Helper class for common collection operations.
/// Provides utilities for working with lists, arrays, and other collections.
/// </summary>
public static class CollectionHelper
{
    /// <summary>
    /// Checks if a collection is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="collection">The collection to check</param>
    /// <returns>True if the collection is null or empty; otherwise false</returns>
    public static bool IsNullOrEmpty<T>(IEnumerable<T>? collection)
    {
        return collection == null || !collection.Any();
    }

    /// <summary>
    /// Checks if a collection has any elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="collection">The collection to check</param>
    /// <returns>True if the collection has elements; otherwise false</returns>
    public static bool HasElements<T>(IEnumerable<T>? collection)
    {
        return collection != null && collection.Any();
    }

    /// <summary>
    /// Returns an empty collection if the input is null.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="collection">The collection to check</param>
    /// <returns>The original collection or an empty collection if null</returns>
    public static IEnumerable<T> EmptyIfNull<T>(IEnumerable<T>? collection)
    {
        return collection ?? Enumerable.Empty<T>();
    }

    /// <summary>
    /// Safely gets an element at the specified index, returning a default value if out of range.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
    /// <param name="list">The list to access</param>
    /// <param name="index">The index to retrieve</param>
    /// <param name="defaultValue">The default value to return if index is out of range</param>
    /// <returns>The element at the index or the default value</returns>
    public static T GetOrDefault<T>(IList<T>? list, int index, T defaultValue = default!)
    {
        if (list == null || index < 0 || index >= list.Count)
            return defaultValue;

        return list[index];
    }

    /// <summary>
    /// Adds an item to a collection only if it's not already present.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="collection">The collection to add to</param>
    /// <param name="item">The item to add</param>
    /// <returns>True if the item was added; false if it already existed</returns>
    public static bool AddIfNotExists<T>(ICollection<T> collection, T item)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        if (collection.Contains(item))
            return false;

        collection.Add(item);
        return true;
    }

    /// <summary>
    /// Removes all items from a collection that match a predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="collection">The collection to remove from</param>
    /// <param name="predicate">The predicate to match items for removal</param>
    /// <returns>The number of items removed</returns>
    public static int RemoveWhere<T>(ICollection<T> collection, Func<T, bool> predicate)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        // Optimized: Avoid LINQ and collect items to remove in single pass
        var itemsToRemove = new List<T>();
        foreach (var item in collection)
        {
            if (predicate(item))
            {
                itemsToRemove.Add(item);
            }
        }

        foreach (var item in itemsToRemove)
        {
            collection.Remove(item);
        }

        return itemsToRemove.Count;
    }

    /// <summary>
    /// Partitions a collection into batches of a specified size.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="batchSize">The size of each batch</param>
    /// <returns>An enumerable of batches</returns>
    public static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> source, int batchSize)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be greater than zero", nameof(batchSize));

        var batch = new List<T>(batchSize);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// Creates a dictionary from a collection, handling duplicate keys by keeping the first occurrence.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <typeparam name="TValue">The type of the value</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="keySelector">Function to extract the key</param>
    /// <param name="valueSelector">Function to extract the value</param>
    /// <returns>A dictionary with unique keys</returns>
    public static Dictionary<TKey, TValue> ToDictionarySafe<TSource, TKey, TValue>(
        IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector) where TKey : notnull
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));
        if (valueSelector == null)
            throw new ArgumentNullException(nameof(valueSelector));

        var dictionary = new Dictionary<TKey, TValue>();
        foreach (var item in source)
        {
            var key = keySelector(item);
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = valueSelector(item);
            }
        }

        return dictionary;
    }

    /// <summary>
    /// Shuffles a list in place using the Fisher-Yates algorithm.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
    /// <param name="list">The list to shuffle</param>
    public static void Shuffle<T>(IList<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        var random = new Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    /// <summary>
    /// Optimized FirstOrDefault implementation for collections
    /// Avoids LINQ overhead for simple searches
    /// </summary>
    public static T? FirstOrDefaultOptimized<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : class
    {
        foreach (var item in source)
        {
            if (predicate(item))
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Optimized Count implementation for collections with predicate
    /// Avoids LINQ overhead for simple counting
    /// </summary>
    public static int CountOptimized<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        int count = 0;
        foreach (var item in source)
        {
            if (predicate(item))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Optimized Where implementation that returns a list directly
    /// Avoids deferred execution overhead when immediate materialization is needed
    /// </summary>
    public static List<T> WhereToList<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        var result = new List<T>();
        foreach (var item in source)
        {
            if (predicate(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Optimized Select implementation that returns a list directly
    /// Avoids deferred execution overhead when immediate materialization is needed
    /// </summary>
    public static List<TResult> SelectToList<T, TResult>(this IEnumerable<T> source, Func<T, TResult> selector)
    {
        var result = new List<TResult>();
        foreach (var item in source)
        {
            result.Add(selector(item));
        }
        return result;
    }
}
