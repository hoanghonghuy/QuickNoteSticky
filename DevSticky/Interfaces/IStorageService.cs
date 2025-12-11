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
}
