using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for persisting application data to JSON file with lazy loading support
/// </summary>
public class StorageService : IStorageService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptionsFactory.Default;
    private const string ContentFolderName = "content";
    private const string LazyLoadingMarkerFile = ".lazy-loading";

    private readonly string _storagePath;
    private readonly string _contentFolderPath;
    private readonly string _markerFilePath;
    private readonly IErrorHandler _errorHandler;
    private readonly IFileSystem _fileSystem;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _noteLocks = new();
    private readonly SemaphoreSlim _migrationLock = new(1, 1);
    private bool _isLazyLoadingFormat;
    private bool _disposed;

    public StorageService(IErrorHandler errorHandler, IFileSystem fileSystem)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        
        var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
        _storagePath = PathHelper.Combine(appDataPath, AppConstants.NotesFileName);
        _contentFolderPath = PathHelper.Combine(appDataPath, ContentFolderName);
        _markerFilePath = PathHelper.Combine(appDataPath, LazyLoadingMarkerFile);
        
        // Check if lazy loading format is enabled
        _isLazyLoadingFormat = _fileSystem.FileExists(_markerFilePath);
    }

    public string GetStoragePath() => _storagePath;
    public bool IsLazyLoadingFormat => _isLazyLoadingFormat;

    public async Task<AppData> LoadAsync()
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (!_fileSystem.FileExists(_storagePath))
            {
                return CreateDefaultAppData();
            }

            var json = await _fileSystem.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            var appData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
            
            if (appData == null)
            {
                throw new JsonException(L.Get("ErrorAppDataNull"));
            }
            
            return appData;
        }, 
        CreateDefaultAppData(), 
        "StorageService.LoadAsync - Loading application data from storage");
    }

    public async Task SaveAsync(AppData data)
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var directory = PathHelper.GetDirectoryName(_storagePath);
            if (!StringHelper.IsNullOrEmpty(directory))
            {
                PathHelper.EnsureDirectoryExists(directory);
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            System.Diagnostics.Debug.WriteLine($"[StorageService] Saving {data.Notes?.Count ?? 0} notes to {_storagePath}");
            System.Diagnostics.Debug.WriteLine($"[StorageService] JSON length: {json.Length} chars");
            await _fileSystem.WriteAllTextAsync(_storagePath, json).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[StorageService] Save completed successfully");
            return true;
        }, 
        false, 
        "StorageService.SaveAsync - Saving application data to storage");
    }

    public async Task SaveNotesAsync(IEnumerable<Note> notes, AppData currentData)
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            // Create a new AppData with updated notes but preserve other data
            var updatedData = new AppData
            {
                AppSettings = currentData.AppSettings,
                Notes = notes.ToList(),
                Groups = currentData.Groups,
                Tags = currentData.Tags
            };

            var directory = PathHelper.GetDirectoryName(_storagePath);
            if (!StringHelper.IsNullOrEmpty(directory))
            {
                PathHelper.EnsureDirectoryExists(directory);
            }

            var json = JsonSerializer.Serialize(updatedData, JsonOptions);
            await _fileSystem.WriteAllTextAsync(_storagePath, json).ConfigureAwait(false);
            return true;
        }, 
        false, 
        "StorageService.SaveNotesAsync - Saving notes incrementally to storage");
    }


    #region Lazy Loading Methods
    
    /// <summary>
    /// Preloads content for multiple notes in parallel (for recent notes)
    /// </summary>
    public async Task PreloadNoteContentsAsync(IEnumerable<Guid> noteIds)
    {
        if (!_isLazyLoadingFormat) return;
        
        var tasks = noteIds.Select(LoadNoteContentAsync);
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
    
    public async Task<AppData> LoadMetadataOnlyAsync()
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (!_fileSystem.FileExists(_storagePath))
            {
                return CreateDefaultAppData();
            }

            var json = await _fileSystem.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            var appData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
            
            if (appData == null)
            {
                throw new JsonException(L.Get("ErrorAppDataNull"));
            }
            
            // If using lazy loading format, mark notes as not loaded
            if (_isLazyLoadingFormat)
            {
                foreach (var note in appData.Notes)
                {
                    note.IsContentLoaded = false;
                    // Content in JSON is just the preview
                    note.ContentPreview = note.Content;
                    note.Content = string.Empty;
                }
            }
            
            return appData;
        }, 
        CreateDefaultAppData(), 
        "StorageService.LoadMetadataOnlyAsync - Loading metadata from storage");
    }
    
    public async Task<string?> LoadNoteContentAsync(Guid noteId)
    {
        var semaphore = _noteLocks.GetOrAdd(noteId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync().ConfigureAwait(false);
        
        try
        {
            var contentPath = GetContentFilePath(noteId);
            
            if (!_fileSystem.FileExists(contentPath))
            {
                System.Diagnostics.Debug.WriteLine($"[StorageService] Content file not found for note {noteId}");
                return null;
            }
            
            var content = await _fileSystem.ReadAllTextAsync(contentPath).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[StorageService] Loaded content for note {noteId}, length: {content.Length}");
            return content;
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    public async Task SaveNoteContentAsync(Guid noteId, string content)
    {
        var semaphore = _noteLocks.GetOrAdd(noteId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync().ConfigureAwait(false);
        
        try
        {
            PathHelper.EnsureDirectoryExists(_contentFolderPath);
            var contentPath = GetContentFilePath(noteId);
            await _fileSystem.WriteAllTextAsync(contentPath, content).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[StorageService] Saved content for note {noteId}, length: {content.Length}");
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    public async Task DeleteNoteContentAsync(Guid noteId)
    {
        var semaphore = _noteLocks.GetOrAdd(noteId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync().ConfigureAwait(false);
        
        try
        {
            var contentPath = GetContentFilePath(noteId);
            if (_fileSystem.FileExists(contentPath))
            {
                await _fileSystem.DeleteFileAsync(contentPath).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[StorageService] Deleted content for note {noteId}");
            }
        }
        finally
        {
            semaphore.Release();
            _noteLocks.TryRemove(noteId, out _);
        }
    }
    
    public async Task<bool> MigrateToLazyLoadingFormatAsync()
    {
        // Prevent concurrent migrations
        await _migrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await _errorHandler.HandleWithFallbackAsync(async () =>
            {
                if (_isLazyLoadingFormat)
                {
                    System.Diagnostics.Debug.WriteLine("[StorageService] Already using lazy loading format");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine("[StorageService] Starting migration to lazy loading format...");
                
                // Load all data in old format
                var appData = await LoadAsync().ConfigureAwait(false);
                
                // Create content folder
                PathHelper.EnsureDirectoryExists(_contentFolderPath);
                
                // Save each note's content to separate file (parallel for better performance)
                var tasks = appData.Notes
                    .Where(n => !string.IsNullOrEmpty(n.Content))
                    .Select(async note =>
                    {
                        await SaveNoteContentAsync(note.Id, note.Content).ConfigureAwait(false);
                        note.UpdateContentPreview();
                        note.Content = note.ContentPreview; // Store preview in main JSON
                    });
                
                await Task.WhenAll(tasks).ConfigureAwait(false);
                
                // Save updated metadata
                await SaveAsync(appData).ConfigureAwait(false);
                
                // Create marker file
                await _fileSystem.WriteAllTextAsync(_markerFilePath, DateTime.UtcNow.ToString("O")).ConfigureAwait(false);
                _isLazyLoadingFormat = true;
                
                System.Diagnostics.Debug.WriteLine($"[StorageService] Migration complete. Migrated {appData.Notes.Count} notes.");
                return true;
            }, 
            false, 
            "StorageService.MigrateToLazyLoadingFormatAsync - Migrating to lazy loading format");
        }
        finally
        {
            _migrationLock.Release();
        }
    }
    
    private string GetContentFilePath(Guid noteId)
    {
        return PathHelper.Combine(_contentFolderPath, $"{noteId}.txt");
    }
    
    #endregion

    private static AppData CreateDefaultAppData()
    {
        var now = DateTime.UtcNow;
        return new AppData
        {
            AppSettings = new AppSettings(),
            Notes = new List<Note>
            {
                new Note
                {
                    Title = L.Get("WelcomeTitle"),
                    Content = L.Get("WelcomeContent"),
                    Language = "CSharp",
                    IsPinned = true,
                    Opacity = 0.9,
                    WindowRect = new WindowRect
                    {
                        Top = 100,
                        Left = 100,
                        Width = 300,
                        Height = 200
                    },
                    CreatedDate = now,
                    ModifiedDate = now
                }
            }
        };
    }

    private async Task BackupCorruptedFileAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (_fileSystem.FileExists(_storagePath))
            {
                var backupPath = _storagePath + $".backup.{DateTime.Now:yyyyMMddHHmmss}";
                await _fileSystem.MoveFileAsync(_storagePath, backupPath).ConfigureAwait(false);
            }
            return true;
        }, 
        false, 
        "StorageService.BackupCorruptedFileAsync - Backing up corrupted storage file").ConfigureAwait(false);
    }

    private void BackupCorruptedFile()
    {
        _errorHandler.HandleWithFallback(() =>
        {
            if (_fileSystem.FileExists(_storagePath))
            {
                var backupPath = _storagePath + $".backup.{DateTime.Now:yyyyMMddHHmmss}";
                _fileSystem.MoveFile(_storagePath, backupPath);
            }
            return true;
        }, 
        false, 
        "StorageService.BackupCorruptedFile - Backing up corrupted storage file");
    }

    /// <summary>
    /// Disposes the storage service and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            // Dispose all semaphores
            _migrationLock.Dispose();
            foreach (var kvp in _noteLocks)
            {
                kvp.Value.Dispose();
            }
            _noteLocks.Clear();
        }
        
        _disposed = true;
    }
}
