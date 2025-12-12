using System.IO;
using System.Text.Json;
using DevSticky.Helpers;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service for tracking recently accessed notes
/// </summary>
public class RecentNotesService : IRecentNotesService
{
    private readonly string _historyPath;
    private readonly INoteService _noteService;
    private readonly List<RecentNoteEntry> _recentNotes = new();
    private readonly object _lock = new();
    
    private int _maxRecentNotes = 10;

    public event EventHandler? RecentNotesChanged;

    public int MaxRecentNotes
    {
        get => _maxRecentNotes;
        set
        {
            _maxRecentNotes = Math.Max(1, Math.Min(50, value));
            TrimHistory();
        }
    }

    public RecentNotesService(INoteService noteService)
    {
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        
        _historyPath = PathHelper.Combine(
            PathHelper.GetAppDataPath(AppConstants.AppDataFolderName),
            "recent_notes.json");
        
        LoadHistory();
    }

    public void RecordAccess(Guid noteId)
    {
        lock (_lock)
        {
            // Remove existing entry if present
            _recentNotes.RemoveAll(r => r.NoteId == noteId);
            
            // Add to front
            _recentNotes.Insert(0, new RecentNoteEntry
            {
                NoteId = noteId,
                LastAccessedAt = DateTime.UtcNow
            });
            
            TrimHistory();
            SaveHistory();
        }
        
        RecentNotesChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<Guid> GetRecentNoteIds()
    {
        lock (_lock)
        {
            return _recentNotes.Select(r => r.NoteId).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<RecentNoteInfo> GetRecentNotes()
    {
        lock (_lock)
        {
            var result = new List<RecentNoteInfo>();
            var allNotes = _noteService.GetAllNotes();
            
            foreach (var entry in _recentNotes)
            {
                var note = allNotes.FirstOrDefault(n => n.Id == entry.NoteId);
                if (note != null)
                {
                    var content = note.Content ?? string.Empty;
                    result.Add(new RecentNoteInfo
                    {
                        NoteId = entry.NoteId,
                        Title = note.Title,
                        LastAccessedAt = entry.LastAccessedAt,
                        ContentPreview = content.Length > 50 ? content[..50] + "..." : content
                    });
                }
            }
            
            return result.AsReadOnly();
        }
    }

    public void RemoveFromHistory(Guid noteId)
    {
        lock (_lock)
        {
            _recentNotes.RemoveAll(r => r.NoteId == noteId);
            SaveHistory();
        }
        
        RecentNotesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _recentNotes.Clear();
            SaveHistory();
        }
        
        RecentNotesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrimHistory()
    {
        while (_recentNotes.Count > _maxRecentNotes)
        {
            _recentNotes.RemoveAt(_recentNotes.Count - 1);
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                var entries = JsonSerializer.Deserialize<List<RecentNoteEntry>>(json);
                if (entries != null)
                {
                    _recentNotes.Clear();
                    _recentNotes.AddRange(entries);
                    TrimHistory();
                }
            }
        }
        catch
        {
            // Ignore load errors, start with empty history
        }
    }

    private void SaveHistory()
    {
        try
        {
            var directory = PathHelper.GetDirectoryName(_historyPath);
            if (!StringHelper.IsNullOrEmpty(directory))
            {
                PathHelper.EnsureDirectoryExists(directory);
            }
            
            var json = JsonSerializer.Serialize(_recentNotes);
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private class RecentNoteEntry
    {
        public Guid NoteId { get; set; }
        public DateTime LastAccessedAt { get; set; }
    }
}
