using System.Collections.ObjectModel;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.ViewModels;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for GroupManagementService
/// </summary>
public class GroupManagementServiceTests
{
    private readonly ObservableCollection<NoteGroup> _groups;
    private readonly ObservableCollection<NoteViewModel> _notes;
    private readonly CacheService _cacheService;
    private readonly GroupManagementService _service;
    private readonly MockNoteService _mockNoteService;
    private readonly MockFormatterService _mockFormatterService;
    private readonly MockSearchService _mockSearchService;
    private readonly MockDebounceService _mockDebounceService;
    private bool _saveCallbackCalled;

    public GroupManagementServiceTests()
    {
        _groups = new ObservableCollection<NoteGroup>();
        _notes = new ObservableCollection<NoteViewModel>();
        _cacheService = new CacheService();
        _mockNoteService = new MockNoteService();
        _mockFormatterService = new MockFormatterService();
        _mockSearchService = new MockSearchService();
        _mockDebounceService = new MockDebounceService();
        _saveCallbackCalled = false;
        
        _service = new GroupManagementService(
            _groups,
            _notes,
            _cacheService,
            () => _saveCallbackCalled = true);
    }

    private NoteViewModel CreateNoteViewModel()
    {
        return new NoteViewModel(
            new Note(),
            _mockNoteService,
            _mockFormatterService,
            _mockSearchService,
            _mockDebounceService);
    }

