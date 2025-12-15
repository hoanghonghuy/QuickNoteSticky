using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing notes (CRUD operations) with lazy loading support
/// </summary>
public class NoteService : INoteService, IDisposable
{
    private readonly List<Note> _notes = new();
    private readonly AppSettings _settings;
    private readonly IStorageService? _storageService;

    public NoteService(AppSettings settings, IStorageService? storageService = null)
    {
        _settings = settings;
        _storageService = storageService;
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

    public void AddNote(Note note)
    {
        if (note == null) return;
        
        // Check if note already exists
        var existingIndex = _notes.FindIndex(n => n.Id == note.Id);
        if (existingIndex >= 0)
        {
            _notes[existingIndex] = note;
        }
        else
        {
            _notes.Add(note);
        }
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
    
    #region Lazy Loading Support
    
    /// <summary>
    /// Preloads content for multiple notes (e.g., recent notes)
    /// </summary>
    public async Task PreloadContentsAsync(IEnumerable<Guid> noteIds)
    {
        if (_storageService == null || !_storageService.IsLazyLoadingFormat) return;
        
        var notesToLoad = noteIds
            .Select(GetNoteById)
            .Where(n => n != null && !n.IsContentLoaded)
            .Select(n => n!.Id)
            .ToList();
        
        if (notesToLoad.Count == 0) return;
        
        // Preload from storage
        await _storageService.PreloadNoteContentsAsync(notesToLoad).ConfigureAwait(false);
        
        // Update notes with loaded content
        foreach (var noteId in notesToLoad)
        {
            await EnsureContentLoadedAsync(noteId).ConfigureAwait(false);
        }
        
        System.Diagnostics.Debug.WriteLine($"[NoteService] Preloaded {notesToLoad.Count} notes");
    }
    
    public async Task<bool> EnsureContentLoadedAsync(Guid noteId)
    {
        var note = GetNoteById(noteId);
        if (note == null) return false;
        
        if (note.IsContentLoaded) return true;
        
        if (_storageService == null || !_storageService.IsLazyLoadingFormat)
        {
            note.IsContentLoaded = true;
            return true;
        }
        
        var content = await _storageService.LoadNoteContentAsync(noteId).ConfigureAwait(false);
        if (content != null)
        {
            note.Content = content;
            note.IsContentLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[NoteService] Loaded content for note {noteId}");
            return true;
        }
        
        // Content file not found, use preview as content
        note.Content = note.ContentPreview.TrimEnd('.', ' ');
        note.IsContentLoaded = true;
        return true;
    }
    
    public void UnloadNoteContent(Guid noteId)
    {
        var note = GetNoteById(noteId);
        if (note == null) return;
        
        if (_storageService?.IsLazyLoadingFormat == true)
        {
            note.UnloadContent();
            System.Diagnostics.Debug.WriteLine($"[NoteService] Unloaded content for note {noteId}");
        }
    }
    
    public async Task<string?> GetNoteContentAsync(Guid noteId)
    {
        var note = GetNoteById(noteId);
        if (note == null) return null;
        
        if (!note.IsContentLoaded)
        {
            await EnsureContentLoadedAsync(noteId).ConfigureAwait(false);
        }
        
        return note.Content;
    }
    
    public async Task SaveNoteContentAsync(Guid noteId, string content)
    {
        var note = GetNoteById(noteId);
        if (note == null) return;
        
        note.Content = content;
        note.IsContentLoaded = true;
        note.UpdateContentPreview();
        note.ModifiedDate = DateTime.UtcNow;
        
        if (_storageService?.IsLazyLoadingFormat == true)
        {
            await _storageService.SaveNoteContentAsync(noteId, content).ConfigureAwait(false);
        }
    }
    
    #endregion

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
