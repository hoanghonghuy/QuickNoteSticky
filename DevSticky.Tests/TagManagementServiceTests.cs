using System.Collections.ObjectModel;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.ViewModels;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for TagManagementService
/// </summary>
public class TagManagementServiceTests
{
    private readonly ObservableCollection<NoteTag> _tags;
    private readonly ObservableCollection<NoteViewModel> _notes;
    private readonly CacheService _cacheService;
    private readonly TagManagementService _service;
    private readonly MockNoteService _mockNoteService;
    private readonly MockFormatterService _mockFormatterService;
    private readonly MockSearchService _mockSearchService;
    private readonly MockDebounceService _mockDebounceService;
    private bool _saveCallbackCalled;

    public TagManagementServiceTests()
    {
        _tags = new ObservableCollection<NoteTag>();
        _notes = new ObservableCollection<NoteViewModel>();
        _cacheService = new CacheService();
        _mockNoteService = new MockNoteService();
        _mockFormatterService = new MockFormatterService();
        _mockSearchService = new MockSearchService();
        _mockDebounceService = new MockDebounceService();
        _saveCallbackCalled = false;
        
        _service = new TagManagementService(
            _tags,
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
    public void Constructor_WithNullTags_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TagManagementService(
            null!,
            _notes,
            _cacheService,
            () => { }));
    }

