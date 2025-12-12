namespace DevSticky.Interfaces;

/// <summary>
/// Service for automatic memory cleanup of unused resources
/// </summary>
public interface IMemoryCleanupService : IDisposable
{
    /// <summary>
    /// Starts automatic cleanup timer
    /// </summary>
    void Start();
    
    /// <summary>
    /// Stops automatic cleanup timer
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Performs cleanup immediately
    /// </summary>
    void CleanupNow();
    
    /// <summary>
    /// Marks a note as recently accessed
    /// </summary>
    void MarkAccessed(Guid noteId);
    
    /// <summary>
    /// Marks a note as closed (candidate for cleanup)
    /// </summary>
    void MarkClosed(Guid noteId);
    
    /// <summary>
    /// Gets memory usage statistics
    /// </summary>
    MemoryStats GetStats();
    
    /// <summary>
    /// Event raised when cleanup is performed
    /// </summary>
    event EventHandler<CleanupEventArgs>? CleanupPerformed;
}

/// <summary>
/// Memory usage statistics
/// </summary>
public class MemoryStats
{
    public long TotalMemoryBytes { get; set; }
    public long ManagedMemoryBytes { get; set; }
    public int LoadedNotesCount { get; set; }
    public int CachedItemsCount { get; set; }
    public DateTime LastCleanupTime { get; set; }
    public int ItemsCleanedLastRun { get; set; }
}

/// <summary>
/// Event args for cleanup events
/// </summary>
public class CleanupEventArgs : EventArgs
{
    public int ItemsCleaned { get; set; }
    public long MemoryFreedBytes { get; set; }
    public DateTime CleanupTime { get; set; }
}
