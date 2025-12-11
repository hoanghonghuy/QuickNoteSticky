using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing notes (CRUD operations)
/// </summary>
public class NoteService : INoteService, IDisposable
{
    private readonly List<Note> _notes = new();
    private readonly AppSettings _settings;

    public NoteService(AppSettings settings)
    {
        _settings = settings;
    }

    public Note CreateNote()
    {
        var now = DateTime.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = L.Get("UntitledNote"),
            Content = string.Empty,
            Language = "PlainText",
            IsPinned = true,
            Opacity = OpacityHelper.Clamp(_settings.DefaultOpacity),
            WindowRect = new WindowRect
            {
                Top = 100,
                Left = 100,
                Width = WindowRect.DefaultWidth,
                Height = WindowRect.DefaultHeight
            },
            CreatedDate = now,
            ModifiedDate = now
        };

        _notes.Add(note);
        return note;
    }

    public void DeleteNote(Guid id)
    {
        // Optimized: Direct search and removal without LINQ
        for (int i = 0; i < _notes.Count; i++)
        {
            if (_notes[i].Id == id)
            {
                _notes.RemoveAt(i);
                break;
            }
        }
    }

    public void UpdateNote(Note note)
    {
        var index = _notes.FindIndex(n => n.Id == note.Id);
        if (index >= 0)
        {
            note.ModifiedDate = DateTime.UtcNow;
            _notes[index] = note;
        }
    }


    public IReadOnlyList<Note> GetAllNotes() => _notes.AsReadOnly();

    public Note? GetNoteById(Guid id)
    {
        // Optimized: Direct search without LINQ
        foreach (var note in _notes)
        {
            if (note.Id == id)
            {
                return note;
            }
        }
        return null;
    }

    public void TogglePin(Guid id)
    {
        var note = GetNoteById(id);
        if (note != null)
        {
            note.IsPinned = !note.IsPinned;
        }
    }

    public double AdjustOpacity(Guid id, double step)
    {
        var note = GetNoteById(id);
        if (note != null)
        {
            note.Opacity = OpacityHelper.Adjust(note.Opacity, step);
            return note.Opacity;
        }
        return OpacityHelper.DefaultOpacity;
    }

    /// <summary>
    /// Loads notes from external source (used during app startup)
    /// </summary>
    public void LoadNotes(IEnumerable<Note> notes)
    {
        _notes.Clear();
        _notes.AddRange(notes);
    }

    /// <summary>
    /// Disposes the note service and releases all resources.
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
        if (disposing)
        {
            _notes.Clear();
        }
    }
}
