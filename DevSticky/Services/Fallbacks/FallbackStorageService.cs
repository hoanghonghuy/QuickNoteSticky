using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services.Fallbacks;

/// <summary>
/// Fallback implementation of IStorageService with in-memory storage
/// Used when the primary StorageService fails to initialize or file system is unavailable
/// </summary>
public class FallbackStorageService : IStorageService
{
    private readonly IErrorHandler _errorHandler;
    private AppData? _inMemoryData;

    public FallbackStorageService(IErrorHandler errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public string GetStoragePath()
    {
        return "[In-Memory Storage - No File Path]";
    }

    public async Task<AppData> LoadAsync()
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            await Task.CompletedTask; // Make method async for consistency
            
            // Return cached data or create default
            if (_inMemoryData != null)
            {
                return _inMemoryData;
            }

            return CreateDefaultAppData();
        },
        CreateDefaultAppData(),
        "FallbackStorageService.LoadAsync");
    }

    public async Task SaveAsync(AppData data)
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            await Task.CompletedTask; // Make method async for consistency
            
            // Store in memory only
            _inMemoryData = data;
            return true;
        },
        false,
        "FallbackStorageService.SaveAsync");
    }

    public async Task SaveNotesAsync(IEnumerable<Note> notes, AppData currentData)
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            await Task.CompletedTask; // Make method async for consistency
            
            // Update in-memory data
            if (_inMemoryData != null)
            {
                _inMemoryData.Notes = notes.ToList();
            }
            else
            {
                _inMemoryData = new AppData
                {
                    AppSettings = currentData.AppSettings,
                    Notes = notes.ToList(),
                    Groups = currentData.Groups,
                    Tags = currentData.Tags
                };
            }
            return true;
        },
        false,
        "FallbackStorageService.SaveNotesAsync");
    }
    
    #region Lazy Loading (Not supported in fallback mode)
    
    public bool IsLazyLoadingFormat => false;
    
    public Task<AppData> LoadMetadataOnlyAsync() => LoadAsync();
    
    public Task<string?> LoadNoteContentAsync(Guid noteId)
    {
        var note = _inMemoryData?.Notes.FirstOrDefault(n => n.Id == noteId);
        return Task.FromResult(note?.Content);
    }
    
    public Task SaveNoteContentAsync(Guid noteId, string content)
    {
        var note = _inMemoryData?.Notes.FirstOrDefault(n => n.Id == noteId);
        if (note != null) note.Content = content;
        return Task.CompletedTask;
    }
    
    public Task DeleteNoteContentAsync(Guid noteId) => Task.CompletedTask;
    
    public Task<bool> MigrateToLazyLoadingFormatAsync() => Task.FromResult(false);
    
    public Task PreloadNoteContentsAsync(IEnumerable<Guid> noteIds) => Task.CompletedTask;
    
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
                    Id = Guid.NewGuid(),
                    Title = "Fallback Mode Note",
                    Content = "DevSticky is running in fallback mode. Data is stored in memory only and will be lost when the application closes.",
                    Language = "PlainText",
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
            },
            Groups = new List<NoteGroup>(),
            Tags = new List<NoteTag>()
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inMemoryData = null;
        }
    }
}