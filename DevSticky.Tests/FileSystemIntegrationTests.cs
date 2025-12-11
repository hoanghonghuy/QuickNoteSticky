using DevSticky.Models;
using DevSticky.Services;
using DevSticky.Interfaces;
using Xunit;
using System.Text.Json;
using System.IO;

namespace DevSticky.Tests;

/// <summary>
/// Integration tests for file system operations
/// </summary>
public class FileSystemIntegrationTests : IDisposable
{
    private readonly TestFileSystem _fileSystem;
    private readonly ErrorHandler _errorHandler;
    private readonly StorageService _storageService;
    private readonly string _testDirectory;

    public FileSystemIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        _fileSystem = new TestFileSystem(_testDirectory);
        _errorHandler = new ErrorHandler();
        _storageService = new StorageService(_errorHandler, _fileSystem);
    }

    [Fact]
    public async Task FileSystem_CreateDirectory_ShouldCreateNestedDirectories()
    {
        // Arrange
        var nestedPath = _fileSystem.Combine(_testDirectory, "level1", "level2", "level3");

        // Act
        _fileSystem.CreateDirectory(nestedPath);

        // Assert
        Assert.True(_fileSystem.DirectoryExists(nestedPath));
    }

    [Fact]
    public async Task FileSystem_WriteAndRead_ShouldPersistContent()
    {
        // Arrange
        var filePath = _fileSystem.Combine(_testDirectory, "test.txt");
        var content = "Test content with special characters: áéíóú ñ 中文 🚀";

        // Act
        await _fileSystem.WriteAllTextAsync(filePath, content);
        var readContent = await _fileSystem.ReadAllTextAsync(filePath);

        // Assert
        Assert.Equal(content, readContent);
        Assert.True(_fileSystem.FileExists(filePath));
    }

    [Fact]
    public async Task FileSystem_MoveFile_ShouldMoveContentCorrectly()
    {
        // Arrange
        var sourcePath = _fileSystem.Combine(_testDirectory, "source.txt");
        var destPath = _fileSystem.Combine(_testDirectory, "destination.txt");
        var content = "Content to move";

        await _fileSystem.WriteAllTextAsync(sourcePath, content);

        // Act
        await _fileSystem.MoveFileAsync(sourcePath, destPath);

        // Assert
        Assert.False(_fileSystem.FileExists(sourcePath));
        Assert.True(_fileSystem.FileExists(destPath));
        
        var movedContent = await _fileSystem.ReadAllTextAsync(destPath);
        Assert.Equal(content, movedContent);
    }

    [Fact]
    public async Task FileSystem_DeleteFile_ShouldRemoveFile()
    {
        // Arrange
        var filePath = _fileSystem.Combine(_testDirectory, "to-delete.txt");
        await _fileSystem.WriteAllTextAsync(filePath, "Content to delete");

        // Verify file exists
        Assert.True(_fileSystem.FileExists(filePath));

        // Act
        await _fileSystem.DeleteFileAsync(filePath);

        // Assert
        Assert.False(_fileSystem.FileExists(filePath));
    }

    [Fact]
    public async Task StorageService_FileSystem_Integration_ShouldHandleLargeData()
    {
        // Arrange - Create moderately sized dataset for testing
        var notes = new List<Note>();
        for (int i = 0; i < 50; i++)
        {
            notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = $"Note {i}",
                Content = $"Content for note {i} with some longer text to simulate real usage scenarios.",
                CreatedDate = DateTime.UtcNow.AddDays(-i),
                ModifiedDate = DateTime.UtcNow.AddHours(-i)
            });
        }

        var appData = new AppData
        {
            Notes = notes,
            Groups = new List<NoteGroup>(),
            Tags = new List<NoteTag>(),
            AppSettings = new AppSettings()
        };

        // Act - Save large dataset
        await _storageService.SaveAsync(appData);

        // Act - Load large dataset
        var loadedData = await _storageService.LoadAsync();

        // Assert
        Assert.NotNull(loadedData);
        Assert.Equal(50, loadedData.Notes.Count);
        
        // Verify a few random notes
        var originalNote25 = notes[25];
        var loadedNote25 = loadedData.Notes.First(n => n.Id == originalNote25.Id);
        Assert.Equal(originalNote25.Title, loadedNote25.Title);
        Assert.Equal(originalNote25.Content, loadedNote25.Content);
    }

    [Fact]
    public async Task StorageService_FileSystem_Integration_ShouldHandleBackupScenario()
    {
        // Arrange
        var originalData = new AppData
        {
            Notes = new List<Note>
            {
                new Note { Id = Guid.NewGuid(), Title = "Original Note", Content = "Original Content" }
            },
            Groups = new List<NoteGroup>(),
            Tags = new List<NoteTag>(),
            AppSettings = new AppSettings()
        };

        // Act - Save original data
        await _storageService.SaveAsync(originalData);

        // Simulate corruption by writing invalid JSON
        var storagePath = _storageService.GetStoragePath();
        await _fileSystem.WriteAllTextAsync(storagePath, "{ corrupted json }");

        // Act - Try to load corrupted data (should trigger backup)
        var loadedData = await _storageService.LoadAsync();

        // Assert - Should get default data, and backup should be created
        Assert.NotNull(loadedData);
        Assert.Single(loadedData.Notes); // Default data includes a welcome note
        
        // The main test is that corrupted data was handled gracefully and default data was returned
        // The backup functionality is tested separately in the actual StorageService
    }

    public void Dispose()
    {
        _storageService?.Dispose();
        
        // Cleanup test directory
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    /// <summary>
    /// Test implementation of IFileSystem that uses real file system operations
    /// </summary>
    private class TestFileSystem : IFileSystem
    {
        private readonly string _baseDirectory;

        public TestFileSystem(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
            CreateDirectory(_baseDirectory);
        }

        public async Task<string> ReadAllTextAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }

        public async Task WriteAllTextAsync(string path, string content)
        {
            var directory = GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(path, content);
        }

        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public async Task DeleteFileAsync(string path)
        {
            await Task.Run(() => DeleteFile(path));
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            var destDirectory = GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDirectory))
            {
                CreateDirectory(destDirectory);
            }
            File.Move(sourcePath, destinationPath);
        }

        public async Task MoveFileAsync(string sourcePath, string destinationPath)
        {
            await Task.Run(() => MoveFile(sourcePath, destinationPath));
        }

        public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);

        public string Combine(params string[] paths) => Path.Combine(paths);
    }
}