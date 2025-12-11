namespace DevSticky.Interfaces;

/// <summary>
/// Interface for tracking dirty (modified) items
/// </summary>
/// <typeparam name="T">The type of items to track</typeparam>
public interface IDirtyTracker<T> where T : class
{
    /// <summary>
    /// Starts tracking an item
    /// </summary>
    /// <param name="item">The item to track</param>
    void Track(T item);

    /// <summary>
    /// Marks an item as dirty (modified)
    /// </summary>
    /// <param name="item">The item to mark as dirty</param>
    void MarkDirty(T item);

    /// <summary>
    /// Marks an item as clean (not modified)
    /// </summary>
    /// <param name="item">The item to mark as clean</param>
    void MarkClean(T item);

    /// <summary>
    /// Gets all items that are currently marked as dirty
    /// </summary>
    /// <returns>Collection of dirty items</returns>
    IEnumerable<T> GetDirtyItems();

    /// <summary>
    /// Clears all tracked items
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the count of tracked items
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the count of dirty items
    /// </summary>
    int DirtyCount { get; }
}
