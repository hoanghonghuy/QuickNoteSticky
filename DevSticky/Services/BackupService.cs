using System.IO;
using System.Text.Json;
using System.Timers;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for automatic backup of notes to local storage
/// </summary>
public class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptionsFactory.Default;
    
    private readonly string _backupFolder;
    private readonly IStorageService _storageService;
    private readonly IErrorHandler _errorHandler;
    private readonly System.Timers.Timer _backupTimer;
    
    private BackupSettings _settings = new();
    private bool _disposed;

    public BackupService(IStorageService storageService, IErrorHandler errorHandler)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        
        _backupFolder = PathHelper.Combine(
            PathHelper.GetAppDataPath(AppConstants.AppDataFolderName),
            "backups");
        
        PathHelper.EnsureDirectoryExists(_backupFolder);
        
        _backupTimer = new System.Timers.Timer();
        _backupTimer.Elapsed += OnBackupTimerElapsed;
        
        LoadSettings();
    }

    public void StartAutoBackup()
    {
        if (!_settings.IsEnabled || _disposed) return;
        
        _backupTimer.Interval = _settings.IntervalMinutes * 60 * 1000;
        _backupTimer.Start();
    }

    public void StopAutoBackup()
    {
        _backupTimer.Stop();
    }

    public async Task<bool> CreateBackupAsync()
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var appData = await _storageService.LoadAsync();
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"backup_{timestamp}.json";
            var filePath = PathHelper.Combine(_backupFolder, fileName);
            
            var json = JsonSerializer.Serialize(appData, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            // Clean up old backups
            await CleanupOldBackupsAsync();
            
            return true;
        }, false, "BackupService.CreateBackupAsync");
    }

    public async Task<IReadOnlyList<BackupInfo>> GetBackupsAsync()
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var backups = new List<BackupInfo>();
            
            if (!Directory.Exists(_backupFolder))
                return backups;
            
            var files = Directory.GetFiles(_backupFolder, "backup_*.json")
                .OrderByDescending(f => f);
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var noteCount = 0;
                
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var appData = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                    noteCount = appData?.Notes?.Count ?? 0;
                }
                catch { /* Ignore parse errors */ }
                
                backups.Add(new BackupInfo
                {
                    FileName = fileInfo.Name,
                    FullPath = file,
                    CreatedAt = fileInfo.CreationTime,
                    SizeBytes = fileInfo.Length,
                    NoteCount = noteCount
                });
            }
            
            return backups;
        }, new List<BackupInfo>(), "BackupService.GetBackupsAsync");
    }

    public async Task<AppData?> RestoreFromBackupAsync(string backupFileName)
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var filePath = PathHelper.Combine(_backupFolder, backupFileName);
            
            if (!File.Exists(filePath))
                return null;
            
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions);
        }, (AppData?)null, "BackupService.RestoreFromBackupAsync");
    }

    public async Task<bool> DeleteBackupAsync(string backupFileName)
    {
        return await _errorHandler.HandleWithFallbackAsync(() =>
        {
            var filePath = PathHelper.Combine(_backupFolder, backupFileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }, false, "BackupService.DeleteBackupAsync");
    }

    public BackupSettings GetSettings() => _settings;

    public void UpdateSettings(BackupSettings settings)
    {
        _settings = settings ?? new BackupSettings();
        SaveSettings();
        
        // Restart timer with new interval
        StopAutoBackup();
        if (_settings.IsEnabled)
        {
            StartAutoBackup();
        }
    }

    private async void OnBackupTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await CreateBackupAsync();
    }

    private async Task CleanupOldBackupsAsync()
    {
        await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            var backups = await GetBackupsAsync();
            
            if (backups.Count <= _settings.MaxBackups)
                return true;
            
            // Delete oldest backups
            var toDelete = backups
                .OrderBy(b => b.CreatedAt)
                .Take(backups.Count - _settings.MaxBackups);
            
            foreach (var backup in toDelete)
            {
                await DeleteBackupAsync(backup.FileName);
            }
            
            return true;
        }, false, "BackupService.CleanupOldBackupsAsync");
    }

    private void LoadSettings()
    {
        _errorHandler.HandleWithFallback(() =>
        {
            var settingsPath = PathHelper.Combine(_backupFolder, "backup_settings.json");
            
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                _settings = JsonSerializer.Deserialize<BackupSettings>(json, JsonOptions) ?? new BackupSettings();
            }
            
            return true;
        }, false, "BackupService.LoadSettings");
    }

    private void SaveSettings()
    {
        _errorHandler.HandleWithFallback(() =>
        {
            var settingsPath = PathHelper.Combine(_backupFolder, "backup_settings.json");
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(settingsPath, json);
            return true;
        }, false, "BackupService.SaveSettings");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            _backupTimer.Stop();
            _backupTimer.Dispose();
        }
        
        _disposed = true;
    }
}
