using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing Kanban board functionality
/// </summary>
public interface IKanbanService
{
    /// <summary>
    /// Updates the Kanban status of a note
    /// </summary>
    /// <param name="noteId">The ID of the note to update</param>
    /// <param name="status">The new Kanban status</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    Task<bool> UpdateNoteStatusAsync(Guid noteId, KanbanStatus status);

    /// <summary>
    /// Gets all notes with a specific Kanban status
    /// </summary>
    /// <param name="status">The Kanban status to filter by</param>
    /// <returns>A read-only list of notes with the specified status</returns>
    Task<IReadOnlyList<Note>> GetNotesByStatusAsync(KanbanStatus status);

    /// <summary>
    /// Gets all notes organized by their Kanban status
    /// </summary>
    /// <returns>A dictionary mapping Kanban status to lists of notes</returns>
    Task<Dictionary<KanbanStatus, IReadOnlyList<Note>>> GetAllKanbanNotesAsync();
}