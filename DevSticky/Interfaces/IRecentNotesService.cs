using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for tracking recently accessed notes
/// </summary>
public interface IRecentNotesService
{
    /// <summary>
    /// Records that a note was accessed
    /// </summary>
    void RecordAccess(Guid noteId);
    
    /// <summary>
    /// Gets the list of recently accessed note IDs (most recent first)
    /// </summary>
    IReadOnlyList<Guid> GetRecentNoteIds();
    
    /// <summary>
    /// Gets the list of recently accessed notes with metadata
    /// </summary>
    IReadOnlyList<RecentNoteInfo> GetRecentNotes();
    
    /// <summary>
    /// Removes a note from recent history (e.g., when deleted)
    /// </summary>
    void RemoveFromHistory(Guid noteId);
    
    /// <summary>
    /// Clears all recent history
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Maximum number of recent notes to track
    /// </summary>
    int MaxRecentNotes { get; set; }
    
    /// <summary>
    /// Event raised when recent notes list changes
    /// </summary>
    event EventHandler? RecentNotesChanged;
}

/// <summary>
/// Information about a recently accessed note
/// </summary>
public class RecentNoteInfo
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime LastAccessedAt { get; set; }
    public string ContentPreview { get; set; } = string.Empty;
}
