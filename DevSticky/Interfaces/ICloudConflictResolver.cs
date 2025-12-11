using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for handling sync conflicts between local and remote notes.
/// </summary>
public interface ICloudConflictResolver
{
    /// <summary>
    /// Event raised when a sync conflict is detected.
    /// </summary>
    event EventHandler<SyncConflictEventArgs>? SyncConflict;

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
