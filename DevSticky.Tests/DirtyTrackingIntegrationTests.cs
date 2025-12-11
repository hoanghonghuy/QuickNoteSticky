using DevSticky.Models;
using DevSticky.ViewModels;
using DevSticky.Services;
using DevSticky.Interfaces;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Integration tests for dirty tracking functionality across ViewModels and Models
/// </summary>
public class DirtyTrackingIntegrationTests
{
    private readonly MockNoteService _mockNoteService;
    private readonly MockFormatterService _mockFormatterService;
    private readonly MockSearchService _mockSearchService;
    private readonly MockDebounceService _mockDebounceService;

    public DirtyTrackingIntegrationTests()
    {
        _mockNoteService = new MockNoteService();
        _mockFormatterService = new MockFormatterService();
        _mockSearchService = new MockSearchService();
        _mockDebounceService = new MockDebounceService();
    }

    [Fact]
    public void NoteViewModel_ShouldReflectUnderlyingNoteDirtyState()
    {
        // Arrange
        var note = new Note();
        note.MarkClean(); // Ensure it starts clean
        
        var viewModel = new NoteViewModel(
            note,
            _mockNoteService,
            _mockFormatterService,
            _mockSearchService,
            _mockDebounceService
        );

        // Act & Assert - Initially clean
        Assert.False(viewModel.IsDirty);
        Assert.False(note.IsDirty);

        // Act - Modify through ViewModel
        viewModel.Title = "New Title";

        // Assert - Both should be dirty
        Assert.True(viewModel.IsDirty);
        Assert.True(note.IsDirty);

        // Act - Mark clean
        note.MarkClean();

        // Assert - Both should be clean
        Assert.False(viewModel.IsDirty);
        Assert.False(note.IsDirty);
    }

    [Fact]
    public void NoteViewModel_ContentChange_ShouldMakeNoteDirty()
    {
        // Arrange
        var note = new Note();
        note.MarkClean();
        
        var viewModel = new NoteViewModel(
            note,
            _mockNoteService,
            _mockFormatterService,
            _mockSearchService,
            _mockDebounceService
        );

        // Verify initial state
        Assert.False(note.IsDirty, "Note should start clean");
        Assert.False(viewModel.IsDirty, "ViewModel should start clean");

        // Act
        viewModel.Content = "New content";

        // Debug: Check what happened
        Assert.Equal("New content", viewModel.Content);
        Assert.Equal("New content", note.Content);

        // Assert
        Assert.True(note.IsDirty, "Note should be dirty after content change");
        Assert.True(viewModel.IsDirty, "ViewModel should be dirty after content change");
    }

    [Fact]
    public void NoteViewModel_MultiplePropertyChanges_ShouldKeepNoteDirty()
    {
        // Arrange
        var note = new Note();
        note.MarkClean();
        
        var viewModel = new NoteViewModel(
            note,
            _mockNoteService,
            _mockFormatterService,
            _mockSearchService,
            _mockDebounceService
        );

        // Act
        viewModel.Title = "New Title";
        viewModel.Content = "New Content";
        viewModel.Language = "CSharp";
        viewModel.IsPinned = false;

        // Assert
        Assert.True(note.IsDirty);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("New Title", note.Title);
        Assert.Equal("New Content", note.Content);
        Assert.Equal("CSharp", note.Language);
        Assert.False(note.IsPinned);
    }

    [Fact]
    public void NoteViewModel_ToNote_ShouldReturnSameInstance()
    {
        // Arrange
        var note = new Note();
        var viewModel = new NoteViewModel(
            note,
            _mockNoteService,
            _mockFormatterService,
            _mockSearchService,
            _mockDebounceService
        );

        // Act
        var returnedNote = viewModel.ToNote();

        // Assert
        Assert.Same(note, returnedNote);
    }

    [Fact]
    public void Note_PropertyChanges_ShouldUpdateModifiedDate()
    {
        // Arrange
        var note = new Note();
        var originalModifiedDate = note.ModifiedDate;
        
        // Wait to ensure time difference
        Thread.Sleep(10);

        // Act
        note.Content = "Modified content";

        // Assert
        Assert.True(note.ModifiedDate > originalModifiedDate);
        Assert.True(note.IsDirty);
    }

    #region Mock Classes

    private class MockNoteService : INoteService
    {
        public Note CreateNote() => new Note();
        public void UpdateNote(Note note) { }
        public void DeleteNote(Guid id) { }
        public Note? GetNoteById(Guid id) => null;
        public IReadOnlyList<Note> GetAllNotes() => Array.Empty<Note>();
        public void TogglePin(Guid id) { }
        public double AdjustOpacity(Guid id, double step) => 0.9;
        public void LoadNotes(IEnumerable<Note> notes) { }
        public void Dispose() { }
    }

    private class MockFormatterService : IFormatterService
    {
        public bool IsValidJson(string content) => false;
        public bool IsValidXml(string content) => false;
        public string FormatJson(string json) => json;
        public string FormatXml(string xml) => xml;
    }

    private class MockSearchService : ISearchService
    {
        public IReadOnlyList<SearchMatch> FindMatches(string content, string searchTerm) => Array.Empty<SearchMatch>();
        public int GetNextMatchIndex(int currentIndex, int totalMatches) => 0;
        public int GetPreviousMatchIndex(int currentIndex, int totalMatches) => 0;
    }

    private class MockDebounceService : IDebounceService
    {
        public void Debounce(string key, Action action, int delayMs) { }
        public void Cancel(string key) { }
        public void Dispose() { }
    }

    #endregion
}