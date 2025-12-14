using System.IO;
using System.Text.Json;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for persisting application data to JSON file
/// </summary>
public class StorageService : IStorageService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptionsFactory.Default;

    private readonly string _storagePath;
    private readonly IErrorHandler _errorHandler;
    private readonly IFileSystem _fileSystem;

    public StorageService(IErrorHandler errorHandler, IFileSystem fileSystem)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _storagePath = PathHelper.Combine(
            PathHelper.GetAppDataPath(AppConstants.AppDataFolderName),
            AppConstants.NotesFileName);
    }

    public string GetStoragePath() => _storagePath;

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
        if (disposing)
        {
            // No managed resources to dispose in this service
            // This implementation is for consistency and future-proofing
        }
    }
}
