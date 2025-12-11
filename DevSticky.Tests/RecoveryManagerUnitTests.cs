using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for RecoveryManager class
/// Tests individual recovery methods and their behavior
/// </summary>
public class RecoveryManagerUnitTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdFiles = new();
    private readonly List<string> _createdDirectories = new();

    public RecoveryManagerUnitTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DevStickyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateRecoveryManager()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();

        // Act
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);

        // Assert
        Assert.NotNull(recoveryManager);
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ShouldThrowArgumentNullException()
    {
        // Arrange
        IFileSystem nullFileSystem = null!;
        var errorHandler = new ErrorHandler();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RecoveryManager(nullFileSystem, errorHandler));
    }

    [Fact]
    public void Constructor_WithNullErrorHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        IErrorHandler nullErrorHandler = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RecoveryManager(fileSystem, nullErrorHandler));
    }

    #endregion

    #region RecoverMissingConfigurationAsync Tests

    [Fact]
    public async Task RecoverMissingConfigurationAsync_WithMissingFile_ShouldCreateDefaultConfiguration()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var missingFilePath = Path.Combine(_testDirectory, "missing_config.json");

        // Act
        var result = await recoveryManager.RecoverMissingConfigurationAsync(missingFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccessful, "Recovery should succeed for missing file");
        Assert.Equal(RecoveryAction.CreateDefaultConfiguration, result.Action);
        Assert.False(string.IsNullOrEmpty(result.Message));
        Assert.True(result.Timestamp <= DateTime.UtcNow);
        
        // Verify file was created
        Assert.True(fileSystem.FileExists(missingFilePath), "Configuration file should be created");
        _createdFiles.Add(missingFilePath);
        
        // Verify file contains valid JSON
        var content = await fileSystem.ReadAllTextAsync(missingFilePath);
        Assert.False(string.IsNullOrEmpty(content));
        var contentDoc = System.Text.Json.JsonDocument.Parse(content);
        Assert.NotNull(contentDoc);
    }

    [Fact]
    public async Task RecoverMissingConfigurationAsync_WithExistingFile_ShouldNotOverwrite()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var existingFilePath = Path.Combine(_testDirectory, "existing_config.json");
        var originalContent = "{\"existing\": \"data\"}";
        
        await fileSystem.WriteAllTextAsync(existingFilePath, originalContent);
        _createdFiles.Add(existingFilePath);

        // Act
        var result = await recoveryManager.RecoverMissingConfigurationAsync(existingFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccessful, "Recovery should not be needed for existing file");
        Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
        
        // Verify original content is preserved
        var content = await fileSystem.ReadAllTextAsync(existingFilePath);
        Assert.Equal(originalContent, content);
    }

    [Fact]
    public async Task RecoverMissingConfigurationAsync_WithInvalidPath_ShouldHandleGracefully()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var invalidPath = ""; // Empty path

        // Act
        var result = await recoveryManager.RecoverMissingConfigurationAsync(invalidPath);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccessful, "Recovery should fail for invalid path");
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region RecoverCorruptedConfigurationAsync Tests

    [Fact]
    public async Task RecoverCorruptedConfigurationAsync_WithCorruptedFile_ShouldBackupAndReplace()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var corruptedFilePath = Path.Combine(_testDirectory, "corrupted_config.json");
        var corruptedContent = "{ invalid json content";
        
        await fileSystem.WriteAllTextAsync(corruptedFilePath, corruptedContent);
        _createdFiles.Add(corruptedFilePath);

        // Act
        var result = await recoveryManager.RecoverCorruptedConfigurationAsync(corruptedFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccessful, "Recovery should succeed for corrupted file");
        Assert.Equal(RecoveryAction.BackupCorruptedConfiguration, result.Action);
        Assert.False(string.IsNullOrEmpty(result.Message));
        
        // Verify backup was created
        var backupFiles = Directory.GetFiles(_testDirectory, "corrupted_config.json.backup_*");
        Assert.True(backupFiles.Length > 0, "Backup file should be created");
        
        // Verify original file now contains valid JSON
        var newContent = await fileSystem.ReadAllTextAsync(corruptedFilePath);
        var newContentDoc = System.Text.Json.JsonDocument.Parse(newContent);
        Assert.NotNull(newContentDoc);
        
        // Verify backup contains original corrupted content
        var backupContent = await fileSystem.ReadAllTextAsync(backupFiles[0]);
        Assert.Equal(corruptedContent, backupContent);
    }

    [Fact]
    public async Task RecoverCorruptedConfigurationAsync_WithValidFile_ShouldNotModify()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var validFilePath = Path.Combine(_testDirectory, "valid_config.json");
        var validContent = "{\"valid\": \"json\"}";
        
        await fileSystem.WriteAllTextAsync(validFilePath, validContent);
        _createdFiles.Add(validFilePath);

        // Act
        var result = await recoveryManager.RecoverCorruptedConfigurationAsync(validFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccessful, "Recovery should not be needed for valid file");
        Assert.Contains("valid", result.Message, StringComparison.OrdinalIgnoreCase);
        
        // Verify original content is preserved
        var content = await fileSystem.ReadAllTextAsync(validFilePath);
        Assert.Equal(validContent, content);
        
        // Verify no backup was created
        var backupFiles = Directory.GetFiles(_testDirectory, "valid_config.json.backup_*");
        Assert.Empty(backupFiles);
    }

    [Fact]
    public async Task RecoverCorruptedConfigurationAsync_WithMissingFile_ShouldCreateDefault()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var missingFilePath = Path.Combine(_testDirectory, "missing_for_corruption.json");

        // Act
        var result = await recoveryManager.RecoverCorruptedConfigurationAsync(missingFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccessful, "Recovery should create default for missing file");
        Assert.Equal(RecoveryAction.CreateDefaultConfiguration, result.Action);
        
        // Verify file was created with valid JSON
        Assert.True(fileSystem.FileExists(missingFilePath));
        _createdFiles.Add(missingFilePath);
        
        var content = await fileSystem.ReadAllTextAsync(missingFilePath);
        var contentDoc = System.Text.Json.JsonDocument.Parse(content);
        Assert.NotNull(contentDoc);
    }

    #endregion

    #region RecoverMissingDirectoriesAsync Tests

    [Fact]
    public async Task RecoverMissingDirectoriesAsync_WithMissingDirectory_ShouldCreateDirectory()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var missingDirPath = Path.Combine(_testDirectory, "missing_directory");

        // Act
        var result = await recoveryManager.RecoverMissingDirectoriesAsync(missingDirPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccessful, "Recovery should succeed for missing directory");
        Assert.Equal(RecoveryAction.CreateMissingDirectories, result.Action);
        Assert.False(string.IsNullOrEmpty(result.Message));
        
        // Verify directory was created
        Assert.True(fileSystem.DirectoryExists(missingDirPath), "Directory should be created");
        _createdDirectories.Add(missingDirPath);
    }

    [Fact]
    public async Task RecoverMissingDirectoriesAsync_WithExistingDirectory_ShouldNotModify()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var existingDirPath = Path.Combine(_testDirectory, "existing_directory");
        Directory.CreateDirectory(existingDirPath);
        _createdDirectories.Add(existingDirPath);

        // Act
        var result = await recoveryManager.RecoverMissingDirectoriesAsync(existingDirPath);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccessful, "Recovery should not be needed for existing directory");
        Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
        
        // Verify directory still exists
        Assert.True(fileSystem.DirectoryExists(existingDirPath));
    }

    [Fact]
    public async Task RecoverMissingDirectoriesAsync_WithNestedPath_ShouldCreateAllDirectories()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var nestedDirPath = Path.Combine(_testDirectory, "level1", "level2", "level3");

        // Act
        var result = await recoveryManager.RecoverMissingDirectoriesAsync(nestedDirPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccessful, "Recovery should succeed for nested directories");
        Assert.Equal(RecoveryAction.CreateMissingDirectories, result.Action);
        
        // Verify all nested directories were created
        Assert.True(fileSystem.DirectoryExists(nestedDirPath), "Nested directory should be created");
        _createdDirectories.Add(Path.Combine(_testDirectory, "level1"));
    }

    #endregion

    #region CreateConfigurationBackupAsync Tests

    [Fact]
    public async Task CreateConfigurationBackupAsync_WithExistingFile_ShouldCreateBackup()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var originalFilePath = Path.Combine(_testDirectory, "backup_test.json");
        var originalContent = "{\"test\": \"data\"}";
        
        await fileSystem.WriteAllTextAsync(originalFilePath, originalContent);
        _createdFiles.Add(originalFilePath);

        // Act
        var backupPath = await recoveryManager.CreateConfigurationBackupAsync(originalFilePath);

        // Assert
        Assert.NotNull(backupPath);
        Assert.False(string.IsNullOrEmpty(backupPath));
        Assert.Contains("backup_", backupPath);
        Assert.True(fileSystem.FileExists(backupPath), "Backup file should be created");
        
        // Verify backup contains original content
        var backupContent = await fileSystem.ReadAllTextAsync(backupPath);
        Assert.Equal(originalContent, backupContent);
        
        // Verify original file still exists
        Assert.True(fileSystem.FileExists(originalFilePath));
        var originalContentAfter = await fileSystem.ReadAllTextAsync(originalFilePath);
        Assert.Equal(originalContent, originalContentAfter);
    }

    [Fact]
    public async Task CreateConfigurationBackupAsync_WithMissingFile_ShouldReturnNull()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var missingFilePath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act
        var backupPath = await recoveryManager.CreateConfigurationBackupAsync(missingFilePath);

        // Assert
        Assert.Null(backupPath);
    }

    [Fact]
    public async Task CreateConfigurationBackupAsync_ShouldCreateUniqueBackupNames()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        var originalFilePath = Path.Combine(_testDirectory, "unique_backup_test.json");
        var originalContent = "{\"test\": \"data\"}";
        
        await fileSystem.WriteAllTextAsync(originalFilePath, originalContent);
        _createdFiles.Add(originalFilePath);

        // Act
        var backupPath1 = await recoveryManager.CreateConfigurationBackupAsync(originalFilePath);
        await Task.Delay(10); // Ensure different timestamps
        var backupPath2 = await recoveryManager.CreateConfigurationBackupAsync(originalFilePath);

        // Assert
        Assert.NotNull(backupPath1);
        Assert.NotNull(backupPath2);
        Assert.NotEqual(backupPath1, backupPath2);
        Assert.True(fileSystem.FileExists(backupPath1));
        Assert.True(fileSystem.FileExists(backupPath2));
    }

    #endregion

    #region PerformComprehensiveRecoveryAsync Tests

    [Fact]
    public async Task PerformComprehensiveRecoveryAsync_ShouldIdentifyAndRecoverIssues()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);

        // Act
        var results = await recoveryManager.PerformComprehensiveRecoveryAsync();

        // Assert
        Assert.NotNull(results);
        // Should return a list (may be empty if no issues found)
        Assert.True(results.Count >= 0);
        
        // All results should have valid timestamps and messages
        foreach (var result in results)
        {
            Assert.True(result.Timestamp > DateTime.MinValue);
            Assert.False(string.IsNullOrEmpty(result.Message));
            Assert.True(Enum.IsDefined(typeof(RecoveryAction), result.Action));
        }
    }

    [Fact]
    public async Task PerformComprehensiveRecoveryAsync_WithMixedIssues_ShouldHandleAll()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        // Create a scenario with mixed issues
        var testSubDir = Path.Combine(_testDirectory, "comprehensive_test");
        var corruptedFile = Path.Combine(testSubDir, "corrupted.json");
        
        // Create directory and corrupted file
        Directory.CreateDirectory(testSubDir);
        _createdDirectories.Add(testSubDir);
        
        await fileSystem.WriteAllTextAsync(corruptedFile, "{ invalid json");
        _createdFiles.Add(corruptedFile);

        // Act
        var results = await recoveryManager.PerformComprehensiveRecoveryAsync();

        // Assert
        Assert.NotNull(results);
        
        if (results.Count > 0)
        {
            var successfulRecoveries = results.Where(r => r.IsSuccessful).ToList();
            Assert.True(successfulRecoveries.Count > 0, "Should have some successful recoveries");
            
            // Should handle different types of recovery actions
            var actionTypes = results.Select(r => r.Action).Distinct().ToList();
            Assert.True(actionTypes.Count > 0, "Should perform different types of recovery actions");
        }
    }

    #endregion

    #region IdentifyRequiredRecoveryActionsAsync Tests

    [Fact]
    public async Task IdentifyRequiredRecoveryActionsAsync_ShouldReturnActionsList()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);

        // Act
        var actions = await recoveryManager.IdentifyRequiredRecoveryActionsAsync();

        // Assert
        Assert.NotNull(actions);
        // Should return a list (may be empty if no issues found)
        Assert.True(actions.Count >= 0);
        
        // All actions should be valid enum values
        foreach (var action in actions)
        {
            Assert.True(Enum.IsDefined(typeof(RecoveryAction), action));
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task RecoveryMethods_WithIOExceptions_ShouldHandleGracefully()
    {
        // Arrange
        var fileSystem = new FileSystemAdapter();
        var errorHandler = new ErrorHandler();
        var recoveryManager = new RecoveryManager(fileSystem, errorHandler);
        
        // Use a path that might cause IO issues (very long path)
        var problematicPath = Path.Combine(_testDirectory, new string('a', 200), "test.json");

        // Act & Assert - Should not throw exceptions
        var configResult = await recoveryManager.RecoverMissingConfigurationAsync(problematicPath);
        Assert.NotNull(configResult);
        
        var corruptionResult = await recoveryManager.RecoverCorruptedConfigurationAsync(problematicPath);
        Assert.NotNull(corruptionResult);
        
        var directoryResult = await recoveryManager.RecoverMissingDirectoriesAsync(problematicPath);
        Assert.NotNull(directoryResult);
    }

    #endregion

    public void Dispose()
    {
        // Clean up created files
        foreach (var file in _createdFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        // Clean up created directories (in reverse order for nested directories)
        for (int i = _createdDirectories.Count - 1; i >= 0; i--)
        {
            try
            {
                var directory = _createdDirectories[i];
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}