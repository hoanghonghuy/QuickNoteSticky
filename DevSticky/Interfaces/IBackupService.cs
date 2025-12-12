using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for automatic backup of notes
/// </summary>
public interface IBackupService : IDisposable
{
    /// <summary>
    /// Starts automatic backup timer
    /// </summary>
    void StartAutoBackup();
    
    /// <summary>
    /// Stops automatic backup timer
    /// </summary>
    void StopAutoBackup();
    
    /// <summary>
    /// Creates a backup immediately
    /// </summary>
    Task<bool> CreateBackupAsync();
    
    /// <summary>
    /// Gets list of available backups
    /// </summary>
    Task<IReadOnlyList<BackupInfo>> GetBackupsAsync();
    
    /// <summary>
    /// Restores data from a specific backup
    /// </summary>
    Task<AppData?> RestoreFromBackupAsync(string backupFileName);
    
    /// <summary>
    /// Deletes a specific backup
    /// </summary>
    Task<bool> DeleteBackupAsync(string backupFileName);
    
    /// <summary>
    /// Gets backup settings
    /// </summary>
    BackupSettings GetSettings();
    
    /// <summary>
    /// Updates backup settings
    /// </summary>
    void UpdateSettings(BackupSettings settings);
}

/// <summary>
/// Information about a backup file
/// </summary>
public class BackupInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public int NoteCount { get; set; }
}

/// <summary>
/// Backup configuration settings
/// </summary>
public class BackupSettings
{
    /// <summary>
    /// Interval between automatic backups in minutes (default: 30)
    /// </summary>
    public int IntervalMinutes { get; set; } = 30;
    
    /// <summary>
    /// Maximum number of backups to keep (default: 10)
    /// </summary>
    public int MaxBackups { get; set; } = 10;
    
    /// <summary>
    /// Whether automatic backup is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
