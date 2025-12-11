using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services.Fallbacks;

/// <summary>
/// Fallback implementation of INoteService with minimal functionality
/// Used when the primary NoteService fails to initialize
/// </summary>
public class FallbackNoteService : INoteService
{
    private readonly List<Note> _notes = new();
    private readonly IErrorHandler _errorHandler;

    public FallbackNoteService(IErrorHandler errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public Note CreateNote()
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var now = DateTime.UtcNow;
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Untitled Note", // Hardcoded fallback
                Content = string.Empty,
                Language = "PlainText",
                IsPinned = true,
                Opacity = 0.9, // Default opacity
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
        },
        new Note { Id = Guid.NewGuid(), Title = "Error Note", Content = "Failed to create note" },
        "FallbackNoteService.CreateNote");
    }

    public void DeleteNote(Guid id)
    {
        _errorHandler.HandleWithFallback(() =>
        {
            for (int i = 0; i < _notes.Count; i++)
            {
                if (_notes[i].Id == id)
                {
                    _notes.RemoveAt(i);
                    break;
                }
            }
            return true;
        },
        false,
        "FallbackNoteService.DeleteNote");
    }

    public void UpdateNote(Note note)
    {
        _errorHandler.HandleWithFallback(() =>
        {
            var index = _notes.FindIndex(n => n.Id == note.Id);
            if (index >= 0)
            {
                note.ModifiedDate = DateTime.UtcNow;
                _notes[index] = note;
            }
            return true;
        },
        false,
        "FallbackNoteService.UpdateNote");
    }

    public IReadOnlyList<Note> GetAllNotes()
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            return _notes.AsReadOnly();
        },
        new List<Note>().AsReadOnly(),
        "FallbackNoteService.GetAllNotes");
    }

    public Note? GetNoteById(Guid id)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            foreach (var note in _notes)
            {
                if (note.Id == id)
                {
                    return note;
                }
            }
            return null;
        },
        null,
        "FallbackNoteService.GetNoteById");
    }

    public void TogglePin(Guid id)
    {
        _errorHandler.HandleWithFallback(() =>
        {
            var note = GetNoteById(id);
            if (note != null)
            {
                note.IsPinned = !note.IsPinned;
            }
            return true;
        },
        false,
        "FallbackNoteService.TogglePin");
    }

    public double AdjustOpacity(Guid id, double step)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var note = GetNoteById(id);
            if (note != null)
            {
                note.Opacity = OpacityHelper.Adjust(note.Opacity, step);
                return note.Opacity;
            }
            return OpacityHelper.DefaultOpacity;
        },
        OpacityHelper.DefaultOpacity,
        "FallbackNoteService.AdjustOpacity");
    }

    public void LoadNotes(IEnumerable<Note> notes)
    {
        _errorHandler.HandleWithFallback(() =>
        {
            _notes.Clear();
            _notes.AddRange(notes);
            return true;
        },
        false,
        "FallbackNoteService.LoadNotes");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notes.Clear();
        }
    }
}