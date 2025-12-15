using System.Timers;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service for automatic memory cleanup of unused note content with lazy loading support
/// </summary>
public class MemoryCleanupService : IMemoryCleanupService
{
    private readonly Dictionary<Guid, DateTime> _accessTimes = new();
    private readonly Dictionary<Guid, DateTime> _closedTimes = new();
    private readonly HashSet<Guid> _loadedNotes = new();
    private readonly System.Timers.Timer _cleanupTimer;
    private readonly ICacheService? _cacheService;
    private readonly INoteService? _noteService;
    private readonly object _lock = new();
    
    private const int CleanupIntervalMinutes = 5;
    private const int InactiveThresholdMinutes = 10;
    
    private DateTime _lastCleanupTime = DateTime.MinValue;
    private int _lastCleanupCount;
    private bool _disposed;

    public event EventHandler<CleanupEventArgs>? CleanupPerformed;

    public MemoryCleanupService(ICacheService? cacheService = null, INoteService? noteService = null)
    {
        _cacheService = cacheService;
        _noteService = noteService;
        
        _cleanupTimer = new System.Timers.Timer(CleanupIntervalMinutes * 60 * 1000);
        _cleanupTimer.Elapsed += OnCleanupTimerElapsed;
    }

    public void Start()
    {
        if (_disposed) return;
        _cleanupTimer.Start();
    }

    public void Stop()
    {
        _cleanupTimer.Stop();
    }

    public void CleanupNow()
    {
        if (_disposed) return;
        
        var memoryBefore = GC.GetTotalMemory(false);
        var cleanedCount = 0;
        var threshold = DateTime.UtcNow.AddMinutes(-InactiveThresholdMinutes);
        
        List<Guid> toCleanup;
        lock (_lock)
        {
            // Find notes that have been closed for longer than threshold
            toCleanup = _closedTimes
                .Where(kvp => kvp.Value < threshold)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var noteId in toCleanup)
            {
                _closedTimes.Remove(noteId);
                _loadedNotes.Remove(noteId);
                _accessTimes.Remove(noteId);
                cleanedCount++;
            }
        }
        
        // Unload content for cleaned up notes (lazy loading support)
        if (_noteService != null)
        {
            foreach (var noteId in toCleanup)
            {
                _noteService.UnloadNoteContent(noteId);
            }
        }
        
        // Force garbage collection if we cleaned up items
        if (cleanedCount > 0)
        {
            GC.Collect(2, GCCollectionMode.Optimized);
            GC.WaitForPendingFinalizers();
        }
        
        var memoryAfter = GC.GetTotalMemory(true);
        
        _lastCleanupTime = DateTime.UtcNow;
        _lastCleanupCount = cleanedCount;
        
        System.Diagnostics.Debug.WriteLine($"[MemoryCleanupService] Cleaned up {cleanedCount} notes, freed {(memoryBefore - memoryAfter) / 1024}KB");
        
        CleanupPerformed?.Invoke(this, new CleanupEventArgs
        {
            ItemsCleaned = cleanedCount,
            MemoryFreedBytes = Math.Max(0, memoryBefore - memoryAfter),
            CleanupTime = _lastCleanupTime
        });
    }

    public void MarkAccessed(Guid noteId)
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            _accessTimes[noteId] = DateTime.UtcNow;
            _loadedNotes.Add(noteId);
            _closedTimes.Remove(noteId); // Remove from closed list if present
        }
    }

    public void MarkClosed(Guid noteId)
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            _closedTimes[noteId] = DateTime.UtcNow;
        }
    }

    public MemoryStats GetStats()
    {
        lock (_lock)
        {
            return new MemoryStats
            {
                TotalMemoryBytes = GC.GetTotalMemory(false),
                ManagedMemoryBytes = GC.GetTotalMemory(false),
                LoadedNotesCount = _loadedNotes.Count,
                CachedItemsCount = _accessTimes.Count,
                LastCleanupTime = _lastCleanupTime,
                ItemsCleanedLastRun = _lastCleanupCount
            };
        }
    }

    private void OnCleanupTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        CleanupNow();
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
            _cleanupTimer.Stop();
            _cleanupTimer.Dispose();
            
            lock (_lock)
            {
                _accessTimes.Clear();
                _closedTimes.Clear();
                _loadedNotes.Clear();
            }
        }
        
        _disposed = true;
    }
}
