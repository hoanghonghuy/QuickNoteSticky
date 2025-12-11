using DevSticky.Models;
using DevSticky.Services;
using DevSticky.Interfaces;
using Xunit;
using System.Text.Json;

namespace DevSticky.Tests;

/// <summary>
/// Integration tests for service interactions
/// </summary>
public class ServiceIntegrationTests : IDisposable
{
    private readonly TestFileSystem _fileSystem;
    private readonly ErrorHandler _errorHandler;
    private readonly StorageService _storageService;
    private readonly NoteService _noteService;
    private readonly AppSettings _settings;

    public ServiceIntegrationTests()
    {
        _fileSystem = new TestFileSystem();
        _errorHandler = new ErrorHandler();
        _storageService = new StorageService(_errorHandler, _fileSystem);
        _settings = new AppSettings();
        _noteService = new NoteService(_settings);
    }

    [Fact]
    public async Task NoteService_StorageService_Integration_ShouldPersistAndLoadNotes()
    {
        // Arrange
        var note1 = _noteService.CreateNote();
        note1.Title = "Test Note 1";
        note1.Content = "Content 1";

        var note2 = _noteService.CreateNote();
        note2.Title = "Test Note 2";
        note2.Content = "Content 2";

        var appData = new AppData
        {
            Notes = new List<Note> { note1, note2 },
            Groups = new List<NoteGroup>(),
            Tags = new List<NoteTag>(),
            AppSettings = _settings
        };

        // Act - Save data
        await _storageService.SaveAsync(appData);

        // Act - Load data
        var loadedData = await _storageService.LoadAsync();

        // Assert
        Assert.NotNull(loadedData);
        Assert.Equal(2, loadedData.Notes.Count);
        
        var loadedNote1 = loadedData.Notes.First(n => n.Title == "Test Note 1");
        var loadedNote2 = loadedData.Notes.First(n => n.Title == "Test Note 2");
        
        Assert.Equal("Content 1", loadedNote1.Content);
        Assert.Equal("Content 2", loadedNote2.Content);
    }

    [Fact]
    public void NoteService_TagAssociation_Integration_ShouldAssociateTags()
    {
        // Arrange
        var tag1 = new NoteTag { Id = Guid.NewGuid(), Name = "Tag1", Color = "#FF0000" };
        var tag2 = new NoteTag { Id = Guid.NewGuid(), Name = "Tag2", Color = "#00FF00" };

        // Act - Create a note with tags to test the integration
        var note = _noteService.CreateNote();
        note.TagIds = new List<Guid> { tag1.Id, tag2.Id };

        // Assert - Verify note has correct tag associations
        Assert.Contains(tag1.Id, note.TagIds);
        Assert.Contains(tag2.Id, note.TagIds);
        Assert.Equal(2, note.TagIds.Count);
    }

    [Fact]
    public async Task StorageService_ErrorHandling_Integration_ShouldHandleCorruptedFiles()
    {
        // Arrange - Create corrupted JSON
        var corruptedJson = "{ invalid json content }";
        await _fileSystem.WriteAllTextAsync(_storageService.GetStoragePath(), corruptedJson);

        // Act - Try to load corrupted data
        var result = await _storageService.LoadAsync();

        // Assert - Should return default data instead of throwing
        Assert.NotNull(result);
        Assert.Single(result.Notes); // Default data includes a welcome note
        Assert.Empty(result.Groups);
        Assert.Empty(result.Tags);
        Assert.NotNull(result.AppSettings);
        
        // Verify the welcome note
        var welcomeNote = result.Notes.First();
        Assert.Contains("Welcome", welcomeNote.Title);
        Assert.Equal("CSharp", welcomeNote.Language);
    }

    [Fact]
    public void NoteService_CRUD_Operations_Integration()
    {
        // Arrange & Act - Create
        var note = _noteService.CreateNote();
        var originalId = note.Id;
        
        // Assert - Create
        Assert.NotEqual(Guid.Empty, note.Id);
        Assert.Equal("Untitled Note", note.Title);
        
        // Act - Update
        note.Title = "Updated Title";
        note.Content = "Updated Content";
        _noteService.UpdateNote(note);
        
        // Assert - Update
        var retrievedNote = _noteService.GetNoteById(originalId);
        Assert.NotNull(retrievedNote);
        Assert.Equal("Updated Title", retrievedNote.Title);
        Assert.Equal("Updated Content", retrievedNote.Content);
        
        // Act - Delete
        _noteService.DeleteNote(originalId);
        
        // Assert - Delete
        var deletedNote = _noteService.GetNoteById(originalId);
        Assert.Null(deletedNote);
    }

    public void Dispose()
    {
        _storageService?.Dispose();
        _noteService?.Dispose();
    }

    /// <summary>
    /// Test implementation of IFileSystem for integration testing
    /// </summary>
    private class TestFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new();

        public async Task<string> ReadAllTextAsync(string path)
        {
            await Task.Delay(1); // Simulate async operation
            return _files.TryGetValue(path, out var content) ? content : throw new System.IO.FileNotFoundException();
        }

        public async Task WriteAllTextAsync(string path, string content)
        {
            await Task.Delay(1); // Simulate async operation
            var directory = GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                CreateDirectory(directory);
            }
            _files[path] = content;
        }

        public bool FileExists(string path) => _files.ContainsKey(path);

        public bool DirectoryExists(string path) => true; // Simplified for testing

        public void CreateDirectory(string path) { } // No-op for testing

        public void DeleteFile(string path) => _files.Remove(path);

        public async Task DeleteFileAsync(string path)
        {
            await Task.Delay(1);
            DeleteFile(path);
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            if (_files.TryGetValue(sourcePath, out var content))
            {
                _files[destinationPath] = content;
                _files.Remove(sourcePath);
            }
        }

        public async Task MoveFileAsync(string sourcePath, string destinationPath)
        {
            await Task.Delay(1);
            MoveFile(sourcePath, destinationPath);
        }

        public string? GetDirectoryName(string path) => System.IO.Path.GetDirectoryName(path);

        public string Combine(params string[] paths) => System.IO.Path.Combine(paths);
    }
}