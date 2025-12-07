using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Event arguments for sync progress updates.
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    /// <summary>
    /// The current sync operation being performed.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// The current progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// The number of items processed.
    /// </summary>
    public int ItemsProcessed { get; set; }

    /// <summary>
    /// The total number of items to process.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Optional message describing the current operation.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Event arguments for sync conflicts.
/// </summary>
public class SyncConflictEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the note with a conflict.
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// The local version of the note.
    /// </summary>
    public Note LocalNote { get; set; } = null!;

    /// <summary>
    /// The remote version of the note.
    /// </summary>
    public Note RemoteNote { get; set; } = null!;

    /// <summary>
    /// The resolution chosen by the user.
    /// </summary>
    public SyncConflictResolution Resolution { get; set; } = SyncConflictResolution.None;
}


/// <summary>
/// Sync conflict resolution options.
/// </summary>
public enum SyncConflictResolution
{
    /// <summary>
    /// No resolution chosen yet.
    /// </summary>
    None,

    /// <summary>
    /// Keep the local version.
    /// </summary>
    KeepLocal,

    /// <summary>
    /// Keep the remote version.
    /// </summary>
    KeepRemote,

    /// <summary>
    /// Merge both versions with conflict markers.
    /// </summary>
    Merge
}

/// <summary>
/// Sync status enumeration.
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Not connected to any cloud provider.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Currently connecting to cloud provider.
    /// </summary>
    Connecting,

    /// <summary>
    /// Currently syncing data.
    /// </summary>
    Syncing,

    /// <summary>
    /// Connected and idle.
    /// </summary>
    Idle,

    /// <summary>
    /// An error occurred during sync.
    /// </summary>
    Error
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public class SyncResult
{
    /// <summary>
    /// Whether the sync was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of notes uploaded.
    /// </summary>
    public int NotesUploaded { get; set; }

    /// <summary>
    /// Number of notes downloaded.
    /// </summary>
    public int NotesDownloaded { get; set; }

    /// <summary>
    /// Number of conflicts detected.
    /// </summary>
    public int ConflictsDetected { get; set; }

    /// <summary>
    /// Number of conflicts resolved.
    /// </summary>
    public int ConflictsResolved { get; set; }

    /// <summary>
    /// Error message if sync failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The time the sync completed.
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a pending sync change.
/// </summary>
public class PendingSyncChange
{
    /// <summary>
    /// The ID of the note to sync.
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// The type of change.
    /// </summary>
    public SyncChangeType ChangeType { get; set; }

    /// <summary>
    /// When the change was queued.
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Next retry time (for exponential backoff).
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
}

/// <summary>
/// Type of sync change.
/// </summary>
public enum SyncChangeType
{
    /// <summary>
    /// Note was created or modified.
    /// </summary>
    CreateOrUpdate,

    /// <summary>
    /// Note was deleted.
    /// </summary>
    Delete
}

/// <summary>
/// Service for synchronizing notes with cloud storage providers.
/// Handles conflict detection, resolution, and retry logic.
/// </summary>
public interface ICloudSyncService : IDisposable
{
    /// <summary>
    /// Event raised when sync progress changes.
    /// </summary>
    event EventHandler<SyncProgressEventArgs>? SyncProgress;

    /// <summary>
    /// Event raised when a sync conflict is detected.
    /// </summary>
    event EventHandler<SyncConflictEventArgs>? SyncConflict;

    /// <summary>
    /// Gets the currently connected cloud provider, or null if not connected.
    /// </summary>
    CloudProvider? CurrentProvider { get; }

    /// <summary>
    /// Gets the current sync status.
    /// </summary>
    SyncStatus Status { get; }

    /// <summary>
    /// Gets the last sync result, or null if no sync has been performed.
    /// </summary>
    SyncResult? LastSyncResult { get; }

    /// <summary>
    /// Gets the pending sync queue.
    /// </summary>
    IReadOnlyList<PendingSyncChange> PendingChanges { get; }

    /// <summary>
    /// Connects to a cloud provider and authenticates.
    /// </summary>
    /// <param name="provider">The cloud provider to connect to.</param>
    /// <returns>True if connection was successful, false otherwise.</returns>
    Task<bool> ConnectAsync(CloudProvider provider);

    /// <summary>
    /// Disconnects from the current cloud provider.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Performs a full sync with the cloud provider.
    /// </summary>
    /// <returns>The result of the sync operation.</returns>
    Task<SyncResult> SyncAsync();

    /// <summary>
    /// Syncs a specific note with the cloud provider.
    /// </summary>
    /// <param name="noteId">The ID of the note to sync.</param>
    /// <returns>The result of the sync operation.</returns>
    Task<SyncResult> SyncNoteAsync(Guid noteId);

    /// <summary>
    /// Queues a note for sync. The note will be synced within the configured interval.
    /// </summary>
    /// <param name="noteId">The ID of the note to queue.</param>
    /// <param name="changeType">The type of change (create/update or delete).</param>
    void QueueNoteForSync(Guid noteId, SyncChangeType changeType = SyncChangeType.CreateOrUpdate);

    /// <summary>
    /// Sets the encryption passphrase for cloud sync.
    /// </summary>
    /// <param name="passphrase">The passphrase to use for encryption.</param>
    void SetEncryptionPassphrase(string passphrase);

    /// <summary>
    /// Resolves a sync conflict with the specified resolution.
    /// </summary>
    /// <param name="noteId">The ID of the conflicting note.</param>
    /// <param name="resolution">The resolution to apply.</param>
    /// <param name="localNote">The local version of the note.</param>
    /// <param name="remoteNote">The remote version of the note.</param>
    /// <returns>The resolved note.</returns>
    Task<Note> ResolveConflictAsync(Guid noteId, SyncConflictResolution resolution, Note localNote, Note remoteNote);
}
