using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing notes (CRUD operations)
/// </summary>
public interface INoteService
{
    Note CreateNote();
    void DeleteNote(Guid id);
    void UpdateNote(Note note);
    IReadOnlyList<Note> GetAllNotes();
    Note? GetNoteById(Guid id);
    void TogglePin(Guid id);
    double AdjustOpacity(Guid id, double step);
}
