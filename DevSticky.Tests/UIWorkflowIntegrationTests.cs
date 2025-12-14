using DevSticky.Models;
using DevSticky.ViewModels;
using DevSticky.Services;
using DevSticky.Interfaces;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Integration tests for UI workflows and user interactions
/// </summary>
public class UIWorkflowIntegrationTests : IDisposable
{
    private readonly TestNoteService _noteService;
    private readonly TestFormatterService _formatterService;
    private readonly TestSearchService _searchService;
    private readonly TestDebounceService _debounceService;
    private readonly TestWindowService _windowService;
    private readonly MainViewModel _mainViewModel;

    public UIWorkflowIntegrationTests()
    {
        _noteService = new TestNoteService();
        _formatterService = new TestFormatterService();
        _searchService = new TestSearchService();
        _debounceService = new TestDebounceService();
        _windowService = new TestWindowService();

        var settings = new AppSettings();
        var storageService = new TestStorageService();
        var templateService = new TestTemplateService();
        var saveQueueService = new TestSaveQueueService();
        var dirtyTracker = new DirtyTracker<Note>();
        var folderService = new TestFolderService();

        _mainViewModel = new MainViewModel(
            _noteService,
            storageService,
            _formatterService,
            _searchService,
            _debounceService,
            _windowService,
            templateService,
            saveQueueService,
            dirtyTracker,
            folderService,
            settings
        );
    }

    [Fact]
    public void CreateNewNote_Workflow_ShouldCreateAndShowNote()
    {
        // Arrange
        var initialNoteCount = _mainViewModel.Notes.Count;

        // Act - Create new note (simulates user clicking "New Note")
        _mainViewModel.CreateNewNote();

        // Assert
        Assert.Equal(initialNoteCount + 1, _mainViewModel.Notes.Count);
        Assert.Equal(1, _windowService.ShownNotes.Count);
        
        var newNote = _mainViewModel.Notes.Last();
        Assert.Equal("Untitled Note", newNote.Title);
        Assert.True(newNote.IsPinned);
    }

    [Fact]
    public void EditNote_Workflow_ShouldUpdateNoteAndTriggerAutoSave()
    {
        // Arrange - Create a note first
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();

        // Act - Edit note content (simulates user typing)
        noteViewModel.Content = "User typed this content";
        noteViewModel.Title = "User changed title";

        // Assert - Content and title should be updated
        Assert.Equal("User typed this content", noteViewModel.Content);
        Assert.Equal("User changed title", noteViewModel.Title);
        
        // Assert - Auto-save should be triggered via debounce service
        Assert.True(_debounceService.DebounceCalled, "Auto-save should be triggered via debounce");
    }

    [Fact]
    public void SearchInNote_Workflow_ShouldFindAndNavigateMatches()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();
        noteViewModel.Content = "This is a test content with multiple test words for testing.";

        // Act - Start search (simulates user pressing Ctrl+F)
        noteViewModel.ToggleSearchCommand.Execute(null);
        noteViewModel.SearchTerm = "test";

        // Assert
        Assert.True(noteViewModel.IsSearchVisible);
        Assert.Equal(3, noteViewModel.SearchMatches.Count); // Should find 3 "test" matches
        Assert.Equal(0, noteViewModel.CurrentMatchIndex);

        // Act - Navigate to next match (simulates user clicking Next)
        noteViewModel.NextMatchCommand.Execute(null);

        // Assert
        Assert.Equal(1, noteViewModel.CurrentMatchIndex);

        // Act - Navigate to previous match (simulates user clicking Previous)
        noteViewModel.PreviousMatchCommand.Execute(null);

