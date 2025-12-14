using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing Kanban board functionality
/// </summary>
public class KanbanService : IKanbanService
{
    private readonly INoteService _noteService;
    private readonly ISaveQueueService _saveQueueService;

    public KanbanService(INoteService noteService, ISaveQueueService saveQueueService)
    {
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        _saveQueueService = saveQueueService ?? throw new ArgumentNullException(nameof(saveQueueService));
    }

    /// <summary>
    /// Updates the Kanban status of a note (Requirements 5.2)
    /// </summary>
    public async Task<bool> UpdateNoteStatusAsync(Guid noteId, KanbanStatus status)
    {
        try
        {
            var notes = _noteService.GetAllNotes();
            var note = notes.FirstOrDefault(n => n.Id == noteId);
            
            if (note == null)
            {
                return false;
            }

            note.KanbanStatus = status;
            _noteService.UpdateNote(note);
            
            // Queue the note for saving
            _saveQueueService.QueueNote(note);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all notes with a specific Kanban status (Requirements 5.3)
    /// </summary>
    public async Task<IReadOnlyList<Note>> GetNotesByStatusAsync(KanbanStatus status)
    {
        try
        {
            var notes = _noteService.GetAllNotes();
            return notes.Where(n => n.KanbanStatus == status).ToList().AsReadOnly();
        }
        catch
        {
            return new List<Note>().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets all notes organized by their Kanban status (Requirements 5.3)
    /// Notes without a KanbanStatus are treated as ToDo
    /// </summary>
    public async Task<Dictionary<KanbanStatus, IReadOnlyList<Note>>> GetAllKanbanNotesAsync()
    {
        try
        {
            var notes = _noteService.GetAllNotes();
            
            var result = new Dictionary<KanbanStatus, IReadOnlyList<Note>>();
            
            // Initialize all status categories
            foreach (KanbanStatus status in Enum.GetValues<KanbanStatus>())
            {
                // Notes with null KanbanStatus are treated as ToDo
                var statusNotes = status == KanbanStatus.ToDo
                    ? notes.Where(n => n.KanbanStatus == status || n.KanbanStatus == null).ToList()
                    : notes.Where(n => n.KanbanStatus == status).ToList();
                result[status] = statusNotes.AsReadOnly();
            }

            return result;
        }
        catch
        {
            // Return empty collections for all statuses on error
            var result = new Dictionary<KanbanStatus, IReadOnlyList<Note>>();
            foreach (KanbanStatus status in Enum.GetValues<KanbanStatus>())
            {
                result[status] = new List<Note>().AsReadOnly();
            }
            return result;
        }
    }
}