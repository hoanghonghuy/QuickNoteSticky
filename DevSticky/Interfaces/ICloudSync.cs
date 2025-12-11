using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for cloud synchronization operations.
/// Handles syncing notes to and from cloud storage.
/// </summary>
public interface ICloudSync
{
    /// <summary>
    /// Event raised when sync progress changes.
    /// </summary>
    event EventHandler<SyncProgressEventArgs>? SyncProgress;

    /// <summary>
    /// Gets the last sync result, or null if no sync has been performed.
    /// </summary>
    SyncResult? LastSyncResult { get; }

    /// <summary>
    /// Gets the pending sync queue.
    /// </summary>
    IReadOnlyList<PendingSyncChange> PendingChanges { get; }

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
}
