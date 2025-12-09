using System.Text;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Cloud Sync operations.
/// **Feature: devsticky-v2, Properties 14-17: Cloud Synchronization**
/// **Validates: Requirements 5.5, 5.7, 5.8, 5.9**
/// </summary>
public class CloudSyncPropertyTests
{
    /// <summary>
    /// Property 14: Sync queue ordering
    /// *For any* sequence of note modifications, the sync queue should maintain chronological order of changes.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SyncQueue_ShouldMaintainChronologicalOrder()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(2, 10)),
            noteCount =>
            {
                // Create a mock service to test queue ordering
                var noteService = new MockNoteService();
                var storageService = new MockStorageService();
                var encryptionService = new EncryptionService();
                
                var syncService = new CloudSyncService(
                    noteService, 
                    storageService, 
                    encryptionService,
                    _ => new MockCloudStorageProvider());

                // Queue notes with small delays to ensure ordering
                var queuedTimes = new List<DateTime>();
                for (int i = 0; i < noteCount; i++)
                {
                    var noteId = Guid.NewGuid();
                    syncService.QueueNoteForSync(noteId);
                    queuedTimes.Add(DateTime.UtcNow);
                    Thread.Sleep(1); // Small delay to ensure different timestamps
                }

                // Verify queue maintains chronological order
                var pendingChanges = syncService.PendingChanges;
                
                for (int i = 1; i < pendingChanges.Count; i++)
                {
                    if (pendingChanges[i].QueuedAt < pendingChanges[i - 1].QueuedAt)
                    {
                        return false;
                    }
                }

                return true;
            });
    }


    /// <summary>
    /// Property 15: Conflict detection accuracy
    /// *For any* note with local version L and remote version R where both have been modified since last sync, 
    /// a conflict should be detected.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictDetection_ShouldDetectWhenBothVersionsModified()
    {
        return Prop.ForAll(
            NoteGenerator(),
            NoteGenerator(),
            (localNote, remoteNote) =>
            {
                // Set up scenario where both have been modified since last sync
                var lastSyncTime = DateTime.UtcNow.AddHours(-1);
                localNote.LastSyncedDate = lastSyncTime;
                localNote.ModifiedDate = DateTime.UtcNow.AddMinutes(-30); // Modified after sync
                remoteNote.ModifiedDate = DateTime.UtcNow.AddMinutes(-20); // Also modified after sync

                var hasConflict = CloudSyncService.DetectConflict(localNote, remoteNote);

                return hasConflict == true;
            });
    }

    /// <summary>
    /// Property 15 (continued): No conflict when only one version modified
    /// *For any* note where only the remote version has been modified since last sync,
    /// no conflict should be detected.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictDetection_ShouldNotDetectWhenOnlyRemoteModified()
    {
        return Prop.ForAll(
            NoteGenerator(),
            NoteGenerator(),
            (localNote, remoteNote) =>
            {
                // Set up scenario where only remote has been modified since last sync
                var lastSyncTime = DateTime.UtcNow.AddHours(-1);
                localNote.LastSyncedDate = lastSyncTime;
                localNote.ModifiedDate = lastSyncTime.AddMinutes(-10); // Modified BEFORE sync
                remoteNote.ModifiedDate = DateTime.UtcNow.AddMinutes(-20); // Modified after sync

                var hasConflict = CloudSyncService.DetectConflict(localNote, remoteNote);

                return hasConflict == false;
            });
    }

    /// <summary>
    /// Property 16: Merge preserves both versions
    /// *For any* conflicting note versions, the merged result should contain content from both versions with conflict markers.
    /// **Validates: Requirements 5.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeNotes_ShouldPreserveBothVersionsContent()
    {
        return Prop.ForAll(
            NoteGenerator(),
            NoteGenerator(),
            (localNote, remoteNote) =>
            {
                // Ensure notes have different content
                localNote.Content = "Local content: " + localNote.Content;
                remoteNote.Content = "Remote content: " + remoteNote.Content;

                var mergedNote = CloudSyncService.MergeNotes(localNote, remoteNote);

                // Verify merged content contains both versions
                var containsLocal = mergedNote.Content.Contains(localNote.Content);
                var containsRemote = mergedNote.Content.Contains(remoteNote.Content);
                var containsLocalMarker = mergedNote.Content.Contains("<<<<<<< LOCAL");
                var containsRemoteMarker = mergedNote.Content.Contains(">>>>>>> REMOTE");
                var containsSeparator = mergedNote.Content.Contains("=======");

                return containsLocal && containsRemote && 
                       containsLocalMarker && containsRemoteMarker && containsSeparator;
            });
    }

    /// <summary>
    /// Property 16 (continued): Merge preserves note metadata
    /// *For any* conflicting note versions, the merged result should preserve the note ID and other metadata.
    /// **Validates: Requirements 5.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeNotes_ShouldPreserveNoteMetadata()
    {
        return Prop.ForAll(
            NoteGenerator(),
            NoteGenerator(),
            (localNote, remoteNote) =>
            {
                // Ensure same ID for both notes (simulating conflict on same note)
                remoteNote.Id = localNote.Id;

                var mergedNote = CloudSyncService.MergeNotes(localNote, remoteNote);

                // Verify metadata is preserved from local note
                return mergedNote.Id == localNote.Id &&
                       mergedNote.Title == localNote.Title &&
                       mergedNote.Language == localNote.Language &&
                       mergedNote.IsPinned == localNote.IsPinned;
            });
    }


    /// <summary>
    /// Property 17: Retry backoff progression
    /// *For any* sequence of sync failures, retry delays should follow exponential backoff pattern (1s, 2s, 4s, 8s...) up to maximum.
    /// **Validates: Requirements 5.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryBackoff_ShouldFollowExponentialPattern()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 10)),
            retryCount =>
            {
                var delay = CloudSyncService.CalculateRetryDelay(retryCount);

                // Expected delay: 2^retryCount seconds, capped at 60
                var expectedDelay = Math.Min((int)Math.Pow(2, retryCount), 60);

                return delay == expectedDelay;
            });
    }

    /// <summary>
    /// Property 17 (continued): Retry backoff should not exceed maximum
    /// *For any* retry count, the delay should never exceed the maximum (60 seconds).
    /// **Validates: Requirements 5.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryBackoff_ShouldNotExceedMaximum()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100)),
            retryCount =>
            {
                var delay = CloudSyncService.CalculateRetryDelay(retryCount);
                return delay <= 60;
            });
    }

    /// <summary>
    /// Property 17 (continued): Retry backoff should be monotonically increasing up to max
    /// *For any* two retry counts where a < b, delay(a) <= delay(b).
    /// **Validates: Requirements 5.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryBackoff_ShouldBeMonotonicallyIncreasing()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 10)),
            Arb.From(Gen.Choose(0, 10)),
            (a, b) =>
            {
                if (a > b) (a, b) = (b, a); // Ensure a <= b

                var delayA = CloudSyncService.CalculateRetryDelay(a);
                var delayB = CloudSyncService.CalculateRetryDelay(b);

                return delayA <= delayB;
            });
    }

    #region Generators

    private static Arbitrary<Note> NoteGenerator()
    {
        var gen = from id in Gen.Constant(Guid.NewGuid())
                  from title in Arb.Generate<NonEmptyString>()
                  from content in Arb.Generate<NonEmptyString>()
                  from language in Gen.Elements("PlainText", "CSharp", "JavaScript", "Markdown")
                  from isPinned in Arb.Generate<bool>()
                  from opacity in Gen.Choose(20, 100).Select(x => x / 100.0)
                  from syncVersion in Gen.Choose(0, 100).Select(x => (long)x)
                  select new Note
                  {
                      Id = id,
                      Title = title.Get,
                      Content = content.Get,
                      Language = language,
                      IsPinned = isPinned,
                      Opacity = opacity,
                      SyncVersion = syncVersion,
                      CreatedDate = DateTime.UtcNow.AddDays(-7),
                      ModifiedDate = DateTime.UtcNow
                  };

        return Arb.From(gen);
    }

    #endregion

    #region Mock Classes

    private class MockNoteService : INoteService
    {
        private readonly List<Note> _notes = new();

        public Note CreateNote()
        {
            var note = new Note { Id = Guid.NewGuid() };
            _notes.Add(note);
            return note;
        }

        public void DeleteNote(Guid id) => _notes.RemoveAll(n => n.Id == id);
        public void UpdateNote(Note note)
        {
            var index = _notes.FindIndex(n => n.Id == note.Id);
            if (index >= 0) _notes[index] = note;
            else _notes.Add(note);
        }
        public IReadOnlyList<Note> GetAllNotes() => _notes.AsReadOnly();
        public Note? GetNoteById(Guid id) => _notes.FirstOrDefault(n => n.Id == id);
        public void TogglePin(Guid id) { }
        public double AdjustOpacity(Guid id, double step) => 1.0;
        public void LoadNotes(IEnumerable<Note> notes)
        {
            _notes.Clear();
            _notes.AddRange(notes);
        }
    }

    private class MockStorageService : IStorageService
    {
        public Task<AppData> LoadAsync() => Task.FromResult(new AppData());
        public Task SaveAsync(AppData data) => Task.CompletedTask;
        public string GetStoragePath() => "mock://storage";
    }

    private class MockCloudStorageProvider : ICloudStorageProvider
    {
        public string ProviderName => "Mock";
        public bool IsAuthenticated => true;

        public Task<bool> AuthenticateAsync() => Task.FromResult(true);
        public Task SignOutAsync() => Task.CompletedTask;
        public Task<string?> UploadFileAsync(string remotePath, byte[] content) => Task.FromResult<string?>("etag");
        public Task<byte[]?> DownloadFileAsync(string remotePath) => Task.FromResult<byte[]?>(null);
        public Task<bool> DeleteFileAsync(string remotePath) => Task.FromResult(true);
        public Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string remotePath) => 
            Task.FromResult<IReadOnlyList<CloudFileInfo>>(Array.Empty<CloudFileInfo>());
        public Task<CloudFileInfo?> GetFileInfoAsync(string remotePath) => Task.FromResult<CloudFileInfo?>(null);
        public Task<bool> CreateFolderAsync(string remotePath) => Task.FromResult(true);
        public void Dispose() { }
    }

    #endregion
}
