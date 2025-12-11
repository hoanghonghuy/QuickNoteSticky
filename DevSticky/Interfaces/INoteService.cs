using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing notes (CRUD operations)
/// </summary>
public interface INoteService : IDisposable
{
    Note CreateNote();
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
}
