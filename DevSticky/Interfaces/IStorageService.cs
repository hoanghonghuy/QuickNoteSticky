using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for data persistence (JSON file storage)
/// </summary>
public interface IStorageService : IDisposable
{
    Task<AppData> LoadAsync();
    Task SaveAsync(AppData data);
    Task SaveNotesAsync(IEnumerable<Note> notes, AppData currentData);
    string GetStoragePath();
    
    // Lazy loading support
    /// <summary>
    /// Loads only metadata for all notes (content not loaded)
    /// </summary>
    Task<AppData> LoadMetadataOnlyAsync();
    
    /// <summary>
    /// Loads content for a specific note
    /// </summary>
    Task<string?> LoadNoteContentAsync(Guid noteId);
    
    /// <summary>
    /// Saves content for a specific note to separate file
    /// </summary>
    Task SaveNoteContentAsync(Guid noteId, string content);
    
    /// <summary>
    /// Deletes content file for a note
    /// </summary>
    Task DeleteNoteContentAsync(Guid noteId);
    
    /// <summary>
    /// Migrates from old format (all-in-one) to new format (separate content files)
    /// </summary>
    Task<bool> MigrateToLazyLoadingFormatAsync();
    
    /// <summary>
    /// Checks if storage is using lazy loading format
    /// </summary>
    bool IsLazyLoadingFormat { get; }
    
    /// <summary>
    /// Preloads content for multiple notes in parallel
    /// </summary>
    Task PreloadNoteContentsAsync(IEnumerable<Guid> noteIds);
}
