using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DevSticky.Services;

/// <summary>
/// Controller for managing safe mode operations and minimal service configuration
/// </summary>
public class SafeModeController : ISafeModeController
{
    private readonly IFileSystem? _fileSystem;
    private readonly IExceptionLogger? _exceptionLogger;
    private SafeModeConfig _configuration;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsInSafeMode => _configuration.IsEnabled;

    /// <inheritdoc />
    public SafeModeConfig Configuration => _configuration;

    /// <summary>
    /// Initializes a new instance of SafeModeController
    /// </summary>
    /// <param name="fileSystem">File system service for configuration persistence</param>
    /// <param name="exceptionLogger">Exception logger for error tracking</param>
    public SafeModeController(IFileSystem? fileSystem = null, IExceptionLogger? exceptionLogger = null)
    {
        _fileSystem = fileSystem;
        _exceptionLogger = exceptionLogger;
        _configuration = LoadConfiguration();
    }

    /// <inheritdoc />
    public void ActivateSafeMode(string reason)
    {
        try
        {
            _configuration = SafeModeConfig.CreateForReason(reason);
            SaveConfiguration();
            
            _exceptionLogger?.LogStartupException(
                new InvalidOperationException($"Safe mode activated: {reason}"), 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "ActivateSafeMode", 
                    Parameters = new Dictionary<string, object> { { "reason", reason } }
                });
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "ActivateSafeMode", 
                    Parameters = new Dictionary<string, object> { { "reason", reason } }
                });
            
            // Fallback to in-memory configuration
            _configuration = SafeModeConfig.CreateForReason(reason);
        }
    }

    /// <inheritdoc />
    public bool DeactivateSafeMode()
    {
        try
        {
            _configuration.IsEnabled = false;
            _configuration.Reason = string.Empty;
            SaveConfiguration();
            
            // Log deactivation as informational exception
            _exceptionLogger?.LogStartupException(
                new InvalidOperationException("Safe mode deactivated"), 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "DeactivateSafeMode"
                });
            
            return true;
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "DeactivateSafeMode"
                });
            return false;
        }
    }

    /// <inheritdoc />
    public bool ShouldActivateSafeMode(IReadOnlyList<ValidationIssue> startupFailures)
    {
        if (startupFailures == null || startupFailures.Count == 0)
            return false;

        // Check for critical failures that warrant safe mode
        var criticalFailures = startupFailures
            .Where(f => f.Severity == ValidationSeverity.Critical)
            .ToList();

        if (criticalFailures.Count > 0)
        {
            // Add failures to configuration for tracking
            _configuration.StartupFailures.AddRange(
                criticalFailures.Select(f => f.Issue));
            
            return true;
        }

        // Check for multiple error-level failures
        var errorFailures = startupFailures
            .Where(f => f.Severity == ValidationSeverity.Error)
            .ToList();

        if (errorFailures.Count >= 3)
        {
            _configuration.StartupFailures.AddRange(
                errorFailures.Select(f => f.Issue));
            
            return true;
        }

        // Check for specific failure patterns that indicate need for safe mode
        var failurePatterns = new[]
        {
            "service initialization",
            "dependency injection",
            "configuration corruption",
            "resource loading",
            "critical service"
        };

        var hasPatternMatch = startupFailures.Any(f => 
            failurePatterns.Any(pattern => 
                f.Issue.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

        if (hasPatternMatch)
        {
            _configuration.StartupFailures.AddRange(
                startupFailures.Where(f => 
                    failurePatterns.Any(pattern => 
                        f.Issue.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.Issue));
            
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> GetEssentialServices()
    {
        return new[]
        {
            typeof(IFileSystem),
            typeof(IErrorHandler),
            typeof(IExceptionLogger),
            typeof(IStorageService),
            typeof(INoteService),
            typeof(IThemeService),
            typeof(IDebounceService),
            typeof(IDialogService)
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> GetNonEssentialServices()
    {
        return new[]
        {
            typeof(ICloudSyncService),
            typeof(IHotkeyService),
            typeof(IMarkdownService),
            typeof(ISnippetService),
            typeof(ITemplateService),
            typeof(IExportService),
            typeof(ISearchService),
            typeof(ILinkService),
            typeof(IGroupManagementService),
            typeof(ITagManagementService),
            typeof(IFormatterService),
            typeof(IEncryptionService)
        };
    }

    /// <inheritdoc />
    public void ConfigureMinimalServices(IServiceProvider serviceProvider)
    {
        if (!IsInSafeMode)
            return;

        try
        {
            // Verify essential services are available
            var essentialServices = GetEssentialServices();
            var missingServices = new List<Type>();

            foreach (var serviceType in essentialServices)
            {
                try
                {
                    var service = serviceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        missingServices.Add(serviceType);
                    }
                }
                catch (Exception ex)
                {
                    _exceptionLogger?.LogStartupException(ex, 
                        new StartupExceptionContext 
                        { 
                            Component = "SafeModeController", 
                            Operation = "ConfigureMinimalServices", 
                            Parameters = new Dictionary<string, object> { { "serviceType", serviceType.Name } }
                        });
                    missingServices.Add(serviceType);
                }
            }

            if (missingServices.Count > 0)
            {
                var missingServiceNames = string.Join(", ", missingServices.Select(s => s.Name));
                _exceptionLogger?.LogStartupException(
                    new InvalidOperationException($"Missing essential services in safe mode: {missingServiceNames}"), 
                    new StartupExceptionContext 
                    { 
                        Component = "SafeModeController", 
                        Operation = "ConfigureMinimalServices", 
                        Parameters = new Dictionary<string, object> { { "missingServices", missingServiceNames } }
                    });
            }

            // Log successful configuration as informational exception
            _exceptionLogger?.LogStartupException(
                new InvalidOperationException("Minimal services configured for safe mode"), 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "ConfigureMinimalServices"
                });
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "ConfigureMinimalServices"
                });
        }
    }

    /// <inheritdoc />
    public void ResetConfigurationToDefaults()
    {
        try
        {
            // Reset application settings to defaults
            var defaultSettings = new AppSettings();
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            var settingsPath = Path.Combine(appDataPath, AppConstants.SettingsFileName);

            if (_fileSystem != null)
            {
                var settingsJson = JsonSerializer.Serialize(defaultSettings, 
                    JsonSerializerOptionsFactory.Default);
                _fileSystem.WriteAllTextAsync(settingsPath, settingsJson).Wait();
            }
            else
            {
                var settingsJson = JsonSerializer.Serialize(defaultSettings, 
                    JsonSerializerOptionsFactory.Default);
                File.WriteAllText(settingsPath, settingsJson);
            }

            _exceptionLogger?.LogStartupException(
                new InvalidOperationException("Configuration reset to defaults"), 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "ResetConfigurationToDefaults"
                });
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "ResetConfigurationToDefaults"
                });
        }
    }

    /// <inheritdoc />
    public SafeModeStatus GetSafeModeStatus()
    {
        var status = new SafeModeStatus
        {
            IsActive = IsInSafeMode,
            Reason = _configuration.Reason,
            ActivatedAt = _configuration.IsEnabled ? _configuration.ActivatedAt : null,
            StartupFailures = new List<string>(_configuration.StartupFailures),
            ConfigurationReset = _configuration.UseDefaultSettings
        };

        if (IsInSafeMode)
        {
            // Add disabled services to status
            var nonEssentialServices = GetNonEssentialServices();
            status.DisabledServices.AddRange(nonEssentialServices.Select(s => s.Name));
        }

        return status;
    }

    /// <summary>
    /// Loads safe mode configuration from file or creates default
    /// </summary>
    /// <returns>Safe mode configuration</returns>
    private SafeModeConfig LoadConfiguration()
    {
        try
        {
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            var configPath = Path.Combine(appDataPath, "safemode.json");

            string configJson;
            if (_fileSystem != null)
            {
                if (!_fileSystem.FileExists(configPath))
                    return SafeModeConfig.CreateDefault();
                
                configJson = _fileSystem.ReadAllTextAsync(configPath).GetAwaiter().GetResult();
            }
            else
            {
                if (!File.Exists(configPath))
                    return SafeModeConfig.CreateDefault();
                
                configJson = File.ReadAllText(configPath);
            }

            if (string.IsNullOrWhiteSpace(configJson))
                return SafeModeConfig.CreateDefault();

            var config = JsonSerializer.Deserialize<SafeModeConfig>(configJson, 
                JsonSerializerOptionsFactory.Default);
            
            return config ?? SafeModeConfig.CreateDefault();
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "LoadConfiguration"
                });
            return SafeModeConfig.CreateDefault();
        }
    }

    /// <summary>
    /// Saves safe mode configuration to file
    /// </summary>
    private void SaveConfiguration()
    {
        try
        {
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            
            // Ensure directory exists
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            var configPath = Path.Combine(appDataPath, "safemode.json");
            var configJson = JsonSerializer.Serialize(_configuration, 
                JsonSerializerOptionsFactory.Default);

            if (_fileSystem != null)
            {
                _fileSystem.WriteAllTextAsync(configPath, configJson).Wait();
            }
            else
            {
                File.WriteAllText(configPath, configJson);
            }
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "SaveConfiguration"
                });
        }
    }

    /// <summary>
    /// Disposes the safe mode controller
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // Save configuration on dispose
            if (IsInSafeMode)
            {
                SaveConfiguration();
            }
        }
        catch (Exception ex)
        {
            _exceptionLogger?.LogStartupException(ex, 
                new StartupExceptionContext 
                { 
                    Component = "SafeModeController", 
                    Operation = "Dispose"
                });
        }
        finally
        {
            _disposed = true;
        }
    }
}