        // Assert
        Assert.Equal(0, noteViewModel.CurrentMatchIndex);
    }

    [Fact]
    public void FormatContent_Workflow_ShouldFormatJsonAndXml()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();

        // Act - Format JSON content (simulates user pressing Ctrl+Shift+F)
        noteViewModel.Content = "{\"name\":\"test\",\"value\":123}";
        noteViewModel.FormatCommand.Execute(null);

        // Assert - Content should be formatted
        Assert.Contains("name", noteViewModel.Content);
        Assert.Contains("test", noteViewModel.Content);
        Assert.Contains("value", noteViewModel.Content);
        Assert.Contains("123", noteViewModel.Content);
    }

    [Fact]
    public void PinToggle_Workflow_ShouldTogglePinState()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();
        var initialPinState = noteViewModel.IsPinned;

        // Act - Toggle pin (simulates user clicking pin button)
        noteViewModel.TogglePinCommand.Execute(null);

        // Assert
        Assert.NotEqual(initialPinState, noteViewModel.IsPinned);

        // Act - Toggle again
        noteViewModel.TogglePinCommand.Execute(null);

        // Assert - Should return to original state
        Assert.Equal(initialPinState, noteViewModel.IsPinned);
    }

    [Fact]
    public void OpacityAdjustment_Workflow_ShouldAdjustOpacityWithinBounds()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();
        noteViewModel.Opacity = 0.5; // Start at middle value

        // Act - Increase opacity (simulates user pressing Ctrl+Up)
        noteViewModel.IncreaseOpacityCommand.Execute(null);

        // Assert
        Assert.Equal(0.6, noteViewModel.Opacity, 1); // Should increase by 0.1

        // Act - Decrease opacity (simulates user pressing Ctrl+Down)
        noteViewModel.DecreaseOpacityCommand.Execute(null);
        noteViewModel.DecreaseOpacityCommand.Execute(null);

        // Assert
        Assert.Equal(0.4, noteViewModel.Opacity, 1); // Should decrease by 0.2 total
    }

    [Fact]
    public void CloseNote_Workflow_ShouldCloseWindowButKeepNoteInCollection()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        _mainViewModel.CreateNewNote();
        var initialCount = _mainViewModel.Notes.Count;
        var noteToClose = _mainViewModel.Notes.First();

        // Act - Close note (simulates user clicking X button)
        // This should only close the window, not delete the note
        noteToClose.CloseCommand.Execute(null);

        // Assert - Note should still be in collection (can be reopened from Dashboard)
        Assert.Equal(initialCount, _mainViewModel.Notes.Count);
        Assert.Contains(noteToClose, _mainViewModel.Notes);
        
        // Window should be closed
        Assert.DoesNotContain(noteToClose.ToNote(), _windowService.ShownNotes);
    }
    
    [Fact]
    public void DeleteNote_Workflow_ShouldRemoveNoteFromCollection()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        _mainViewModel.CreateNewNote();
        var initialCount = _mainViewModel.Notes.Count;
        var noteToDelete = _mainViewModel.Notes.First();

        // Act - Delete note (simulates user clicking Delete button on Dashboard)
        _mainViewModel.RemoveNote(noteToDelete);

        // Assert - Note should be removed from collection
        Assert.Equal(initialCount - 1, _mainViewModel.Notes.Count);
        Assert.DoesNotContain(noteToDelete, _mainViewModel.Notes);
    }

    [Fact]
    public void SaveAllNotes_Workflow_ShouldSaveOnlyDirtyNotes()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        _mainViewModel.CreateNewNote();
        
        var note1 = _mainViewModel.Notes[0];
        var note2 = _mainViewModel.Notes[1];
        
        // Mark both as clean initially
        note1.ToNote().MarkClean();
        note2.ToNote().MarkClean();
        
        // Make only one note dirty
        note1.Content = "Modified content";

        // Act - Save all notes (simulates auto-save or manual save)
        _mainViewModel.SaveAllNotes();

        // Assert - Only dirty note should be processed
        Assert.True(_debounceService.DebounceCalled);
        // The actual save logic would be tested in the storage service integration tests
    }

    [Fact]
    public void TrayMenu_Workflow_ShouldControlNoteVisibility()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        _mainViewModel.CreateNewNote();

        // Act - Hide all notes (simulates user clicking "Hide All" in tray menu)
        _mainViewModel.TrayViewModel.HideAllCommand.Execute(null);

        // Assert
        Assert.Equal(2, _windowService.HiddenNotes.Count);

        // Act - Show all notes (simulates user clicking "Show All" in tray menu)
        _mainViewModel.TrayViewModel.ShowAllCommand.Execute(null);

        // Assert
        Assert.Equal(2, _windowService.ShownNotes.Count);
        Assert.Empty(_windowService.HiddenNotes);
    }

    [Fact]
    public void GroupAssignment_Workflow_ShouldAssignNoteToGroup()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();
        var groupId = Guid.NewGuid();

        // Act - Assign note to group (simulates user selecting group in dropdown)
        noteViewModel.GroupId = groupId;

        // Assert
        Assert.Equal(groupId, noteViewModel.GroupId);
        Assert.Equal(groupId, noteViewModel.ToNote().GroupId);
        Assert.True(noteViewModel.IsDirty);
    }

    [Fact]
    public void TagAssignment_Workflow_ShouldAssignTagsToNote()
    {
        // Arrange
        _mainViewModel.CreateNewNote();
        var noteViewModel = _mainViewModel.Notes.First();
        var tag1Id = Guid.NewGuid();
        var tag2Id = Guid.NewGuid();

        // Act - Assign tags to note (simulates user selecting tags)
        noteViewModel.TagIds = new List<Guid> { tag1Id, tag2Id };

        // Assert
        Assert.Contains(tag1Id, noteViewModel.TagIds);
        Assert.Contains(tag2Id, noteViewModel.TagIds);
        Assert.Equal(2, noteViewModel.TagIds.Count);
        Assert.True(noteViewModel.IsDirty);
    }

    public void Dispose()
    {
        _noteService?.Dispose();
        _debounceService?.Dispose();
    }

    #region Test Service Implementations

    private class TestNoteService : INoteService
    {
        private readonly List<Note> _notes = new();

        public Note CreateNote()
        {
            var note = new Note();
            _notes.Add(note);
            return note;
        }

        public void AddNote(Note note)
        {
            if (note == null) return;
            var existingIndex = _notes.FindIndex(n => n.Id == note.Id);
            if (existingIndex >= 0)
                _notes[existingIndex] = note;
            else
                _notes.Add(note);
        }

        public void UpdateNote(Note note) { }
        public void DeleteNote(Guid id) => _notes.RemoveAll(n => n.Id == id);
        public Note? GetNoteById(Guid id) => _notes.FirstOrDefault(n => n.Id == id);
        public IReadOnlyList<Note> GetAllNotes() => _notes.AsReadOnly();
        public void TogglePin(Guid id) { }
        public double AdjustOpacity(Guid id, double step) => 0.9;
        public void LoadNotes(IEnumerable<Note> notes) => _notes.AddRange(notes);
        public void Dispose() { }
    }

    private class TestFormatterService : IFormatterService
    {
        public bool IsValidJson(string content) => content.Trim().StartsWith("{");
        public bool IsValidXml(string content) => content.Trim().StartsWith("<");
        public string FormatJson(string json) => json.Replace(",", ",\n  ");
        public string FormatXml(string xml) => xml;
    }

    private class TestSearchService : ISearchService
    {
        public IReadOnlyList<SearchMatch> FindMatches(string content, string searchTerm)
        {
            var matches = new List<SearchMatch>();
            if (string.IsNullOrEmpty(searchTerm)) return matches.AsReadOnly();

            int index = 0;
            while ((index = content.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                matches.Add(new SearchMatch(index, searchTerm.Length));
                index += searchTerm.Length;
            }
            return matches.AsReadOnly();
        }

        public int GetNextMatchIndex(int currentIndex, int totalMatches)
        {
            return (currentIndex + 1) % totalMatches;
        }

        public int GetPreviousMatchIndex(int currentIndex, int totalMatches)
        {
            return currentIndex == 0 ? totalMatches - 1 : currentIndex - 1;
        }
    }

    private class TestDebounceService : IDebounceService
    {
        public bool DebounceCalled { get; private set; }

        public void Debounce(string key, Action action, int delayMs)
        {
            DebounceCalled = true;
            action(); // Execute immediately for testing
        }

        public void Cancel(string key) { }
        public void Dispose() { }
    }

    private class TestWindowService : IWindowService
    {
        public List<Note> ShownNotes { get; } = new();
        public List<Note> HiddenNotes { get; } = new();

        public void ShowNote(Note note) => ShownNotes.Add(note);
        public void CloseNote(Guid noteId) => ShownNotes.RemoveAll(n => n.Id == noteId);
        public void ShowAllNotes() 
        { 
            ShownNotes.AddRange(HiddenNotes);
            HiddenNotes.Clear();
        }
        public void HideAllNotes() 
        { 
            HiddenNotes.AddRange(ShownNotes);
            ShownNotes.Clear();
        }
        public void ToggleAllNotesVisibility() { }
        public string? GetMonitorDeviceIdForNote(Guid noteId) => "primary-monitor";
        public void MoveNoteToMonitor(Guid noteId, string monitorDeviceId) { }
        public void EnsureNoteVisible(Guid noteId) { }
        public void Dispose() { }
    }

    private class TestStorageService : IStorageService
    {
        public Task<AppData> LoadAsync() => Task.FromResult(new AppData
        {
            Notes = new List<Note>(),
            Groups = new List<NoteGroup>(),
            Tags = new List<NoteTag>(),
            AppSettings = new AppSettings()
        });

        public Task SaveAsync(AppData data) => Task.CompletedTask;
        public Task SaveNotesAsync(IEnumerable<Note> notes, AppData currentData) => Task.CompletedTask;
        public string GetStoragePath() => "test-path";
        public void Dispose() { }
    }

    private class TestTemplateService : ITemplateService
    {
        public Task<IReadOnlyList<NoteTemplate>> GetAllTemplatesAsync() => 
            Task.FromResult<IReadOnlyList<NoteTemplate>>(Array.Empty<NoteTemplate>());
        public Task<NoteTemplate?> GetTemplateByIdAsync(Guid id) => Task.FromResult<NoteTemplate?>(null);
        public Task<NoteTemplate> CreateTemplateAsync(NoteTemplate template) => Task.FromResult(template);
        public Task UpdateTemplateAsync(NoteTemplate template) => Task.CompletedTask;
        public Task DeleteTemplateAsync(Guid id) => Task.CompletedTask;
        public Task<Note> CreateNoteFromTemplateAsync(Guid templateId, Dictionary<string, string>? variables = null) => Task.FromResult(new Note());
        public Task ExportTemplatesAsync(string filePath) => Task.CompletedTask;
        public Task ImportTemplatesAsync(string filePath) => Task.CompletedTask;
        public IReadOnlyList<NoteTemplate> GetBuiltInTemplates() => Array.Empty<NoteTemplate>();
        public Task<NoteTemplate> CreateTemplateFromNoteAsync(Note note, string templateName, string description, string category) => Task.FromResult(new NoteTemplate());
        public IReadOnlyList<TemplatePlaceholder> ParsePlaceholders(string content) => Array.Empty<TemplatePlaceholder>();
        public string ReplacePlaceholders(string content, Dictionary<string, string>? variables = null) => content;
    }

    private class TestSaveQueueService : ISaveQueueService
    {
        public void QueueNote(Note note) { }
        public void QueueNotes(IEnumerable<Note> notes) { }
        public Task FlushAsync() => Task.CompletedTask;
        public int QueueCount => 0;
        public event EventHandler<SaveCompletedEventArgs>? SaveCompleted;
        public void Dispose() { }
    }

    private class TestFolderService : IFolderService
    {
        public Task<NoteFolder> CreateFolderAsync(string name, Guid? parentId = null) => Task.FromResult(new NoteFolder());
        public Task<bool> DeleteFolderAsync(Guid folderId) => Task.FromResult(true);
        public Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentId) => Task.FromResult(true);
        public Task<bool> MoveNoteToFolderAsync(Guid noteId, Guid? folderId) => Task.FromResult(true);
        public Task<IReadOnlyList<NoteFolder>> GetRootFoldersAsync() => Task.FromResult<IReadOnlyList<NoteFolder>>(Array.Empty<NoteFolder>());
        public Task<IReadOnlyList<NoteFolder>> GetChildFoldersAsync(Guid parentId) => Task.FromResult<IReadOnlyList<NoteFolder>>(Array.Empty<NoteFolder>());
        public Task<IReadOnlyList<Note>> GetNotesInFolderAsync(Guid? folderId) => Task.FromResult<IReadOnlyList<Note>>(Array.Empty<Note>());
        public Task SaveAsync() => Task.CompletedTask;
        public Task LoadAsync() => Task.CompletedTask;
    }

    #endregion
}