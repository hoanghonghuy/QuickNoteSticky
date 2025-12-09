using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for persisting application data to JSON file
/// </summary>
public class StorageService : IStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _storagePath;

    public StorageService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var devStickyFolder = Path.Combine(appDataPath, AppConstants.AppDataFolderName);
        _storagePath = Path.Combine(devStickyFolder, AppConstants.NotesFileName);
    }

    public string GetStoragePath() => _storagePath;

    public async Task<AppData> LoadAsync()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return CreateDefaultAppData();
            }

            var json = await File.ReadAllTextAsync(_storagePath);
            var appData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
            
            return appData ?? CreateDefaultAppData();
        }
        catch (JsonException)
        {
            // Backup corrupted file and create new
            BackupCorruptedFile();
            return CreateDefaultAppData();
        }
    }

    public async Task SaveAsync(AppData data)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_storagePath, json);
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

    private void BackupCorruptedFile()
    {
        if (File.Exists(_storagePath))
        {
            var backupPath = _storagePath + $".backup.{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(_storagePath, backupPath);
        }
    }
}
