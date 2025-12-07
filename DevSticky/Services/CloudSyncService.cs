using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for synchronizing notes with cloud storage providers.
/// Implements sync queue, conflict detection, and exponential backoff retry.
/// </summary>
public class CloudSyncService : ICloudSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Exponential backoff constants
    private const int InitialRetryDelaySeconds = 1;
    private const int MaxRetryDelaySeconds = 60;
    private const int MaxRetryAttempts = 3;

    private readonly INoteService _noteService;
    private readonly IStorageService _storageService;
    private readonly IEncryptionService _encryptionService;
    private readonly Func<CloudProvider, ICloudStorageProvider> _providerFactory;
    
    private ICloudStorageProvider? _cloudProvider;
    private string? _encryptionPassphrase;
    private readonly ConcurrentQueue<PendingSyncChange> _syncQueue = new();
    private readonly List<PendingSyncChange> _pendingChanges = new();
    private readonly object _pendingChangesLock = new();
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<SyncProgressEventArgs>? SyncProgress;

    /// <inheritdoc />
    public event EventHandler<SyncConflictEventArgs>? SyncConflict;

    /// <inheritdoc />
    public CloudProvider? CurrentProvider { get; private set; }

    /// <inheritdoc />
    public SyncStatus Status { get; private set; } = SyncStatus.Disconnected;

    /// <inheritdoc />
    public SyncResult? LastSyncResult { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<PendingSyncChange> PendingChanges
    {
        get
        {
            lock (_pendingChangesLock)
            {
                return _pendingChanges.ToList().AsReadOnly();
            }
        }
    }


    /// <summary>
    /// Creates a new CloudSyncService instance.
    /// </summary>
    public CloudSyncService(
        INoteService noteService,
        IStorageService storageService,
        IEncryptionService encryptionService,
        Func<CloudProvider, ICloudStorageProvider>? providerFactory = null)
    {
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _providerFactory = providerFactory ?? CreateDefaultProvider;
    }

    private static ICloudStorageProvider CreateDefaultProvider(CloudProvider provider)
    {
        return provider switch
        {
            CloudProvider.OneDrive => new OneDriveStorageProvider(),
            CloudProvider.GoogleDrive => new GoogleDriveStorageProvider(),
            _ => throw new ArgumentException($"Unknown cloud provider: {provider}", nameof(provider))
        };
    }

    /// <inheritdoc />
    public async Task<bool> ConnectAsync(CloudProvider provider)
    {
        if (Status == SyncStatus.Connecting || Status == SyncStatus.Syncing)
            return false;

        try
        {
            Status = SyncStatus.Connecting;
            RaiseProgress("Connecting", 0, 0, 1, "Authenticating with cloud provider...");

            // Disconnect from current provider if any
            if (_cloudProvider != null)
            {
                await DisconnectAsync();
            }

            _cloudProvider = _providerFactory(provider);
            var authenticated = await _cloudProvider.AuthenticateAsync();

            if (authenticated)
            {
                CurrentProvider = provider;
                Status = SyncStatus.Idle;
                RaiseProgress("Connected", 100, 1, 1, "Successfully connected to cloud provider.");
                return true;
            }
            else
            {
                _cloudProvider.Dispose();
                _cloudProvider = null;
                Status = SyncStatus.Disconnected;
                return false;
            }
        }
        catch (Exception ex)
        {
            Status = SyncStatus.Error;
            LastSyncResult = new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (_cloudProvider != null)
        {
            await _cloudProvider.SignOutAsync();
            _cloudProvider.Dispose();
            _cloudProvider = null;
        }

        CurrentProvider = null;
        Status = SyncStatus.Disconnected;
        _encryptionPassphrase = null;

        lock (_pendingChangesLock)
        {
            _pendingChanges.Clear();
        }
    }

    /// <inheritdoc />
    public void SetEncryptionPassphrase(string passphrase)
    {
        _encryptionPassphrase = passphrase;
    }

    /// <inheritdoc />
    public void QueueNoteForSync(Guid noteId, SyncChangeType changeType = SyncChangeType.CreateOrUpdate)
    {
        var change = new PendingSyncChange
        {
            NoteId = noteId,
            ChangeType = changeType,
            QueuedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        lock (_pendingChangesLock)
        {
            // Remove any existing pending change for this note
            _pendingChanges.RemoveAll(c => c.NoteId == noteId);
            _pendingChanges.Add(change);
        }
    }


    /// <inheritdoc />
    public async Task<SyncResult> SyncAsync()
    {
        if (_cloudProvider == null || !_cloudProvider.IsAuthenticated)
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "Not connected to cloud provider"
            };
        }

        if (Status == SyncStatus.Syncing)
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "Sync already in progress"
            };
        }

        Status = SyncStatus.Syncing;
        var result = new SyncResult();

        try
        {
            // Get local notes
            var localNotes = _noteService.GetAllNotes().ToList();
            
            // Get remote notes list
            var remoteFiles = await _cloudProvider.ListFilesAsync("notes");
            var remoteNoteIds = new HashSet<Guid>();

            RaiseProgress("Syncing", 10, 0, localNotes.Count + remoteFiles.Count, "Checking remote notes...");

            // Download and check remote notes
            int processed = 0;
            foreach (var file in remoteFiles.Where(f => !f.IsFolder && f.Name.EndsWith(".json")))
            {
                try
                {
                    if (Guid.TryParse(Path.GetFileNameWithoutExtension(file.Name), out var noteId))
                    {
                        remoteNoteIds.Add(noteId);
                        var localNote = localNotes.FirstOrDefault(n => n.Id == noteId);
                        
                        if (localNote == null)
                        {
                            // Note exists remotely but not locally - download it
                            var downloadedNote = await DownloadNoteAsync(noteId);
                            if (downloadedNote != null)
                            {
                                result.NotesDownloaded++;
                            }
                        }
                        else if (localNote.LastSyncedDate == null || file.LastModified > localNote.LastSyncedDate)
                        {
                            // Remote is newer - check for conflict
                            var remoteNote = await DownloadNoteAsync(noteId);
                            if (remoteNote != null)
                            {
                                var hasConflict = DetectConflict(localNote, remoteNote);
                                if (hasConflict)
                                {
                                    result.ConflictsDetected++;
                                    var resolution = await RaiseConflictAsync(localNote, remoteNote);
                                    if (resolution != SyncConflictResolution.None)
                                    {
                                        await ResolveConflictAsync(noteId, resolution, localNote, remoteNote);
                                        result.ConflictsResolved++;
                                    }
                                }
                                else
                                {
                                    // No conflict, update local with remote
                                    UpdateLocalNote(remoteNote);
                                    result.NotesDownloaded++;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Continue with other files
                }

                processed++;
                RaiseProgress("Syncing", 10 + (processed * 40 / Math.Max(1, remoteFiles.Count)), 
                    processed, localNotes.Count + remoteFiles.Count, $"Processing remote notes...");
            }

            // Upload local notes that are newer or don't exist remotely
            foreach (var localNote in localNotes)
            {
                try
                {
                    var shouldUpload = !remoteNoteIds.Contains(localNote.Id) ||
                                       localNote.ModifiedDate > (localNote.LastSyncedDate ?? DateTime.MinValue);

                    if (shouldUpload)
                    {
                        var uploaded = await UploadNoteAsync(localNote);
                        if (uploaded)
                        {
                            result.NotesUploaded++;
                        }
                    }
                }
                catch
                {
                    // Continue with other notes
                }

                processed++;
                RaiseProgress("Syncing", 50 + (processed * 40 / Math.Max(1, localNotes.Count)), 
                    processed, localNotes.Count + remoteFiles.Count, $"Uploading local notes...");
            }

            // Process pending changes with retry logic
            await ProcessPendingChangesAsync(result);

            result.Success = true;
            Status = SyncStatus.Idle;
            RaiseProgress("Complete", 100, processed, processed, "Sync completed successfully.");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Status = SyncStatus.Error;
        }

        LastSyncResult = result;
        return result;
    }


    /// <inheritdoc />
    public async Task<SyncResult> SyncNoteAsync(Guid noteId)
    {
        if (_cloudProvider == null || !_cloudProvider.IsAuthenticated)
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "Not connected to cloud provider"
            };
        }

        var result = new SyncResult();

        try
        {
            var localNote = _noteService.GetNoteById(noteId);
            if (localNote == null)
            {
                // Note was deleted locally, delete from cloud
                await _cloudProvider.DeleteFileAsync($"notes/{noteId}.json");
                result.Success = true;
                return result;
            }

            // Check if remote version exists
            var remoteInfo = await _cloudProvider.GetFileInfoAsync($"notes/{noteId}.json");
            
            if (remoteInfo != null)
            {
                var remoteNote = await DownloadNoteAsync(noteId);
                if (remoteNote != null)
                {
                    var hasConflict = DetectConflict(localNote, remoteNote);
                    if (hasConflict)
                    {
                        result.ConflictsDetected++;
                        var resolution = await RaiseConflictAsync(localNote, remoteNote);
                        if (resolution != SyncConflictResolution.None)
                        {
                            await ResolveConflictAsync(noteId, resolution, localNote, remoteNote);
                            result.ConflictsResolved++;
                        }
                    }
                    else if (remoteNote.SyncVersion > localNote.SyncVersion)
                    {
                        // Remote is newer, update local
                        UpdateLocalNote(remoteNote);
                        result.NotesDownloaded++;
                    }
                    else
                    {
                        // Local is newer or same, upload
                        await UploadNoteAsync(localNote);
                        result.NotesUploaded++;
                    }
                }
            }
            else
            {
                // Note doesn't exist remotely, upload it
                await UploadNoteAsync(localNote);
                result.NotesUploaded++;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Note> ResolveConflictAsync(Guid noteId, SyncConflictResolution resolution, Note localNote, Note remoteNote)
    {
        Note resolvedNote;

        switch (resolution)
        {
            case SyncConflictResolution.KeepLocal:
                resolvedNote = localNote;
                resolvedNote.SyncVersion = Math.Max(localNote.SyncVersion, remoteNote.SyncVersion) + 1;
                break;

            case SyncConflictResolution.KeepRemote:
                resolvedNote = remoteNote;
                UpdateLocalNote(remoteNote);
                break;

            case SyncConflictResolution.Merge:
                resolvedNote = MergeNotes(localNote, remoteNote);
                break;

            default:
                resolvedNote = localNote;
                break;
        }

        return Task.FromResult(resolvedNote);
    }

    /// <summary>
    /// Merges two conflicting notes with conflict markers.
    /// </summary>
    public static Note MergeNotes(Note localNote, Note remoteNote)
    {
        var mergedContent = new StringBuilder();
        mergedContent.AppendLine("<<<<<<< LOCAL");
        mergedContent.AppendLine(localNote.Content);
        mergedContent.AppendLine("=======");
        mergedContent.AppendLine(remoteNote.Content);
        mergedContent.AppendLine(">>>>>>> REMOTE");

        return new Note
        {
            Id = localNote.Id,
            Title = localNote.Title,
            Content = mergedContent.ToString(),
            Language = localNote.Language,
            IsPinned = localNote.IsPinned,
            Opacity = localNote.Opacity,
            WindowRect = localNote.WindowRect,
            CreatedDate = localNote.CreatedDate,
            ModifiedDate = DateTime.UtcNow,
            GroupId = localNote.GroupId,
            TagIds = localNote.TagIds,
            MonitorDeviceId = localNote.MonitorDeviceId,
            TemplateId = localNote.TemplateId,
            SyncVersion = Math.Max(localNote.SyncVersion, remoteNote.SyncVersion) + 1,
            LastSyncedDate = DateTime.UtcNow
        };
    }


    /// <summary>
    /// Detects if there's a conflict between local and remote versions.
    /// A conflict exists when both versions have been modified since the last sync.
    /// </summary>
    public static bool DetectConflict(Note localNote, Note remoteNote)
    {
        // If local has never been synced, no conflict
        if (localNote.LastSyncedDate == null)
            return false;

        // Conflict if both have been modified since last sync
        var localModifiedSinceSync = localNote.ModifiedDate > localNote.LastSyncedDate;
        var remoteModifiedSinceSync = remoteNote.ModifiedDate > localNote.LastSyncedDate;

        return localModifiedSinceSync && remoteModifiedSinceSync;
    }

    /// <summary>
    /// Calculates the retry delay using exponential backoff.
    /// </summary>
    public static int CalculateRetryDelay(int retryCount)
    {
        if (retryCount <= 0)
            return InitialRetryDelaySeconds;

        var delay = InitialRetryDelaySeconds * (int)Math.Pow(2, retryCount);
        return Math.Min(delay, MaxRetryDelaySeconds);
    }

    private async Task ProcessPendingChangesAsync(SyncResult result)
    {
        List<PendingSyncChange> changesToProcess;
        lock (_pendingChangesLock)
        {
            var now = DateTime.UtcNow;
            changesToProcess = _pendingChanges
                .Where(c => c.NextRetryAt == null || c.NextRetryAt <= now)
                .OrderBy(c => c.QueuedAt)
                .ToList();
        }

        foreach (var change in changesToProcess)
        {
            try
            {
                bool success;
                if (change.ChangeType == SyncChangeType.Delete)
                {
                    success = await _cloudProvider!.DeleteFileAsync($"notes/{change.NoteId}.json");
                }
                else
                {
                    var note = _noteService.GetNoteById(change.NoteId);
                    if (note != null)
                    {
                        success = await UploadNoteAsync(note);
                        if (success) result.NotesUploaded++;
                    }
                    else
                    {
                        // Note no longer exists, remove from queue
                        success = true;
                    }
                }

                if (success)
                {
                    lock (_pendingChangesLock)
                    {
                        _pendingChanges.Remove(change);
                    }
                }
                else
                {
                    HandleRetry(change);
                }
            }
            catch
            {
                HandleRetry(change);
            }
        }
    }

    private void HandleRetry(PendingSyncChange change)
    {
        change.RetryCount++;
        
        if (change.RetryCount >= MaxRetryAttempts)
        {
            // Max retries reached, remove from queue
            lock (_pendingChangesLock)
            {
                _pendingChanges.Remove(change);
            }
        }
        else
        {
            // Schedule retry with exponential backoff
            var delaySeconds = CalculateRetryDelay(change.RetryCount);
            change.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
        }
    }

    private async Task<Note?> DownloadNoteAsync(Guid noteId)
    {
        if (_cloudProvider == null) return null;

        var data = await _cloudProvider.DownloadFileAsync($"notes/{noteId}.json");
        if (data == null) return null;

        // Decrypt if encryption is enabled
        if (!string.IsNullOrEmpty(_encryptionPassphrase))
        {
            try
            {
                data = _encryptionService.Decrypt(data, _encryptionPassphrase);
            }
            catch
            {
                return null; // Decryption failed
            }
        }

        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<Note>(json, JsonOptions);
    }

    private async Task<bool> UploadNoteAsync(Note note)
    {
        if (_cloudProvider == null) return false;

        // Ensure notes folder exists
        await _cloudProvider.CreateFolderAsync("notes");

        // Increment sync version
        note.SyncVersion++;
        note.LastSyncedDate = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(note, JsonOptions);
        var data = Encoding.UTF8.GetBytes(json);

        // Encrypt if encryption is enabled
        if (!string.IsNullOrEmpty(_encryptionPassphrase))
        {
            data = _encryptionService.Encrypt(data, _encryptionPassphrase);
        }

        var result = await _cloudProvider.UploadFileAsync($"notes/{note.Id}.json", data);
        
        if (result != null)
        {
            // Update local note with new sync info
            _noteService.UpdateNote(note);
            return true;
        }

        return false;
    }

    private void UpdateLocalNote(Note remoteNote)
    {
        remoteNote.LastSyncedDate = DateTime.UtcNow;
        _noteService.UpdateNote(remoteNote);
    }

    private void RaiseProgress(string operation, int percent, int processed, int total, string? message = null)
    {
        SyncProgress?.Invoke(this, new SyncProgressEventArgs
        {
            Operation = operation,
            ProgressPercent = percent,
            ItemsProcessed = processed,
            TotalItems = total,
            Message = message
        });
    }

    private async Task<SyncConflictResolution> RaiseConflictAsync(Note localNote, Note remoteNote)
    {
        var args = new SyncConflictEventArgs
        {
            NoteId = localNote.Id,
            LocalNote = localNote,
            RemoteNote = remoteNote,
            Resolution = SyncConflictResolution.None
        };

        SyncConflict?.Invoke(this, args);

        // Wait a bit for UI to respond (in real implementation, this would be async)
        await Task.Delay(100);

        return args.Resolution;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cloudProvider?.Dispose();
        _cloudProvider = null;
    }
}