    [Fact]
    public void Constructor_WithNullNotes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TagManagementService(
            _tags,
            null!,
            _cacheService,
            () => { }));
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TagManagementService(
            _tags,
            _notes,
            null!,
            () => { }));
    }

    [Fact]
    public void Constructor_WithNullSaveCallback_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TagManagementService(
            _tags,
            _notes,
            _cacheService,
            null!));
    }

    [Fact]
    public void CreateTag_WithoutParameters_CreatesTagWithDefaults()
    {
        // Act
        var tag = _service.CreateTag();

        // Assert
        Assert.Single(_tags);
        Assert.NotNull(tag);
        Assert.NotEqual(Guid.Empty, tag.Id);
        Assert.NotEmpty(tag.Name);
        Assert.NotEmpty(tag.Color);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void CreateTag_WithNameAndColor_CreatesTagWithSpecifiedValues()
    {
        // Arrange
        var tagName = "Test Tag";
        var tagColor = "#FF0000";

        // Act
        var tag = _service.CreateTag(tagName, tagColor);

        // Assert
        Assert.Single(_tags);
        Assert.Equal(tagName, tag.Name);
        Assert.Equal(tagColor, tag.Color);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void CreateTag_MultipleTags_UsesDefaultColorsInSequence()
    {
        // Act
        var tag1 = _service.CreateTag();
        _saveCallbackCalled = false;
        var tag2 = _service.CreateTag();
        _saveCallbackCalled = false;
        var tag3 = _service.CreateTag();

        // Assert
        Assert.Equal(3, _tags.Count);
        Assert.NotEqual(tag1.Color, tag2.Color);
        Assert.NotEqual(tag2.Color, tag3.Color);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void DeleteTag_ExistingTag_RemovesTagAndFromAllNotes()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        
        var note1 = CreateNoteViewModel();
        var note2 = CreateNoteViewModel();
        var note3 = CreateNoteViewModel();
        
        note1.TagIds.Add(tag.Id);
        note2.TagIds.Add(tag.Id);
        // note3 doesn't have the tag
        
        _notes.Add(note1);
        _notes.Add(note2);
        _notes.Add(note3);
        
        _saveCallbackCalled = false;

        // Act
        _service.DeleteTag(tag.Id);

        // Assert
        Assert.Empty(_tags);
        Assert.DoesNotContain(tag.Id, note1.TagIds);
        Assert.DoesNotContain(tag.Id, note2.TagIds);
        Assert.Empty(note3.TagIds); // Should remain empty
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void DeleteTag_NonExistentTag_DoesNothing()
    {
        // Arrange
        var existingTag = new NoteTag { Name = "Existing Tag", Color = "#FF0000" };
        _tags.Add(existingTag);
        _saveCallbackCalled = false;

        // Act
        _service.DeleteTag(Guid.NewGuid());

        // Assert
        Assert.Single(_tags);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void RenameTag_ExistingTag_UpdatesName()
    {
        // Arrange
        var tag = new NoteTag { Name = "Old Name", Color = "#FF0000" };
        _tags.Add(tag);
        var newName = "New Name";
        _saveCallbackCalled = false;

        // Act
        _service.RenameTag(tag.Id, newName);

        // Assert
        Assert.Equal(newName, tag.Name);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void RenameTag_NonExistentTag_DoesNothing()
    {
        // Arrange
        var tag = new NoteTag { Name = "Original Name", Color = "#FF0000" };
        _tags.Add(tag);
        _saveCallbackCalled = false;

        // Act
        _service.RenameTag(Guid.NewGuid(), "New Name");

        // Assert
        Assert.Equal("Original Name", tag.Name);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void RenameTag_LongName_TruncatesTo20Characters()
    {
        // Arrange
        var tag = new NoteTag { Name = "Short Name", Color = "#FF0000" };
        _tags.Add(tag);
        var longName = new string('A', 30); // 30 characters
        _saveCallbackCalled = false;

        // Act
        _service.RenameTag(tag.Id, longName);

        // Assert
        Assert.Equal(20, tag.Name.Length);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void ChangeTagColor_ExistingTag_UpdatesColor()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        var newColor = "#00FF00";
        _saveCallbackCalled = false;

        // Act
        _service.ChangeTagColor(tag.Id, newColor);

        // Assert
        Assert.Equal(newColor, tag.Color);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void ChangeTagColor_NonExistentTag_DoesNothing()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        _saveCallbackCalled = false;

        // Act
        _service.ChangeTagColor(Guid.NewGuid(), "#00FF00");

        // Assert
        Assert.Equal("#FF0000", tag.Color);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void AddTagToNote_ExistingNote_AddsTag()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        
        var note = CreateNoteViewModel();
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act
        _service.AddTagToNote(note.Id, tag.Id);

        // Assert
        Assert.Contains(tag.Id, note.TagIds);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void AddTagToNote_TagAlreadyExists_DoesNotDuplicate()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        
        var note = CreateNoteViewModel();
        note.TagIds.Add(tag.Id);
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act
        _service.AddTagToNote(note.Id, tag.Id);

        // Assert
        Assert.Single(note.TagIds);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void AddTagToNote_NoteHas5Tags_DoesNotAdd()
    {
        // Arrange
        var tags = Enumerable.Range(0, 6).Select(_ => new NoteTag { Name = "Tag", Color = "#FF0000" }).ToList();
        foreach (var tag in tags)
        {
            _tags.Add(tag);
        }
        
        var note = CreateNoteViewModel();
        // Add 5 tags (maximum)
        for (int i = 0; i < 5; i++)
        {
            note.TagIds.Add(tags[i].Id);
        }
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act - Try to add 6th tag
        _service.AddTagToNote(note.Id, tags[5].Id);

        // Assert
        Assert.Equal(5, note.TagIds.Count);
        Assert.DoesNotContain(tags[5].Id, note.TagIds);
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void AddTagToNote_NonExistentNote_DoesNothing()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        _saveCallbackCalled = false;

        // Act
        _service.AddTagToNote(Guid.NewGuid(), tag.Id);

        // Assert
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void RemoveTagFromNote_ExistingNoteAndTag_RemovesTag()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        
        var note = CreateNoteViewModel();
        note.TagIds.Add(tag.Id);
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act
        _service.RemoveTagFromNote(note.Id, tag.Id);

        // Assert
        Assert.DoesNotContain(tag.Id, note.TagIds);
        Assert.True(_saveCallbackCalled);
    }

    [Fact]
    public void RemoveTagFromNote_TagNotOnNote_DoesNothing()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        
        var note = CreateNoteViewModel();
        _notes.Add(note);
        _saveCallbackCalled = false;

        // Act
        _service.RemoveTagFromNote(note.Id, tag.Id);

        // Assert
        Assert.Empty(note.TagIds);
        Assert.True(_saveCallbackCalled); // Still calls save even if no change
    }

    [Fact]
    public void RemoveTagFromNote_NonExistentNote_DoesNothing()
    {
        // Arrange
        var tag = new NoteTag { Name = "Test Tag", Color = "#FF0000" };
        _tags.Add(tag);
        _saveCallbackCalled = false;

        // Act
        _service.RemoveTagFromNote(Guid.NewGuid(), tag.Id);

        // Assert
        Assert.False(_saveCallbackCalled);
    }

    [Fact]
    public void GetAllTags_ReturnsReadOnlyList()
    {
        // Arrange
        var tag1 = new NoteTag { Name = "Tag 1", Color = "#FF0000" };
        var tag2 = new NoteTag { Name = "Tag 2", Color = "#00FF00" };
        _tags.Add(tag1);
        _tags.Add(tag2);

        // Act
        var result = _service.GetAllTags();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(tag1, result);
        Assert.Contains(tag2, result);
        Assert.IsAssignableFrom<IReadOnlyList<NoteTag>>(result);
    }

    [Fact]
    public void GetAllTags_EmptyCollection_ReturnsEmptyList()
    {
        // Act
        var result = _service.GetAllTags();

        // Assert
        Assert.Empty(result);
        Assert.IsAssignableFrom<IReadOnlyList<NoteTag>>(result);
    }

    [Fact]
    public void DeleteTag_WithMultipleTags_OnlyDeletesSpecifiedTag()
    {
        // Arrange
        var tag1 = new NoteTag { Name = "Tag 1", Color = "#FF0000" };
        var tag2 = new NoteTag { Name = "Tag 2", Color = "#00FF00" };
        _tags.Add(tag1);
        _tags.Add(tag2);
        
        var note = CreateNoteViewModel();
        note.TagIds.Add(tag1.Id);
        note.TagIds.Add(tag2.Id);
        _notes.Add(note);

        // Act
        _service.DeleteTag(tag1.Id);

        // Assert
        Assert.Single(_tags);
        Assert.Equal(tag2, _tags[0]);
        Assert.DoesNotContain(tag1.Id, note.TagIds);
        Assert.Contains(tag2.Id, note.TagIds);
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
        
        // Lazy loading methods
        public Task PreloadContentsAsync(IEnumerable<Guid> noteIds) => Task.CompletedTask;
        public Task<bool> EnsureContentLoadedAsync(Guid noteId) => Task.FromResult(true);
        public void UnloadNoteContent(Guid noteId) { }
        public Task<string?> GetNoteContentAsync(Guid noteId) => Task.FromResult<string?>(null);
        public Task SaveNoteContentAsync(Guid noteId, string content) => Task.CompletedTask;
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