    [Fact]
    public void Constructor_WithNullGroups_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GroupManagementService(
            null!,
            _notes,
            _cacheService,
            () => { }));
    }

    [Fact]
    public void Constructor_WithNullNotes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GroupManagementService(
            _groups,
            null!,
            _cacheService,
            () => { }));
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GroupManagementService(
            _groups,
            _notes,
            null!,
            () => { }));
    }

    [Fact]
    public void Constructor_WithNullSaveCallback_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GroupManagementService(
            _groups,
            _notes,
            _cacheService,
            null!));
    }

    [Fact]
    public void CreateGroup_WithoutName_CreatesGroupWithDefaultName()
    {
        // Act
        var group = _service.CreateGroup();

        // Assert
        Assert.Single(_groups);
        Assert.NotNull(group);
        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.NotEmpty(group.Name);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void CreateGroup_WithName_CreatesGroupWithSpecifiedName()
    {
        // Arrange
        var groupName = "Test Group";

        // Act
        var group = _service.CreateGroup(groupName);

        // Assert
        Assert.Single(_groups);
        Assert.Equal(groupName, group.Name);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void DeleteGroup_ExistingGroup_RemovesGroupAndMovesNotesToUngrouped()
    {
        // Arrange
        var group = new NoteGroup { Name = "Test Group" };
        _groups.Add(group);
        
        var note1 = CreateNoteViewModel();
        note1.GroupId = group.Id;
        var note2 = CreateNoteViewModel();
        note2.GroupId = group.Id;
        var note3 = CreateNoteViewModel();
        note3.GroupId = null;
        
        _notes.Add(note1);
        _notes.Add(note2);
        _notes.Add(note3);
        
        _saveCallbackCalled = false;

        // Act
        _service.DeleteGroup(group.Id);

        // Assert
        Assert.Empty(_groups);
        Assert.Null(note1.GroupId);
        Assert.Null(note2.GroupId);
        Assert.Null(note3.GroupId); // Should remain null
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void DeleteGroup_NonExistentGroup_DoesNothing()
    {
        // Arrange
        var existingGroup = new NoteGroup { Name = "Existing Group" };
        _groups.Add(existingGroup);
        _saveCallbackCalled = false;

        // Act
        _service.DeleteGroup(Guid.NewGuid());

        // Assert
        Assert.Single(_groups);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void RenameGroup_ExistingGroup_UpdatesName()
    {
        // Arrange
        var group = new NoteGroup { Name = "Old Name" };
        _groups.Add(group);
        var newName = "New Name";
        _saveCallbackCalled = false;

        // Act
        _service.RenameGroup(group.Id, newName);

        // Assert
        Assert.Equal(newName, group.Name);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void RenameGroup_NonExistentGroup_DoesNothing()
    {
        // Arrange
        var group = new NoteGroup { Name = "Original Name" };
        _groups.Add(group);
        _saveCallbackCalled = false;

        // Act
        _service.RenameGroup(Guid.NewGuid(), "New Name");

        // Assert
        Assert.Equal("Original Name", group.Name);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void RenameGroup_LongName_TruncatesTo30Characters()
    {
        // Arrange
        var group = new NoteGroup { Name = "Short Name" };
        _groups.Add(group);
        var longName = new string('A', 50); // 50 characters
        _saveCallbackCalled = false;

        // Act
        _service.RenameGroup(group.Id, longName);

        // Assert
        Assert.Equal(30, group.Name.Length);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void MoveNoteToGroup_ExistingNote_UpdatesGroupId()
    {
        // Arrange
        var group = new NoteGroup { Name = "Test Group" };
        _groups.Add(group);
        
        var note = CreateNoteViewModel();
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act
        _service.MoveNoteToGroup(note.Id, group.Id);

        // Assert
        Assert.Equal(group.Id, note.GroupId);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void MoveNoteToGroup_ToUngrouped_SetsGroupIdToNull()
    {
        // Arrange
        var group = new NoteGroup { Name = "Test Group" };
        _groups.Add(group);
        
        var note = CreateNoteViewModel();
        note.GroupId = group.Id;
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act
        _service.MoveNoteToGroup(note.Id, null);

        // Assert
        Assert.Null(note.GroupId);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void MoveNoteToGroup_NonExistentNote_DoesNothing()
    {
        // Arrange
        var group = new NoteGroup { Name = "Test Group" };
        _groups.Add(group);
        _saveCallbackCalled = false;

        // Act
        _service.MoveNoteToGroup(Guid.NewGuid(), group.Id);

        // Assert
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void GetAllGroups_ReturnsReadOnlyList()
    {
        // Arrange
        var group1 = new NoteGroup { Name = "Group 1" };
        var group2 = new NoteGroup { Name = "Group 2" };
        _groups.Add(group1);
        _groups.Add(group2);

        // Act
        var result = _service.GetAllGroups();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(group1, result);
        Assert.Contains(group2, result);
        Assert.IsAssignableFrom<IReadOnlyList<NoteGroup>>(result);
    }

    [Fact]
    public void GetAllGroups_EmptyCollection_ReturnsEmptyList()
    {
        // Act
        var result = _service.GetAllGroups();

        // Assert
        Assert.Empty(result);
        Assert.IsAssignableFrom<IReadOnlyList<NoteGroup>>(result);
    }

    [Fact]
    public void DeleteGroup_WithMultipleGroups_OnlyDeletesSpecifiedGroup()
    {
        // Arrange
        var group1 = new NoteGroup { Name = "Group 1" };
        var group2 = new NoteGroup { Name = "Group 2" };
        _groups.Add(group1);
        _groups.Add(group2);
        
        var note1 = CreateNoteViewModel();
        note1.GroupId = group1.Id;
        var note2 = CreateNoteViewModel();
        note2.GroupId = group2.Id;
        _notes.Add(note1);
        _notes.Add(note2);

        // Act
        _service.DeleteGroup(group1.Id);

        // Assert
        Assert.Single(_groups);
        Assert.Equal(group2, _groups[0]);
        Assert.Null(note1.GroupId);
        Assert.Equal(group2.Id, note2.GroupId);
    }

    // Mock services for testing
    private class MockNoteService : INoteService
    {
        public void Dispose() { }
        public Note CreateNote() => new Note();
        public void AddNote(Note note) { }
        public void DeleteNote(Guid id) { }
        public Note? GetNoteById(Guid id) => null;
        public IReadOnlyList<Note> GetAllNotes() => Array.Empty<Note>();
        public void UpdateNote(Note note) { }
        public void TogglePin(Guid id) { }
        public double AdjustOpacity(Guid id, double step) => 0.9;
        public void LoadNotes(IEnumerable<Note> notes) { }
    }

    private class MockFormatterService : IFormatterService
    {
        public string FormatJson(string input) => input;
        public string FormatXml(string input) => input;
        public bool IsValidJson(string input) => true;
        public bool IsValidXml(string input) => true;
    }

    private class MockSearchService : ISearchService
    {
        public IReadOnlyList<SearchMatch> FindMatches(string content, string searchTerm) => Array.Empty<SearchMatch>();
        public int GetNextMatchIndex(int currentIndex, int totalMatches) => 0;
        public int GetPreviousMatchIndex(int currentIndex, int totalMatches) => 0;
    }

    private class MockDebounceService : IDebounceService
    {
        public void Dispose() { }
        public void Debounce(string key, Action action, int delayMs) => action();
        public void Cancel(string key) { }
        public void CancelAll() { }
    }
}