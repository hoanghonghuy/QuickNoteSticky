using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Recovery action types for different failure scenarios
/// </summary>
public enum RecoveryAction
{
    CreateMissingDirectories,
    CreateDefaultConfiguration,
    BackupCorruptedConfiguration,
    RestoreFromBackup,
    ResetToDefaults,
    RepairFilePermissions,
    ValidateAndRepairData
}

/// <summary>
/// Result of a recovery operation
/// </summary>
public class RecoveryResult
{
    public bool IsSuccessful { get; set; }
    public RecoveryAction Action { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service for automatic recovery from common startup and configuration issues
/// </summary>
public interface IRecoveryManager
{
    /// <summary>
    /// Attempt to recover from missing configuration files by creating defaults
    /// </summary>
    /// <param name="configPath">Path to the missing configuration file</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverMissingConfigurationAsync(string configPath);

    /// <summary>
    /// Attempt to recover from corrupted configuration by backing up and recreating
    /// </summary>
    /// <param name="configPath">Path to the corrupted configuration file</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverCorruptedConfigurationAsync(string configPath);

    /// <summary>
    /// Attempt to recover from missing directories by creating the required structure
    /// </summary>
    /// <param name="directoryPath">Path to the missing directory</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverMissingDirectoriesAsync(string directoryPath);

    /// <summary>
    /// Perform comprehensive recovery for multiple issues
    /// </summary>
    /// <returns>List of recovery results for all attempted actions</returns>
    Task<List<RecoveryResult>> PerformComprehensiveRecoveryAsync();

    /// <summary>
    /// Validate application structure and identify issues that need recovery
    /// </summary>
    /// <returns>List of recovery actions that should be performed</returns>
    Task<List<RecoveryAction>> IdentifyRequiredRecoveryActionsAsync();

    /// <summary>
    /// Attempt to recover from service initialization failures using fallback services
    /// </summary>
    /// <param name="fallbackManager">Service fallback manager to use for recovery</param>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> RecoverServiceFailuresAsync(IServiceFallbackManager fallbackManager);

    /// <summary>
    /// Reset all configuration to factory defaults
    /// </summary>
    /// <returns>Recovery result indicating success or failure</returns>
    Task<RecoveryResult> ResetToFactoryDefaultsAsync();

    /// <summary>
    /// Create a backup of current configuration before performing recovery
    /// </summary>
    /// <param name="configPath">Path to configuration file to backup</param>
    /// <returns>Path to the backup file if successful, null otherwise</returns>
    Task<string?> CreateConfigurationBackupAsync(string configPath);
}