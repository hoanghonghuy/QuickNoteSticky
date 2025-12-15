using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing notes (CRUD operations)
/// </summary>
public interface INoteService : IDisposable
{
    Note CreateNote();
    
    /// <summary>
    /// Adds an existing note to the service (e.g., from template)
    /// </summary>
    void AddNote(Note note);
    
    void DeleteNote(Guid id);
    void UpdateNote(Note note);
    IReadOnlyList<Note> GetAllNotes();
    Note? GetNoteById(Guid id);
    void TogglePin(Guid id);
    double AdjustOpacity(Guid id, double step);
    
    /// <summary>
    /// Loads notes from external source (used during app startup)
    /// </summary>
    void LoadNotes(IEnumerable<Note> notes);
    
    // Lazy loading support
    /// <summary>
    /// Preloads content for multiple notes (e.g., recent notes)
    /// </summary>
    Task PreloadContentsAsync(IEnumerable<Guid> noteIds);
    
    /// <summary>
    /// Ensures note content is loaded (for lazy loading)
    /// </summary>
    Task<bool> EnsureContentLoadedAsync(Guid noteId);
    
    /// <summary>
    /// Unloads content for a note to free memory
    /// </summary>
    void UnloadNoteContent(Guid noteId);
    
    /// <summary>
    /// Gets note content, loading it if necessary
    /// </summary>
    Task<string?> GetNoteContentAsync(Guid noteId);
    
    /// <summary>
    /// Saves note content (for lazy loading mode)
    /// </summary>
    Task SaveNoteContentAsync(Guid noteId, string content);
}
