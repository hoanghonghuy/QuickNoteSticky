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
    private readonly ITemplateService _templateService;
    private readonly IGroupManagementService _groupManagementService;
    private readonly ITagManagementService _tagManagementService;
    private readonly ISaveQueueService _saveQueueService;
    private readonly IFolderService _folderService;
    private readonly CacheService _cacheService = new();
    private readonly IDirtyTracker<Note> _dirtyTracker;
    private AppData? _currentAppData;

    public ObservableCollection<NoteViewModel> Notes { get; } = new();
    public ObservableCollection<NoteGroup> Groups { get; } = new();
    public ObservableCollection<NoteTag> Tags { get; } = new();
    public TrayViewModel TrayViewModel { get; }
    public AppSettings AppSettings { get; }
    public CacheService Cache => _cacheService;
    
    public Action? OnOpenDashboard { get; set; }
    public Action? OnOpenSettings { get; set; }
    
    /// <summary>
    /// Callback for showing template selection dialog (Requirements 6.1)
    /// Returns the selected template or null for blank note
    /// </summary>
    public Func<NoteTemplate?>? OnShowTemplateSelection { get; set; }

    public MainViewModel(
        INoteService noteService,
        IStorageService storageService,
        IFormatterService formatterService,
        ISearchService searchService,
        IDebounceService debounceService,
        IWindowService windowService,
        ITemplateService templateService,
        ISaveQueueService saveQueueService,
        IDirtyTracker<Note> dirtyTracker,
        IFolderService folderService,
        AppSettings appSettings)
    {
        _noteService = noteService;
        _storageService = storageService;
        _formatterService = formatterService;
        _searchService = searchService;
        _debounceService = debounceService;
        _windowService = windowService;
        _templateService = templateService;
        _saveQueueService = saveQueueService;
        _dirtyTracker = dirtyTracker;
        _folderService = folderService;
        
        AppSettings = appSettings;

        // Initialize GroupManagementService after collections are created (Requirements 1.1, 8.3)
        _groupManagementService = new GroupManagementService(
            Groups,
            Notes,
            _cacheService,
            SaveNotesIncremental);

        // Initialize TagManagementService after collections are created (Requirements 1.1, 8.3)
        _tagManagementService = new TagManagementService(
            Tags,
            Notes,
            _cacheService,
            SaveNotesIncremental);

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
        _currentAppData = data;
        
        // Set current app data for incremental saves
        if (_saveQueueService is SaveQueueService saveQueue)
        {
            saveQueue.SetCurrentAppData(data);
        }
        
        // Load folders
        await _folderService.LoadAsync();
        
        // Load groups and tags
        foreach (var group in data.Groups)
            Groups.Add(group);
        foreach (var tag in data.Tags)
            Tags.Add(tag);
        
        // Load notes into NoteService first
        _noteService.LoadNotes(data.Notes);
        
        // Then create ViewModels (don't show them automatically - user opens from Dashboard)
        foreach (var note in data.Notes)
        {
            var vm = CreateNoteViewModel(note);
            Notes.Add(vm);
        }

        // Open Dashboard on startup instead of showing notes
        OnOpenDashboard?.Invoke();
    }

    /// <summary>
    /// Create a new note, optionally showing template selection dialog (Requirements 6.1, 6.2)
    /// </summary>
    public void CreateNewNote()
    {
        // Show template selection dialog if callback is set
        if (OnShowTemplateSelection != null)
        {
            var template = OnShowTemplateSelection();
            if (template != null)
            {
                // Create note from template - use async void wrapper for fire-and-forget with exception handling
                _ = CreateNoteFromTemplateWithErrorHandlingAsync(template);
                return;
            }
            // If template is null but dialog was shown, user chose blank note or cancelled
            // For blank note, continue with default creation
        }
        
        // Create blank note
        var note = _noteService.CreateNote();
        var vm = CreateNoteViewModel(note);
        Notes.Add(vm);
        _windowService.ShowNote(note);
        
        // Force immediate save
        _ = SaveAllNotesImmediateAsync();
        
        // Record access for recent notes
        RecordNoteAccess(note.Id);
    }

    /// <summary>
    /// Wrapper for CreateNoteFromTemplateAsync with proper error handling for fire-and-forget calls
    /// </summary>
    private async Task CreateNoteFromTemplateWithErrorHandlingAsync(NoteTemplate template)
    {
        try
        {
            await CreateNoteFromTemplateAsync(template);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateNoteFromTemplate failed: {ex.Message}");
            // Fallback is already handled in CreateNoteFromTemplateAsync
        }
    }

    /// <summary>
    /// Record note access for recent notes tracking
    /// </summary>
    private void RecordNoteAccess(Guid noteId)
    {
        try
        {
            var recentNotesService = App.GetService<IRecentNotesService>();
            recentNotesService?.RecordAccess(noteId);
        }
        catch { /* Ignore if service not available */ }
    }

    /// <summary>
    /// Create a new note from a template (Requirements 6.2)
    /// </summary>
    public async Task CreateNoteFromTemplateAsync(NoteTemplate template)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CreateNoteFromTemplate] Creating note from template: {template.Name}");
            var note = await _templateService.CreateNoteFromTemplateAsync(template.Id);
            System.Diagnostics.Debug.WriteLine($"[CreateNoteFromTemplate] Note created: {note.Title}, Content length: {note.Content?.Length ?? 0}");
            
            // Add note to NoteService so it's tracked
            _noteService.AddNote(note);
            
            // Add to notes collection
            var vm = CreateNoteViewModel(note);
            Notes.Add(vm);
            _windowService.ShowNote(note);
            
            // Force immediate save to persist the new note
            await SaveAllNotesImmediateAsync();
            
            RecordNoteAccess(note.Id);
            System.Diagnostics.Debug.WriteLine($"[CreateNoteFromTemplate] Note saved and shown successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateNoteFromTemplate] Failed: {ex.Message}");
            // Fall back to blank note
            var note = _noteService.CreateNote();
            var vm = CreateNoteViewModel(note);
            Notes.Add(vm);
            _windowService.ShowNote(note);
            await SaveAllNotesImmediateAsync();
        }
    }

    /// <summary>
    /// Create a blank note without showing template dialog
    /// </summary>
    public void CreateBlankNote()
    {
        var note = _noteService.CreateNote();
        var vm = CreateNoteViewModel(note);
        Notes.Add(vm);
        _windowService.ShowNote(note);
        
        // Force immediate save
        _ = SaveAllNotesImmediateAsync();
        
        RecordNoteAccess(note.Id);
    }

    private NoteViewModel CreateNoteViewModel(Note note)
    {
        // Track the note for dirty tracking
        _dirtyTracker.Track(note);
        
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

    /// <summary>
    /// Closes the note window without deleting the note.
    /// The note remains in the collection and can be reopened from Dashboard.
    /// </summary>
    private void CloseNote(NoteViewModel vm)
    {
        // Save the note before closing to ensure content is persisted
        SaveAllNotes();
        
        // Just close the window, don't remove from collection or delete
        _windowService.CloseNote(vm.Id);
    }

    /// <summary>
    /// Permanently deletes a note from the collection and storage.
    /// Use this when user explicitly wants to delete a note.
    /// </summary>
    public void RemoveNote(NoteViewModel vm)
    {
        _noteService.DeleteNote(vm.Id);
        Notes.Remove(vm);
        _windowService.CloseNote(vm.Id);
        
        // Stop tracking the removed note
        // Note: We don't need to explicitly remove from DirtyTracker as it will be cleaned up
        // when the note reference is no longer held
        
        SaveAllNotes();
    }

    private void ShowAllNotes() => _windowService.ShowAllNotes();
    private void HideAllNotes()
    {
        // Save all notes before hiding to ensure content is persisted
        SaveAllNotes();
        _windowService.HideAllNotes();
    }
    private void OpenSettings() => OnOpenSettings?.Invoke();
    public void OpenDashboard() => OnOpenDashboard?.Invoke();

    public void ShowNote(NoteViewModel vm)
    {
        var note = vm.ToNote();
        _windowService.ShowNote(note);
    }

    /// <summary>
    /// Open a note by its ID (for internal note link navigation - Requirements 4.7)
    /// </summary>
    public void OpenNoteById(Guid noteId)
    {
        // Record access for recent notes
        try
        {
            var recentNotesService = App.GetService<IRecentNotesService>();
            recentNotesService?.RecordAccess(noteId);
        }
        catch { /* Ignore if service not available */ }
        
        // Optimized: Single pass search through notes
        foreach (var vm in Notes)
        {
            if (vm.Id == noteId)
            {
                // Show and focus the existing note window
                var note = vm.ToNote();
                _windowService.ShowNote(note);
                return;
            }
        }

        // Try to load the note from storage
        var noteFromService = _noteService.GetNoteById(noteId);
        if (noteFromService != null)
        {
            var vm = CreateNoteViewModel(noteFromService);
            Notes.Add(vm);
            _windowService.ShowNote(noteFromService);
        }
    }

    private async void ExitApplication()
    {
        // Wait for save to complete before shutting down
        await SaveAllNotesImmediateAsync();
        System.Windows.Application.Current.Shutdown();
    }

    public void SaveAllNotes()
    {
        // Fire and forget save - use for auto-save scenarios
        _ = SaveAllNotesImmediateAsync();
    }
    
    /// <summary>
    /// Saves all notes immediately and waits for completion
    /// </summary>
    public async Task SaveAllNotesImmediateAsync()
    {
        // Always do a full save to ensure data integrity
        var allNotes = new List<Note>(Notes.Count);
        
        foreach (var vm in Notes)
        {
            var note = vm.ToNote();
            allNotes.Add(note);
            // Mark note as clean after adding to save list
            note.MarkClean();
        }
        
        System.Diagnostics.Debug.WriteLine($"[SaveAllNotes] Saving {allNotes.Count} notes");
        
        var data = new AppData
        {
            AppSettings = AppSettings,
            Notes = allNotes,
            Groups = new List<NoteGroup>(Groups),
            Tags = new List<NoteTag>(Tags)
        };
        
        try
        {
            await _storageService.SaveAsync(data);
            System.Diagnostics.Debug.WriteLine($"[SaveAllNotes] Completed: {allNotes.Count} notes saved");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveAllNotes] Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves only dirty notes incrementally using the save queue service
    /// </summary>
    public void SaveNotesIncremental()
    {
        var dirtyNotes = _dirtyTracker.GetDirtyItems().ToList();
        if (dirtyNotes.Count == 0)
            return;

        // Convert dirty ViewModels to Notes
        var notesToSave = new List<Note>();
        foreach (var dirtyNote in dirtyNotes)
        {
            // Find the corresponding ViewModel and convert to Note
            var noteVm = Notes.FirstOrDefault(vm => vm.Id == dirtyNote.Id);
            if (noteVm != null)
            {
                notesToSave.Add(noteVm.ToNote());
                _dirtyTracker.MarkClean(dirtyNote);
            }
        }

        if (notesToSave.Count > 0)
        {
            _saveQueueService.QueueNotes(notesToSave);
        }
    }

    /// <summary>
    /// Async version of SaveNotesIncremental for partial saves (Requirements 5.3)
    /// </summary>
    public async Task SaveNotesAsync()
    {
        var dirtyNotes = _dirtyTracker.GetDirtyItems().ToList();
        if (dirtyNotes.Count == 0)
            return;

        // Convert dirty ViewModels to Notes
        var notesToSave = new List<Note>();
        foreach (var dirtyNote in dirtyNotes)
        {
            // Find the corresponding ViewModel and convert to Note
            var noteVm = Notes.FirstOrDefault(vm => vm.Id == dirtyNote.Id);
            if (noteVm != null)
            {
                notesToSave.Add(noteVm.ToNote());
                _dirtyTracker.MarkClean(dirtyNote);
            }
        }

        if (notesToSave.Count > 0)
        {
            _saveQueueService.QueueNotes(notesToSave);
            await _saveQueueService.FlushAsync();
        }
    }

    /// <summary>
    /// Saves specific notes by their IDs using incremental save (Requirements 5.3)
    /// </summary>
    public async Task SaveNotesAsync(IEnumerable<Guid> noteIds)
    {
        var notesToSave = new List<Note>();
        
        foreach (var noteId in noteIds)
        {
            var noteVm = Notes.FirstOrDefault(vm => vm.Id == noteId);
            if (noteVm != null)
            {
                var note = noteVm.ToNote();
                notesToSave.Add(note);
                _dirtyTracker.MarkClean(note);
            }
        }

        if (notesToSave.Count > 0)
        {
            _saveQueueService.QueueNotes(notesToSave);
            await _saveQueueService.FlushAsync();
        }
    }

    // Group management - delegated to GroupManagementService (Requirements 1.1, 8.3)
    public NoteGroup CreateGroup(string? name = null) => _groupManagementService.CreateGroup(name);
    public void DeleteGroup(Guid groupId) => _groupManagementService.DeleteGroup(groupId);
    public void RenameGroup(Guid groupId, string newName) => _groupManagementService.RenameGroup(groupId, newName);

    // Tag management - delegated to TagManagementService (Requirements 1.1, 8.3)
    public ITagManagementService TagManagementService => _tagManagementService;
    public NoteTag CreateTag(string? name = null, string? color = null) => _tagManagementService.CreateTag(name, color);
    public void DeleteTag(Guid tagId) => _tagManagementService.DeleteTag(tagId);
    public void RenameTag(Guid tagId, string newName) => _tagManagementService.RenameTag(tagId, newName);
    public void ChangeTagColor(Guid tagId, string newColor) => _tagManagementService.ChangeTagColor(tagId, newColor);

    // Note-Tag operations - delegated to TagManagementService (Requirements 1.1, 8.3)
    public void AddTagToNote(NoteViewModel note, Guid tagId) => _tagManagementService.AddTagToNote(note.Id, tagId);
    public void RemoveTagFromNote(NoteViewModel note, Guid tagId) => _tagManagementService.RemoveTagFromNote(note.Id, tagId);

    // Note-Group operations - delegated to GroupManagementService (Requirements 1.1, 8.3)
    public void MoveNoteToGroup(NoteViewModel note, Guid? groupId) => _groupManagementService.MoveNoteToGroup(note.Id, groupId);
}
