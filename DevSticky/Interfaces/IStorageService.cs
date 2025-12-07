using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for data persistence (JSON file storage)
/// </summary>
public interface IStorageService
{
    Task<AppData> LoadAsync();
    Task SaveAsync(AppData data);
    string GetStoragePath();
}
