using System.Collections.Concurrent;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for batching and queuing save operations to improve performance
/// </summary>
public class SaveQueueService : ISaveQueueService
{
    private readonly IStorageService _storageService;
    private readonly IErrorHandler _errorHandler;
    private readonly IDebounceService _debounceService;
    private readonly ConcurrentDictionary<Guid, Note> _queuedNotes = new();
    private readonly object _flushLock = new();
    private AppData? _currentAppData;
    private bool _disposed;

    private const int BatchDelayMs = 500; // Delay before processing batch
    private const string DebounceKey = "SaveQueue_Flush";

    public SaveQueueService(
        IStorageService storageService,
        IErrorHandler errorHandler,
        IDebounceService debounceService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _debounceService = debounceService ?? throw new ArgumentNullException(nameof(debounceService));
    }

    /// <summary>
    /// Gets the count of notes currently in the save queue
    /// </summary>
    public int QueueCount => _queuedNotes.Count;

    /// <summary>
    /// Event raised when save operations complete
    /// </summary>
    public event EventHandler<SaveCompletedEventArgs>? SaveCompleted;

    /// <summary>
    /// Sets the current application data for incremental saves
    /// </summary>
    /// <param name="appData">The current application data</param>
    public void SetCurrentAppData(AppData appData)
    {
        _currentAppData = appData ?? throw new ArgumentNullException(nameof(appData));
    }

    /// <summary>
    /// Queues a note for saving
    /// </summary>
    /// <param name="note">The note to queue for saving</param>
    public void QueueNote(Note note)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        if (_disposed)
            return;

        _queuedNotes.AddOrUpdate(note.Id, note, (key, existing) => note);

        // Debounce the flush operation to batch multiple saves
        _debounceService.Debounce(DebounceKey, ProcessQueue, BatchDelayMs);
    }

    /// <summary>
    /// Queues multiple notes for saving
    /// </summary>
    /// <param name="notes">The notes to queue for saving</param>
    public void QueueNotes(IEnumerable<Note> notes)
    {
        if (notes == null)
            throw new ArgumentNullException(nameof(notes));

        if (_disposed)
            return;

        foreach (var note in notes)
        {
            _queuedNotes.AddOrUpdate(note.Id, note, (key, existing) => note);
        }

        // Debounce the flush operation to batch multiple saves
        _debounceService.Debounce(DebounceKey, ProcessQueue, BatchDelayMs);
    }

    /// <summary>
    /// Forces immediate processing of the save queue
    /// </summary>
    /// <returns>Task representing the save operation</returns>
    public async Task FlushAsync()
    {
        if (_disposed)
            return;

        // Cancel any pending debounced operation
        _debounceService.Cancel(DebounceKey);

        await ProcessQueueAsync();
    }

    /// <summary>
    /// Synchronous wrapper for ProcessQueueAsync for use with debounce service
    /// </summary>
    private void ProcessQueue()
    {
        _ = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Processes the current save queue
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        if (_disposed || _queuedNotes.IsEmpty)
            return;

        lock (_flushLock)
        {
            if (_queuedNotes.IsEmpty)
                return;
        }

        var success = false;
        var savedCount = 0;
        Exception? error = null;

        try
        {
            // Get all queued notes and clear the queue atomically
            var notesToSave = new List<Note>();
            var keys = _queuedNotes.Keys.ToList();
            
            foreach (var key in keys)
            {
                if (_queuedNotes.TryRemove(key, out var note))
                {
                    notesToSave.Add(note);
                }
            }

            if (notesToSave.Count == 0)
                return;

            savedCount = notesToSave.Count;

            // Use incremental save if we have current app data, otherwise fall back to full save
            if (_currentAppData != null)
            {
                await _storageService.SaveNotesAsync(notesToSave, _currentAppData);
            }
            else
            {
                // Fallback: create minimal AppData for full save
                var appData = new AppData
                {
                    AppSettings = new AppSettings(),
                    Notes = notesToSave,
                    Groups = new List<NoteGroup>(),
                    Tags = new List<NoteTag>()
                };
                await _storageService.SaveAsync(appData);
            }

            success = true;
        }
        catch (Exception ex)
        {
            error = ex;
            _errorHandler.Handle(ex, "SaveQueueService.ProcessQueueAsync - Processing save queue");
        }

        // Raise completion event
        SaveCompleted?.Invoke(this, new SaveCompletedEventArgs
        {
            SavedNotesCount = savedCount,
            Success = success,
            Error = error
        });
    }

    /// <summary>
    /// Disposes the service and flushes any remaining queued saves
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
            return;

        _disposed = true;

        if (disposing)
        {
            // Flush any remaining saves synchronously
            try
            {
                FlushAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _errorHandler.Handle(ex, "SaveQueueService.Dispose - Flushing remaining saves");
            }

            _queuedNotes.Clear();
        }
    }
}