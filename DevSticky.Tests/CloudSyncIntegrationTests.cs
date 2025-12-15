using DevSticky.Models;
using DevSticky.Services;
using DevSticky.Interfaces;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Integration tests for cloud sync operations
/// </summary>
public class CloudSyncIntegrationTests : IDisposable
{
    private readonly MockCloudStorageProvider _mockProvider;
    private readonly MockEncryptionService _mockEncryption;
    private readonly CloudSyncService _cloudSyncService;
    private readonly TestNoteService _noteService;

    public CloudSyncIntegrationTests()
    {
        _mockProvider = new MockCloudStorageProvider();
        _mockEncryption = new MockEncryptionService();
        _noteService = new TestNoteService();
        
        var registry = new CloudProviderRegistry();
        registry.RegisterProvider(CloudProvider.OneDrive, () => _mockProvider);
        
        var storageService = new TestStorageService();
        var errorHandler = new ErrorHandler();
        
        var saveQueueService = new TestSaveQueueService();
        _cloudSyncService = new CloudSyncService(_noteService, storageService, _mockEncryption, registry, errorHandler, saveQueueService);
    }

    [Fact]
    public async Task CloudSync_ConnectAndSync_ShouldUploadNotes()
    {
        // Arrange
        var note1 = new Note { Id = Guid.NewGuid(), Title = "Note 1", Content = "Content 1" };
        var note2 = new Note { Id = Guid.NewGuid(), Title = "Note 2", Content = "Content 2" };
        
        _noteService.LoadNotes(new[] { note1, note2 });

        // Act - Connect to cloud provider
        var connected = await _cloudSyncService.ConnectAsync(CloudProvider.OneDrive);
        Assert.True(connected);

        // Act - Sync notes
        var result = await _cloudSyncService.SyncAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.NotesUploaded);
        Assert.Equal(0, result.NotesDownloaded);
        Assert.Equal(2, _mockProvider.UploadedFiles.Count);
    }

    [Fact]
    public async Task CloudSync_ConflictResolution_ShouldHandleConflicts()
    {
        // Arrange
        var localNote = new Note 
        { 
            Id = Guid.NewGuid(), 
            Title = "Local Version", 
            Content = "Local Content",
            SyncVersion = 1,
            ModifiedDate = DateTime.UtcNow.AddMinutes(-5)
        };

        var remoteNote = new Note 
        { 
            Id = localNote.Id, 
            Title = "Remote Version", 
            Content = "Remote Content",
            SyncVersion = 2,
            ModifiedDate = DateTime.UtcNow.AddMinutes(-1)
        };

        _noteService.LoadNotes(new[] { localNote });
        _mockProvider.AddRemoteFile($"{localNote.Id}.json", System.Text.Json.JsonSerializer.Serialize(remoteNote));

        // Act
        await _cloudSyncService.ConnectAsync(CloudProvider.OneDrive);
        var result = await _cloudSyncService.SyncAsync();

        // Assert - Should detect conflict
        Assert.True(result.Success);
        Assert.Equal(1, result.ConflictsDetected);
    }

    [Fact]
    public async Task CloudSync_EncryptionIntegration_ShouldEncryptData()
    {
        // Arrange
        var note = new Note { Id = Guid.NewGuid(), Title = "Secret Note", Content = "Secret Content" };
        _noteService.LoadNotes(new[] { note });

        // Act
        await _cloudSyncService.ConnectAsync(CloudProvider.OneDrive);
        await _cloudSyncService.SyncAsync();

        // Assert - Data should be encrypted before upload
        Assert.Single(_mockProvider.UploadedFiles);
        var uploadedContent = _mockProvider.UploadedFiles.Values.First();
        var contentString = System.Text.Encoding.UTF8.GetString(uploadedContent);
        Assert.StartsWith("ENCRYPTED:", contentString); // Mock encryption prefix
        Assert.DoesNotContain("Secret Content", contentString);
    }

    [Fact]
    public async Task CloudSync_RetryMechanism_ShouldRetryOnFailure()
    {
        // Arrange
        var note = new Note { Id = Guid.NewGuid(), Title = "Test Note", Content = "Test Content" };
        _noteService.LoadNotes(new[] { note });
        
        _mockProvider.FailNextOperations = 2; // Fail first 2 attempts

        // Act
        await _cloudSyncService.ConnectAsync(CloudProvider.OneDrive);
        var result = await _cloudSyncService.SyncAsync();

        // Assert - Should succeed after retries
        Assert.True(result.Success);
        Assert.Equal(3, _mockProvider.UploadAttempts); // 2 failures + 1 success
    }

    [Fact]
    public async Task CloudSync_QueueManagement_ShouldProcessQueue()
    {
        // Arrange
        var note1 = new Note { Id = Guid.NewGuid(), Title = "Note 1" };
        var note2 = new Note { Id = Guid.NewGuid(), Title = "Note 2" };
        var note3 = new Note { Id = Guid.NewGuid(), Title = "Note 3" };

        // Act - Queue notes for sync
        _cloudSyncService.QueueNoteForSync(note1.Id, SyncChangeType.CreateOrUpdate);
        _cloudSyncService.QueueNoteForSync(note2.Id, SyncChangeType.CreateOrUpdate);
        _cloudSyncService.QueueNoteForSync(note3.Id, SyncChangeType.Delete);

        // Assert - Queue should contain all changes
        Assert.Equal(3, _cloudSyncService.PendingChanges.Count);
        
        var createChange = _cloudSyncService.PendingChanges.First(c => c.ChangeType == SyncChangeType.CreateOrUpdate && c.NoteId == note1.Id);
        var updateChange = _cloudSyncService.PendingChanges.First(c => c.ChangeType == SyncChangeType.CreateOrUpdate && c.NoteId == note2.Id);
        var deleteChange = _cloudSyncService.PendingChanges.First(c => c.ChangeType == SyncChangeType.Delete);
        
        Assert.Equal(note1.Id, createChange.NoteId);
        Assert.Equal(note2.Id, updateChange.NoteId);
        Assert.Equal(note3.Id, deleteChange.NoteId);
    }

    public void Dispose()
    {
        _cloudSyncService?.Dispose();
        _noteService?.Dispose();
    }

    #region Mock Classes

    private class MockCloudStorageProvider : ICloudStorageProvider
    {
        public Dictionary<string, byte[]> UploadedFiles { get; } = new();
        public int UploadAttempts { get; private set; }
        public int FailNextOperations { get; set; }

        public string ProviderName => "MockProvider";
        public bool IsAuthenticated => true;

        public Task<bool> AuthenticateAsync() => Task.FromResult(true);
        public Task SignOutAsync() => Task.CompletedTask;

        public async Task<string?> UploadFileAsync(string remotePath, byte[] content)
        {
            UploadAttempts++;
            
            if (FailNextOperations > 0)
            {
                FailNextOperations--;
                throw new Exception("Simulated upload failure");
            }

            UploadedFiles[remotePath] = content;
            return $"etag-{remotePath}";
        }

        public Task<byte[]?> DownloadFileAsync(string remotePath)
        {
            return Task.FromResult(UploadedFiles.TryGetValue(remotePath, out var content) ? content : null);
        }

        public Task<bool> DeleteFileAsync(string remotePath)
        {
            var removed = UploadedFiles.Remove(remotePath);
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string remotePath)
        {
            var files = UploadedFiles.Keys
                .Where(k => k.StartsWith(remotePath))
                .Select(k => new CloudFileInfo 
                { 
                    Name = System.IO.Path.GetFileName(k), 
                    Path = k, 
                    Size = UploadedFiles[k].Length,
                    LastModified = DateTime.UtcNow
                })
                .ToList();
            return Task.FromResult<IReadOnlyList<CloudFileInfo>>(files);
        }

        public Task<CloudFileInfo?> GetFileInfoAsync(string remotePath)
        {
            if (UploadedFiles.TryGetValue(remotePath, out var content))
            {
                return Task.FromResult<CloudFileInfo?>(new CloudFileInfo
                {
                    Name = System.IO.Path.GetFileName(remotePath),
                    Path = remotePath,
                    Size = content.Length,
                    LastModified = DateTime.UtcNow
                });
            }
            return Task.FromResult<CloudFileInfo?>(null);
        }

        public Task<bool> CreateFolderAsync(string remotePath) => Task.FromResult(true);

        public void AddRemoteFile(string fileName, string content)
        {
            UploadedFiles[fileName] = System.Text.Encoding.UTF8.GetBytes(content);
        }

        public void Dispose() { }
    }

    private class MockEncryptionService : IEncryptionService
    {
        public byte[] Encrypt(byte[] data, string passphrase)
        {
            var prefix = System.Text.Encoding.UTF8.GetBytes("ENCRYPTED:");
            var result = new byte[prefix.Length + data.Length];
            Array.Copy(prefix, 0, result, 0, prefix.Length);
            Array.Copy(data, 0, result, prefix.Length, data.Length);
            return result;
        }

        public byte[] Decrypt(byte[] encryptedData, string passphrase)
        {
            var prefix = System.Text.Encoding.UTF8.GetBytes("ENCRYPTED:");
            if (encryptedData.Length > prefix.Length)
            {
                var result = new byte[encryptedData.Length - prefix.Length];
                Array.Copy(encryptedData, prefix.Length, result, 0, result.Length);
                return result;
            }
            return encryptedData;
        }

        public string DeriveKey(string passphrase, byte[] salt)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(passphrase + Convert.ToBase64String(salt)));
        }
    }

    private class TestNoteService : INoteService
    {
        private readonly List<Note> _notes = new();

        public Note CreateNote()
        {
            var note = new Note();
            _notes.Add(note);
            return note;
        }

        public void UpdateNote(Note note)
        {
            var existing = _notes.FirstOrDefault(n => n.Id == note.Id);
            if (existing != null)
            {
                existing.Title = note.Title;
                existing.Content = note.Content;
                existing.ModifiedDate = note.ModifiedDate;
            }
        }

        public void AddNote(Note note) => _notes.Add(note);
        public void DeleteNote(Guid id) => _notes.RemoveAll(n => n.Id == id);
        public Note? GetNoteById(Guid id) => _notes.FirstOrDefault(n => n.Id == id);
        public IReadOnlyList<Note> GetAllNotes() => _notes.AsReadOnly();
        public void TogglePin(Guid id) { }
        public double AdjustOpacity(Guid id, double step) => 0.9;
        public void LoadNotes(IEnumerable<Note> notes) => _notes.AddRange(notes);
        public void Dispose() { }
        
        // Lazy loading methods
        public Task PreloadContentsAsync(IEnumerable<Guid> noteIds) => Task.CompletedTask;
        public Task<bool> EnsureContentLoadedAsync(Guid noteId) => Task.FromResult(true);
        public void UnloadNoteContent(Guid noteId) { }
        public Task<string?> GetNoteContentAsync(Guid noteId) => Task.FromResult<string?>(GetNoteById(noteId)?.Content);
        public Task SaveNoteContentAsync(Guid noteId, string content) => Task.CompletedTask;
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
        
        // Lazy loading support
        public bool IsLazyLoadingFormat => false;
        public Task<AppData> LoadMetadataOnlyAsync() => LoadAsync();
        public Task<string?> LoadNoteContentAsync(Guid noteId) => Task.FromResult<string?>(null);
        public Task SaveNoteContentAsync(Guid noteId, string content) => Task.CompletedTask;
        public Task DeleteNoteContentAsync(Guid noteId) => Task.CompletedTask;
        public Task<bool> MigrateToLazyLoadingFormatAsync() => Task.FromResult(false);
        public Task PreloadNoteContentsAsync(IEnumerable<Guid> noteIds) => Task.CompletedTask;
        
        public void Dispose() { }
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

    #endregion
}