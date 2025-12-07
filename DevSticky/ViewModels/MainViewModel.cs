using System.Collections.ObjectModel;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;

namespace DevSticky.ViewModels;

/// <summary>
/// Application-level ViewModel managing all notes
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly IStorageService _storageService;
    private readonly IFormatterService _formatterService;
    private readonly ISearchService _searchService;
    private readonly IDebounceService _debounceService;
    private readonly IWindowService _windowService;
    private readonly CacheService _cacheService = new();

    public ObservableCollection<NoteViewModel> Notes { get; } = new();
    public ObservableCollection<NoteGroup> Groups { get; } = new();
    public ObservableCollection<NoteTag> Tags { get; } = new();
    public TrayViewModel TrayViewModel { get; }
    public AppSettings AppSettings { get; }
    public CacheService Cache => _cacheService;
    
    public Action? OnOpenDashboard { get; set; }
    public Action? OnOpenSettings { get; set; }

    public MainViewModel(
        INoteService noteService,
        IStorageService storageService,
        IFormatterService formatterService,
        ISearchService searchService,
        IDebounceService debounceService,
        IWindowService windowService)
    {
        _noteService = noteService;
        _storageService = storageService;
        _formatterService = formatterService;
        _searchService = searchService;
        _debounceService = debounceService;
        _windowService = windowService;
        
        AppSettings = AppSettings.Load();

        TrayViewModel = new TrayViewModel(
            onNewNote: CreateNewNote,
            onShowAll: ShowAllNotes,
            onHideAll: HideAllNotes,
            onSettings: OpenSettings,
            onExit: ExitApplication
        );
    }

    public async Task LoadNotesAsync()
    {
        var data = await _storageService.LoadAsync();
        
        // Load groups and tags
        foreach (var group in data.Groups)
            Groups.Add(group);
        foreach (var tag in data.Tags)
            Tags.Add(tag);
        
        // Load notes
        foreach (var note in data.Notes)
        {
            var vm = CreateNoteViewModel(note);
            Notes.Add(vm);
            _windowService.ShowNote(note);
        }

        if (Notes.Count == 0)
            CreateNewNote();
    }

    public void CreateNewNote()
    {
        var note = _noteService.CreateNote();
        var vm = CreateNoteViewModel(note);
        Notes.Add(vm);
        _windowService.ShowNote(note);
    }

    private NoteViewModel CreateNoteViewModel(Note note)
    {
        return new NoteViewModel(
            note,
            _noteService,
            _formatterService,
            _searchService,
            _debounceService,
            onClose: CloseNote,
            onSave: SaveAllNotes
        );
    }

    private void CloseNote(NoteViewModel vm)
    {
        RemoveNote(vm);
    }

    public void RemoveNote(NoteViewModel vm)
    {
        _noteService.DeleteNote(vm.Id);
        Notes.Remove(vm);
        _windowService.CloseNote(vm.Id);
        SaveAllNotes();
    }

    private void ShowAllNotes() => _windowService.ShowAllNotes();
    private void HideAllNotes() => _windowService.HideAllNotes();
    private void OpenSettings() => OnOpenSettings?.Invoke();
    public void OpenDashboard() => OnOpenDashboard?.Invoke();

    public void ShowNote(NoteViewModel vm)
    {
        var note = vm.ToNote();
        _windowService.ShowNote(note);
    }

    private void ExitApplication()
    {
        SaveAllNotes();
        System.Windows.Application.Current.Shutdown();
    }

    public void SaveAllNotes()
    {
        var notes = Notes.Select(vm => vm.ToNote()).ToList();
        var data = new AppData
        {
            AppSettings = AppSettings,
            Notes = notes,
            Groups = Groups.ToList(),
            Tags = Tags.ToList()
        };
        _ = _storageService.SaveAsync(data);
    }

    // Group management
    public NoteGroup CreateGroup(string? name = null)
    {
        var group = new NoteGroup { Name = name ?? L.Get("DefaultGroupName") };
        Groups.Add(group);
        SaveAllNotes();
        return group;
    }

    public void DeleteGroup(Guid groupId)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            // Move notes to ungrouped
            foreach (var note in Notes.Where(n => n.GroupId == groupId))
                note.GroupId = null;
            Groups.Remove(group);
            _cacheService.InvalidateGroupCache();
            SaveAllNotes();
        }
    }

    public void RenameGroup(Guid groupId, string newName)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            group.Name = newName.Length > 30 ? newName[..30] : newName;
            SaveAllNotes();
        }
    }

    // Tag management
    public NoteTag CreateTag(string? name = null, string? color = null)
    {
        var tag = new NoteTag 
        { 
            Name = name ?? L.Get("DefaultTagName"),
            Color = color ?? NoteTag.DefaultColors[Tags.Count % NoteTag.DefaultColors.Length]
        };
        Tags.Add(tag);
        SaveAllNotes();
        return tag;
    }

    public void DeleteTag(Guid tagId)
    {
        var tag = Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag != null)
        {
            // Remove tag from all notes
            foreach (var note in Notes)
                note.TagIds.Remove(tagId);
            Tags.Remove(tag);
            _cacheService.InvalidateTagCache();
            SaveAllNotes();
        }
    }

    public void RenameTag(Guid tagId, string newName)
    {
        var tag = Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag != null)
        {
            tag.Name = newName.Length > 20 ? newName[..20] : newName;
            SaveAllNotes();
        }
    }

    public void ChangeTagColor(Guid tagId, string newColor)
    {
        var tag = Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag != null)
        {
            tag.Color = newColor;
            SaveAllNotes();
        }
    }

    // Note-Tag operations
    public void AddTagToNote(NoteViewModel note, Guid tagId)
    {
        if (!note.TagIds.Contains(tagId) && note.TagIds.Count < 5)
        {
            note.TagIds.Add(tagId);
            SaveAllNotes();
        }
    }

    public void RemoveTagFromNote(NoteViewModel note, Guid tagId)
    {
        note.TagIds.Remove(tagId);
        SaveAllNotes();
    }

    // Note-Group operations
    public void MoveNoteToGroup(NoteViewModel note, Guid? groupId)
    {
        note.GroupId = groupId;
        SaveAllNotes();
    }
}
