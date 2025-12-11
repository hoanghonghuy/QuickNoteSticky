using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for batching and queuing save operations
/// </summary>
public interface ISaveQueueService : IDisposable
{
    /// <summary>
    /// Queues a note for saving
    /// </summary>
    /// <param name="note">The note to queue for saving</param>
    void QueueNote(Note note);

    /// <summary>
    /// Queues multiple notes for saving
    /// </summary>
    /// <param name="notes">The notes to queue for saving</param>
    void QueueNotes(IEnumerable<Note> notes);

    /// <summary>
    /// Forces immediate processing of the save queue
    /// </summary>
    /// <returns>Task representing the save operation</returns>
    Task FlushAsync();

    /// <summary>
    /// Gets the count of notes currently in the save queue
    /// </summary>
    int QueueCount { get; }

    /// <summary>
    /// Event raised when save operations complete
    /// </summary>
    event EventHandler<SaveCompletedEventArgs>? SaveCompleted;
}

/// <summary>
/// Event arguments for save completion events
/// </summary>
public class SaveCompletedEventArgs : EventArgs
{
    public int SavedNotesCount { get; set; }
    public bool Success { get; set; }
    public Exception? Error { get; set; }
}