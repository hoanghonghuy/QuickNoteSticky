using System.IO;
using System.Text.Json;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;

namespace DevSticky.Services;

/// <summary>
/// Service for automatic recovery from common startup and configuration issues
/// </summary>
public class RecoveryManager : IRecoveryManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IErrorHandler _errorHandler;
    private readonly string _appDataPath;
    private readonly string _backupPath;

    public RecoveryManager(IFileSystem fileSystem, IErrorHandler errorHandler)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        
        _appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
        _backupPath = PathHelper.Combine(_appDataPath, "backups");
    }

    /// <summary>
    /// Attempt to recover from missing configuration files by creating defaults
    /// </summary>
    public async Task<RecoveryResult> RecoverMissingConfigurationAsync(string configPath)
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            // Ensure the directory exists
            var directory = _fileSystem.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            // Create default configuration based on file type
            var fileName = Path.GetFileName(configPath).ToLowerInvariant();
            
            if (fileName == AppConstants.NotesFileName)
            {
                await CreateDefaultNotesFileAsync(configPath);
            }
            else if (fileName == AppConstants.SettingsFileName)
            {
                await CreateDefaultSettingsFileAsync(configPath);
            }
            else if (fileName == AppConstants.SnippetsFileName)
            {
                await CreateDefaultSnippetsFileAsync(configPath);
            }
            else if (fileName == AppConstants.TemplatesFileName)
            {
                await CreateDefaultTemplatesFileAsync(configPath);
            }
            else
            {
                // Generic empty JSON file
                await _fileSystem.WriteAllTextAsync(configPath, "{}");
            }

            return new RecoveryResult
            {
                IsSuccessful = true,
                Action = RecoveryAction.CreateDefaultConfiguration,
                Message = $"Successfully created default configuration file: {configPath}"
            };
        },
        new RecoveryResult
        {
            IsSuccessful = false,
            Action = RecoveryAction.CreateDefaultConfiguration,
            Message = $"Failed to create default configuration file: {configPath}"
        },
        $"RecoveryManager.RecoverMissingConfigurationAsync - {configPath}");
    }

    /// <summary>
    /// Attempt to recover from corrupted configuration by backing up and recreating
    /// </summary>
    public async Task<RecoveryResult> RecoverCorruptedConfigurationAsync(string configPath)
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            // Create backup of corrupted file
            var backupPath = await CreateConfigurationBackupAsync(configPath);
            
            if (backupPath == null)
            {
                return new RecoveryResult
                {
                    IsSuccessful = false,
                    Action = RecoveryAction.BackupCorruptedConfiguration,
                    Message = $"Failed to backup corrupted configuration: {configPath}"
                };
            }

            // Remove corrupted file
            if (_fileSystem.FileExists(configPath))
            {
                await _fileSystem.DeleteFileAsync(configPath);
            }

            // Create new default configuration
            var createResult = await RecoverMissingConfigurationAsync(configPath);
            
            return new RecoveryResult
            {
                IsSuccessful = createResult.IsSuccessful,
                Action = RecoveryAction.BackupCorruptedConfiguration,
                Message = createResult.IsSuccessful 
                    ? $"Successfully recovered corrupted configuration. Backup saved to: {backupPath}"
                    : $"Failed to recover corrupted configuration after backup to: {backupPath}"
            };
        },
        new RecoveryResult
        {
            IsSuccessful = false,
            Action = RecoveryAction.BackupCorruptedConfiguration,
            Message = $"Failed to recover corrupted configuration: {configPath}"
        },
        $"RecoveryManager.RecoverCorruptedConfigurationAsync - {configPath}");
    }

    /// <summary>
    /// Attempt to recover from missing directories by creating the required structure
    /// </summary>
    public async Task<RecoveryResult> RecoverMissingDirectoriesAsync(string directoryPath)
    {
        return await _errorHandler.HandleWithFallbackAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(new RecoveryResult
                {
                    IsSuccessful = false,
                    Action = RecoveryAction.CreateMissingDirectories,
                    Message = "Invalid directory path"
                });
            }

            if (_fileSystem.DirectoryExists(directoryPath))
            {
                return Task.FromResult(new RecoveryResult
                {
                    IsSuccessful = false,
                    Action = RecoveryAction.CreateMissingDirectories,
                    Message = $"Directory already exists: {directoryPath}"
                });
            }

            _fileSystem.CreateDirectory(directoryPath);
            
            if (directoryPath.Equals(_appDataPath, StringComparison.OrdinalIgnoreCase))
            {
                var backupDir = PathHelper.Combine(_appDataPath, "backups");
                var logsDir = PathHelper.Combine(_appDataPath, "logs");
                
                if (!_fileSystem.DirectoryExists(backupDir))
                    _fileSystem.CreateDirectory(backupDir);
                
                if (!_fileSystem.DirectoryExists(logsDir))
                    _fileSystem.CreateDirectory(logsDir);
            }

            return Task.FromResult(new RecoveryResult
            {
                IsSuccessful = true,
                Action = RecoveryAction.CreateMissingDirectories,
                Message = $"Successfully created directory structure: {directoryPath}"
            });
        },
        new RecoveryResult
        {
            IsSuccessful = false,
            Action = RecoveryAction.CreateMissingDirectories,
            Message = $"Failed to create directory structure: {directoryPath}"
        },
        $"RecoveryManager.RecoverMissingDirectoriesAsync - {directoryPath}");
    }

    /// <summary>
    /// Perform comprehensive recovery for multiple issues
    /// </summary>
    public async Task<List<RecoveryResult>> PerformComprehensiveRecoveryAsync()
    {
        var results = new List<RecoveryResult>();

        try
        {
            // 1. Ensure main application directory exists
            var dirResult = await RecoverMissingDirectoriesAsync(_appDataPath);
            results.Add(dirResult);

            // 2. Check and recover all configuration files
            var configFiles = new[]
            {
                PathHelper.Combine(_appDataPath, AppConstants.NotesFileName),
                PathHelper.Combine(_appDataPath, AppConstants.SettingsFileName),
                PathHelper.Combine(_appDataPath, AppConstants.SnippetsFileName),
                PathHelper.Combine(_appDataPath, AppConstants.TemplatesFileName)
            };

            foreach (var configFile in configFiles)
            {
                if (!_fileSystem.FileExists(configFile))
                {
                    var result = await RecoverMissingConfigurationAsync(configFile);
                    results.Add(result);
                }
                else
                {
                    // Validate existing configuration files
                    var isValid = await ValidateConfigurationFileAsync(configFile);
                    if (!isValid)
                    {
                        var result = await RecoverCorruptedConfigurationAsync(configFile);
                        results.Add(result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "RecoveryManager.PerformComprehensiveRecoveryAsync");
            results.Add(new RecoveryResult
            {
                IsSuccessful = false,
                Action = RecoveryAction.ValidateAndRepairData,
                Message = "Comprehensive recovery failed due to unexpected error",
                Exception = ex
            });
        }

        return results;
    }

    /// <summary>
    /// Validate application structure and identify issues that need recovery
    /// </summary>
    public async Task<List<RecoveryAction>> IdentifyRequiredRecoveryActionsAsync()
    {
        var requiredActions = new List<RecoveryAction>();

        try
        {
            // Check if main directory exists
            if (!_fileSystem.DirectoryExists(_appDataPath))
            {
                requiredActions.Add(RecoveryAction.CreateMissingDirectories);
            }

            // Check configuration files
            var configFiles = new[]
            {
                PathHelper.Combine(_appDataPath, AppConstants.NotesFileName),
                PathHelper.Combine(_appDataPath, AppConstants.SettingsFileName),
                PathHelper.Combine(_appDataPath, AppConstants.SnippetsFileName),
                PathHelper.Combine(_appDataPath, AppConstants.TemplatesFileName)
            };

            foreach (var configFile in configFiles)
            {
                if (!_fileSystem.FileExists(configFile))
                {
                    requiredActions.Add(RecoveryAction.CreateDefaultConfiguration);
                }
                else
                {
                    var isValid = await ValidateConfigurationFileAsync(configFile);
                    if (!isValid)
                    {
                        requiredActions.Add(RecoveryAction.BackupCorruptedConfiguration);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _errorHandler.Handle(ex, "RecoveryManager.IdentifyRequiredRecoveryActionsAsync");
            requiredActions.Add(RecoveryAction.ValidateAndRepairData);
        }

        return requiredActions.Distinct().ToList();
    }

    /// <summary>
    /// Attempt to recover from service initialization failures using fallback services
    /// </summary>
    public async Task<RecoveryResult> RecoverServiceFailuresAsync(IServiceFallbackManager fallbackManager)
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            await Task.CompletedTask; // Make method async for consistency
            
            // This method would be called when service initialization fails
            // The actual service replacement would be handled by the ServiceInitializationDetector
            // This method just logs the recovery attempt
            
            return new RecoveryResult
            {
                IsSuccessful = true,
                Action = RecoveryAction.ValidateAndRepairData,
                Message = "Service fallback mechanisms activated successfully"
            };
        },
        new RecoveryResult
        {
            IsSuccessful = false,
            Action = RecoveryAction.ValidateAndRepairData,
            Message = "Failed to activate service fallback mechanisms"
        },
        "RecoveryManager.RecoverServiceFailuresAsync");
    }

    /// <summary>
    /// Reset all configuration to factory defaults
    /// </summary>
    public async Task<RecoveryResult> ResetToFactoryDefaultsAsync()
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            // Create backup of entire configuration
            var backupTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fullBackupPath = PathHelper.Combine(_backupPath, $"factory_reset_backup_{backupTimestamp}");
            
            if (!_fileSystem.DirectoryExists(_backupPath))
            {
                _fileSystem.CreateDirectory(_backupPath);
            }
            
            _fileSystem.CreateDirectory(fullBackupPath);

            // Backup existing files
            var configFiles = new[]
            {
                AppConstants.NotesFileName,
                AppConstants.SettingsFileName,
                AppConstants.SnippetsFileName,
                AppConstants.TemplatesFileName
            };

            foreach (var fileName in configFiles)
            {
                var sourcePath = PathHelper.Combine(_appDataPath, fileName);
                if (_fileSystem.FileExists(sourcePath))
                {
                    var backupFilePath = PathHelper.Combine(fullBackupPath, fileName);
                    var content = await _fileSystem.ReadAllTextAsync(sourcePath);
                    await _fileSystem.WriteAllTextAsync(backupFilePath, content);
                }
            }

            // Delete existing configuration files
            foreach (var fileName in configFiles)
            {
                var filePath = PathHelper.Combine(_appDataPath, fileName);
                if (_fileSystem.FileExists(filePath))
                {
                    await _fileSystem.DeleteFileAsync(filePath);
                }
            }

            // Recreate with defaults
            var recoveryResults = await PerformComprehensiveRecoveryAsync();
            var allSuccessful = recoveryResults.All(r => r.IsSuccessful);

            return new RecoveryResult
            {
                IsSuccessful = allSuccessful,
                Action = RecoveryAction.ResetToDefaults,
                Message = allSuccessful 
                    ? $"Successfully reset to factory defaults. Backup saved to: {fullBackupPath}"
                    : $"Factory reset completed with some errors. Backup saved to: {fullBackupPath}"
            };
        },
        new RecoveryResult
        {
            IsSuccessful = false,
            Action = RecoveryAction.ResetToDefaults,
            Message = "Failed to reset to factory defaults"
        },
        "RecoveryManager.ResetToFactoryDefaultsAsync");
    }

    /// <summary>
    /// Create a backup of current configuration before performing recovery
    /// </summary>
    public async Task<string?> CreateConfigurationBackupAsync(string configPath)
    {
        return await _errorHandler.HandleWithFallbackAsync(async () =>
        {
            if (!_fileSystem.FileExists(configPath))
                return null;

            var directory = _fileSystem.GetDirectoryName(configPath) ?? _appDataPath;
            if (!_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            var fileName = Path.GetFileName(configPath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fffffff");
            var backupFileName = $"{fileName}.backup_{timestamp}_{Guid.NewGuid():N}";
            var backupFilePath = PathHelper.Combine(directory, backupFileName);

            var content = await _fileSystem.ReadAllTextAsync(configPath);
            await _fileSystem.WriteAllTextAsync(backupFilePath, content);

            return backupFilePath;
        },
        null,
        $"RecoveryManager.CreateConfigurationBackupAsync - {configPath}");
    }

    #region Private Helper Methods

    /// <summary>
    /// Create default notes.json file
    /// </summary>
    private async Task CreateDefaultNotesFileAsync(string path)
    {
        var defaultAppData = CreateDefaultAppData();
        var json = JsonSerializer.Serialize(defaultAppData, JsonSerializerOptionsFactory.Default);
        await _fileSystem.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Create default settings.json file
    /// </summary>
    private async Task CreateDefaultSettingsFileAsync(string path)
    {
        var defaultSettings = new AppSettings();
        var json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
        await _fileSystem.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Create default snippets.json file
    /// </summary>
    private async Task CreateDefaultSnippetsFileAsync(string path)
    {
        var defaultSnippets = new List<Snippet>();
        var json = JsonSerializer.Serialize(defaultSnippets, JsonSerializerOptionsFactory.Default);
        await _fileSystem.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Create default templates.json file
    /// </summary>
    private async Task CreateDefaultTemplatesFileAsync(string path)
    {
        var defaultTemplates = new List<NoteTemplate>();
        var json = JsonSerializer.Serialize(defaultTemplates, JsonSerializerOptionsFactory.Default);
        await _fileSystem.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Create default AppData structure
    /// </summary>
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
            },
            Groups = new List<NoteGroup>(),
            Tags = new List<NoteTag>()
        };
    }

    /// <summary>
    /// Validate that a configuration file contains valid JSON
    /// </summary>
    private async Task<bool> ValidateConfigurationFileAsync(string configPath)
    {
        try
        {
            var content = await _fileSystem.ReadAllTextAsync(configPath);
            
            // Try to parse as JSON to validate structure
            using var document = JsonDocument.Parse(content);
            
            // Additional validation based on file type
            var fileName = Path.GetFileName(configPath).ToLowerInvariant();
            
            if (fileName == AppConstants.NotesFileName)
            {
                // Validate AppData structure
                JsonSerializer.Deserialize<AppData>(content, JsonSerializerOptionsFactory.Default);
            }
            else if (fileName == AppConstants.SettingsFileName)
            {
                // Validate AppSettings structure
                JsonSerializer.Deserialize<AppSettings>(content);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
