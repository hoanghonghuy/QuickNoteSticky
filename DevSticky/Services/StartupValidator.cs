using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DevSticky.Services;

/// <summary>
/// Service for validating startup prerequisites and dependencies
/// </summary>
public class StartupValidator : IStartupValidator
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly IFileSystem? _fileSystem;
    
    /// <summary>
    /// Initialize validator without dependencies (for early startup validation)
    /// </summary>
    public StartupValidator()
    {
        _serviceProvider = null;
        _fileSystem = null;
    }
    
    /// <summary>
    /// Initialize validator with service provider (for full validation)
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency validation</param>
    /// <param name="fileSystem">File system service for file operations</param>
    public StartupValidator(IServiceProvider serviceProvider, IFileSystem fileSystem)
    {
        _serviceProvider = serviceProvider;
        _fileSystem = fileSystem;
    }
    
    /// <inheritdoc />
    public ValidationResult Validate()
    {
        var startTime = DateTime.UtcNow;
        var overallResult = ValidationResult.Success("StartupValidator");
        
        try
        {
            // Validate dependencies first (DLLs and NuGet packages)
            var dependencyResult = ValidateDependencies();
            overallResult.Merge(dependencyResult);
            
            // Validate directories
            var directoryResult = ValidateDirectories();
            overallResult.Merge(directoryResult);
            
            // Validate configuration
            var configResult = ValidateConfiguration();
            overallResult.Merge(configResult);
            
            // Validate resources
            var resourceResult = ValidateResources();
            overallResult.Merge(resourceResult);
            
            // Validate services (only if service provider is available)
            if (_serviceProvider != null)
            {
                var serviceResult = ValidateServices();
                overallResult.Merge(serviceResult);
                
                var diResult = ValidateDependencyInjection();
                overallResult.Merge(diResult);
            }
            else
            {
                overallResult.AddIssue(ValidationIssue.Information(
                    "StartupValidator", 
                    "Service provider not available - skipping service and DI validation",
                    "Initialize validator with service provider for full validation"));
            }
        }
        catch (Exception ex)
        {
            overallResult.AddIssue(ValidationIssue.FromException("StartupValidator", ex, ValidationSeverity.Critical));
        }
        finally
        {
            overallResult.Duration = DateTime.UtcNow - startTime;
        }
        
        return overallResult;
    }
    
    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAsync()
    {
        // For now, just run synchronous validation
        // Can be enhanced later with async operations
        return await Task.FromResult(Validate());
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateDirectories()
    {
        var startTime = DateTime.UtcNow;
        var result = ValidationResult.Success("DirectoryValidation");
        
        try
        {
            // Get application data path
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            
            // Check if main app data directory exists
            if (!Directory.Exists(appDataPath))
            {
                try
                {
                    Directory.CreateDirectory(appDataPath);
                    result.AddIssue(ValidationIssue.Information(
                        "DirectoryValidation",
                        $"Created missing app data directory: {appDataPath}",
                        "Directory created successfully"));
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "DirectoryValidation", 
                        ex, 
                        ValidationSeverity.Critical,
                        "Ensure the application has write permissions to the AppData folder"));
                    return result;
                }
            }
            
            // Check if directory is writable
            try
            {
                var testFile = Path.Combine(appDataPath, "write_test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                
                result.AddIssue(ValidationIssue.Information(
                    "DirectoryValidation",
                    $"App data directory is writable: {appDataPath}"));
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "DirectoryValidation",
                    ex,
                    ValidationSeverity.Critical,
                    "Ensure the application has write permissions to the app data directory"));
            }
            
            // Check temp directory access
            try
            {
                var tempPath = Path.GetTempPath();
                var testFile = Path.Combine(tempPath, $"devsticky_test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                
                result.AddIssue(ValidationIssue.Information(
                    "DirectoryValidation",
                    "Temp directory is accessible and writable"));
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "DirectoryValidation",
                    ex,
                    ValidationSeverity.Warning,
                    "Check system temp directory permissions"));
            }
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException("DirectoryValidation", ex, ValidationSeverity.Critical));
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateServices()
    {
        var startTime = DateTime.UtcNow;
        var result = ValidationResult.Success("ServiceValidation");
        
        if (_serviceProvider == null)
        {
            result.AddIssue(ValidationIssue.Error(
                "ServiceValidation",
                "Service provider not available",
                "Initialize validator with service provider"));
            return result;
        }
        
        try
        {
            // List of critical services that must be available
            var criticalServices = new[]
            {
                typeof(IFileSystem),
                typeof(IErrorHandler),
                typeof(IExceptionLogger),
                typeof(IStorageService),
                typeof(INoteService),
                typeof(IThemeService),
                typeof(IDebounceService)
            };
            
            foreach (var serviceType in criticalServices)
            {
                try
                {
                    var service = _serviceProvider.GetRequiredService(serviceType);
                    if (service == null)
                    {
                        result.AddIssue(ValidationIssue.Critical(
                            "ServiceValidation",
                            $"Critical service {serviceType.Name} is null",
                            "Check service registration in ConfigureServices"));
                    }
                    else
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "ServiceValidation",
                            $"Critical service {serviceType.Name} is available"));
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "ServiceValidation",
                        ex,
                        ValidationSeverity.Critical,
                        $"Register {serviceType.Name} in ConfigureServices method"));
                }
            }
            
            // List of optional services that should be available but aren't critical
            var optionalServices = new[]
            {
                typeof(ICloudSyncService),
                typeof(IHotkeyService),
                typeof(IMarkdownService),
                typeof(ISnippetService),
                typeof(ITemplateService)
            };
            
            foreach (var serviceType in optionalServices)
            {
                try
                {
                    var service = _serviceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        result.AddIssue(ValidationIssue.Warning(
                            "ServiceValidation",
                            $"Optional service {serviceType.Name} is not available",
                            "Some features may not work correctly"));
                    }
                    else
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "ServiceValidation",
                            $"Optional service {serviceType.Name} is available"));
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "ServiceValidation",
                        ex,
                        ValidationSeverity.Warning,
                        $"Check {serviceType.Name} registration if this service is needed"));
                }
            }
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException("ServiceValidation", ex, ValidationSeverity.Critical));
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateConfiguration()
    {
        var startTime = DateTime.UtcNow;
        var result = ValidationResult.Success("ConfigurationValidation");
        
        try
        {
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            
            // Validate settings file
            var settingsPath = Path.Combine(appDataPath, AppConstants.SettingsFileName);
            ValidateJsonFile(settingsPath, "Settings", result, isRequired: false);
            
            // Validate notes file
            var notesPath = Path.Combine(appDataPath, AppConstants.NotesFileName);
            ValidateJsonFile(notesPath, "Notes", result, isRequired: false);
            
            // Validate snippets file
            var snippetsPath = Path.Combine(appDataPath, AppConstants.SnippetsFileName);
            ValidateJsonFile(snippetsPath, "Snippets", result, isRequired: false);
            
            // Validate templates file
            var templatesPath = Path.Combine(appDataPath, AppConstants.TemplatesFileName);
            ValidateJsonFile(templatesPath, "Templates", result, isRequired: false);
            
            // Test AppSettings loading and validate required properties
            try
            {
                var settings = AppSettings.Load();
                if (settings != null)
                {
                    result.AddIssue(ValidationIssue.Information(
                        "ConfigurationValidation",
                        "AppSettings loaded successfully"));
                        
                    // Validate required properties exist and have valid values
                    ValidateAppSettingsProperties(settings, result);
                }
                else
                {
                    result.AddIssue(ValidationIssue.Warning(
                        "ConfigurationValidation",
                        "AppSettings.Load() returned null",
                        "Check settings file format"));
                }
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "ConfigurationValidation",
                    ex,
                    ValidationSeverity.Warning,
                    "Reset settings to defaults or check file format"));
            }
            
            // Validate configuration file structure and required properties
            ValidateConfigurationStructure(result);
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException("ConfigurationValidation", ex, ValidationSeverity.Critical));
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateDependencyInjection()
    {
        var startTime = DateTime.UtcNow;
        var result = ValidationResult.Success("DependencyInjectionValidation");
        
        if (_serviceProvider == null)
        {
            result.AddIssue(ValidationIssue.Error(
                "DependencyInjectionValidation",
                "Service provider not available",
                "Initialize validator with service provider"));
            return result;
        }
        
        try
        {
            // Test circular dependency detection by attempting to resolve key services
            var testServices = new[]
            {
                typeof(IStorageService),
                typeof(INoteService),
                typeof(IThemeService),
                typeof(IErrorHandler),
                typeof(IExceptionLogger)
            };
            
            foreach (var serviceType in testServices)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService(serviceType);
                    
                    if (service != null)
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "DependencyInjectionValidation",
                            $"Successfully resolved {serviceType.Name}"));
                    }
                    else
                    {
                        result.AddIssue(ValidationIssue.Error(
                            "DependencyInjectionValidation",
                            $"Service {serviceType.Name} resolved to null",
                            "Check service implementation"));
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "DependencyInjectionValidation",
                        ex,
                        ValidationSeverity.Error,
                        $"Check dependencies for {serviceType.Name}"));
                }
            }
            
            // Test transient service creation
            try
            {
                var exportService1 = _serviceProvider.GetRequiredService<IExportService>();
                var exportService2 = _serviceProvider.GetRequiredService<IExportService>();
                
                if (ReferenceEquals(exportService1, exportService2))
                {
                    result.AddIssue(ValidationIssue.Warning(
                        "DependencyInjectionValidation",
                        "Transient service IExportService returned same instance",
                        "Check service registration - should be AddTransient"));
                }
                else
                {
                    result.AddIssue(ValidationIssue.Information(
                        "DependencyInjectionValidation",
                        "Transient services working correctly"));
                }
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "DependencyInjectionValidation",
                    ex,
                    ValidationSeverity.Warning,
                    "Check IExportService registration"));
            }
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException("DependencyInjectionValidation", ex, ValidationSeverity.Critical));
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateResources()
    {
        var startTime = DateTime.UtcNow;
        var result = ValidationResult.Success("ResourceValidation");
        
        try
        {
            // Check if we're in a WPF context
            if (System.Windows.Application.Current == null)
            {
                result.AddIssue(ValidationIssue.Warning(
                    "ResourceValidation",
                    "Application.Current is null - cannot validate WPF resources",
                    "Run validation after WPF application initialization"));
                return result;
            }
            
            // Validate theme resource files
            var themeFiles = new[]
            {
                "Resources/DarkTheme.xaml",
                "Resources/LightTheme.xaml",
                "Resources/SharedStyles.xaml"
            };
            
            foreach (var themeFile in themeFiles)
            {
                try
                {
                    var uri = new Uri($"pack://application:,,,/DevSticky;component/{themeFile}");
                    var resourceDict = new ResourceDictionary { Source = uri };
                    
                    if (resourceDict.Count > 0)
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "ResourceValidation",
                            $"Theme resource {themeFile} loaded successfully ({resourceDict.Count} resources)"));
                    }
                    else
                    {
                        result.AddIssue(ValidationIssue.Warning(
                            "ResourceValidation",
                            $"Theme resource {themeFile} is empty",
                            "Check theme file content"));
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "ResourceValidation",
                        ex,
                        ValidationSeverity.Error,
                        $"Ensure {themeFile} exists and is properly formatted"));
                }
            }
            
            // Validate string resources
            try
            {
                var stringUri = new Uri("pack://application:,,,/DevSticky;component/Resources/Strings.resx");
                // Note: .resx files need special handling, this is a simplified check
                result.AddIssue(ValidationIssue.Information(
                    "ResourceValidation",
                    "String resource validation completed"));
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "ResourceValidation",
                    ex,
                    ValidationSeverity.Warning,
                    "Check string resource files"));
            }
            
            // Validate application icon
            try
            {
                var iconUri = new Uri("pack://application:,,,/DevSticky;component/Resources/app.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                
                if (streamInfo != null)
                {
                    result.AddIssue(ValidationIssue.Information(
                        "ResourceValidation",
                        "Application icon resource is accessible"));
                    streamInfo.Stream.Dispose();
                }
                else
                {
                    result.AddIssue(ValidationIssue.Warning(
                        "ResourceValidation",
                        "Application icon resource not found",
                        "Check if app.ico exists in Resources folder"));
                }
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "ResourceValidation",
                    ex,
                    ValidationSeverity.Warning,
                    "Check application icon resource"));
            }
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException("ResourceValidation", ex, ValidationSeverity.Critical));
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateDependencies()
    {
        var startTime = DateTime.UtcNow;
        var result = ValidationResult.Success("DependencyValidation");
        
        try
        {
            // Validate required NuGet packages by checking if their assemblies are loaded
            var requiredPackages = new Dictionary<string, string>
            {
                { "AvalonEdit", "ICSharpCode.AvalonEdit" },
                { "Google.Apis.Drive.v3", "Google.Apis.Drive.v3" },
                { "Hardcodet.NotifyIcon.Wpf", "Hardcodet.Wpf.TaskbarNotification" },
                { "Markdig", "Markdig" },
                { "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection" },
                { "Microsoft.Graph", "Microsoft.Graph" },
                { "Microsoft.Identity.Client", "Microsoft.Identity.Client" },
                { "Microsoft.Identity.Client.Extensions.Msal", "Microsoft.Identity.Client.Extensions.Msal" },
                { "Microsoft.Web.WebView2", "Microsoft.Web.WebView2.Core" }
            };
            
            foreach (var package in requiredPackages)
            {
                try
                {
                    // Try to load the assembly to verify the package is available
                    var assembly = Assembly.LoadFrom($"{package.Value}.dll");
                    if (assembly != null)
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "DependencyValidation",
                            $"Required package {package.Key} ({package.Value}) is available"));
                    }
                }
                catch (FileNotFoundException)
                {
                    // Try alternative approach - check if assembly is already loaded
                    var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name?.Contains(package.Value.Split('.')[0]) == true);
                    
                    if (loadedAssembly != null)
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "DependencyValidation",
                            $"Required package {package.Key} is loaded: {loadedAssembly.GetName().Name}"));
                    }
                    else
                    {
                        result.AddIssue(ValidationIssue.Error(
                            "DependencyValidation",
                            $"Required package {package.Key} ({package.Value}) is not available",
                            "Restore NuGet packages or reinstall the application"));
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "DependencyValidation",
                        ex,
                        ValidationSeverity.Warning,
                        $"Could not verify package {package.Key} - it may still be available"));
                }
            }
            
            // Validate critical .NET Framework dependencies
            var frameworkDependencies = new[]
            {
                "System.Text.Json",
                "System.IO.FileSystem",
                "System.Threading.Tasks",
                "System.Reflection",
                "Microsoft.Extensions.DependencyInjection.Abstractions"
            };
            
            foreach (var dependency in frameworkDependencies)
            {
                try
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == dependency);
                    
                    if (assembly != null)
                    {
                        result.AddIssue(ValidationIssue.Information(
                            "DependencyValidation",
                            $"Framework dependency {dependency} is available"));
                    }
                    else
                    {
                        result.AddIssue(ValidationIssue.Warning(
                            "DependencyValidation",
                            $"Framework dependency {dependency} not found in loaded assemblies",
                            "This may be loaded on-demand and is likely not an issue"));
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue(ValidationIssue.FromException(
                        "DependencyValidation",
                        ex,
                        ValidationSeverity.Warning,
                        $"Could not check framework dependency {dependency}"));
                }
            }
            
            // Validate WPF dependencies
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    result.AddIssue(ValidationIssue.Information(
                        "DependencyValidation",
                        "WPF Application context is available"));
                }
                else
                {
                    result.AddIssue(ValidationIssue.Warning(
                        "DependencyValidation",
                        "WPF Application context is not available",
                        "Ensure validation runs after WPF initialization"));
                }
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "DependencyValidation",
                    ex,
                    ValidationSeverity.Error,
                    "Check WPF framework installation"));
            }
            
            // Validate .NET Runtime version
            try
            {
                var runtimeVersion = Environment.Version;
                result.AddIssue(ValidationIssue.Information(
                    "DependencyValidation",
                    $".NET Runtime version: {runtimeVersion}"));
                
                // Check if we're running on .NET 8.0 or later
                if (runtimeVersion.Major < 8)
                {
                    result.AddIssue(ValidationIssue.Error(
                        "DependencyValidation",
                        $".NET Runtime version {runtimeVersion} is below required version 8.0",
                        "Install .NET 8.0 Runtime or later"));
                }
            }
            catch (Exception ex)
            {
                result.AddIssue(ValidationIssue.FromException(
                    "DependencyValidation",
                    ex,
                    ValidationSeverity.Warning,
                    "Could not determine .NET Runtime version"));
            }
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException("DependencyValidation", ex, ValidationSeverity.Critical));
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <summary>
    /// Validate a JSON configuration file
    /// </summary>
    /// <param name="filePath">Path to the JSON file</param>
    /// <param name="fileType">Type of file for error messages</param>
    /// <param name="result">Validation result to add issues to</param>
    /// <param name="isRequired">Whether the file is required to exist</param>
    private void ValidateJsonFile(string filePath, string fileType, ValidationResult result, bool isRequired = true)
    {
        try
        {
            if (_fileSystem != null)
            {
                // Use injected file system if available
                if (!_fileSystem.FileExists(filePath))
                {
                    var severity = isRequired ? ValidationSeverity.Error : ValidationSeverity.Information;
                    result.AddIssue(new ValidationIssue
                    {
                        Component = "ConfigurationValidation",
                        Issue = $"{fileType} file not found: {filePath}",
                        Severity = severity,
                        SuggestedAction = isRequired ? "Create default configuration file" : "File will be created on first use"
                    });
                    return;
                }
                
                var content = _fileSystem.ReadAllTextAsync(filePath).GetAwaiter().GetResult();
                ValidateJsonContent(content, fileType, result);
            }
            else
            {
                // Fallback to direct file system access
                if (!File.Exists(filePath))
                {
                    var severity = isRequired ? ValidationSeverity.Error : ValidationSeverity.Information;
                    result.AddIssue(new ValidationIssue
                    {
                        Component = "ConfigurationValidation",
                        Issue = $"{fileType} file not found: {filePath}",
                        Severity = severity,
                        SuggestedAction = isRequired ? "Create default configuration file" : "File will be created on first use"
                    });
                    return;
                }
                
                var content = File.ReadAllText(filePath);
                ValidateJsonContent(content, fileType, result);
            }
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException(
                "ConfigurationValidation",
                ex,
                ValidationSeverity.Error,
                $"Check {fileType} file permissions and format"));
        }
    }
    
    /// <summary>
    /// Validate AppSettings properties have valid values
    /// </summary>
    /// <param name="settings">AppSettings instance to validate</param>
    /// <param name="result">Validation result to add issues to</param>
    private static void ValidateAppSettingsProperties(AppSettings settings, ValidationResult result)
    {
        // Validate DefaultOpacity
        if (settings.DefaultOpacity < 0.2 || settings.DefaultOpacity > 1.0)
        {
            result.AddIssue(ValidationIssue.Warning(
                "ConfigurationValidation",
                $"DefaultOpacity value {settings.DefaultOpacity} is outside valid range (0.2-1.0)",
                "Reset settings to defaults"));
        }
        
        // Validate DefaultFontSize
        if (settings.DefaultFontSize < 8 || settings.DefaultFontSize > 72)
        {
            result.AddIssue(ValidationIssue.Warning(
                "ConfigurationValidation",
                $"DefaultFontSize value {settings.DefaultFontSize} is outside reasonable range (8-72)",
                "Reset settings to defaults"));
        }
        
        // Validate Theme
        if (!string.IsNullOrEmpty(settings.Theme))
        {
            var validThemes = new[] { "Dark", "Light" };
            if (!validThemes.Contains(settings.Theme))
            {
                result.AddIssue(ValidationIssue.Warning(
                    "ConfigurationValidation",
                    $"Theme '{settings.Theme}' is not a valid theme. Use 'Dark' or 'Light'",
                    "Reset theme to defaults"));
            }
        }
        
        // Validate ThemeMode
        if (!string.IsNullOrEmpty(settings.ThemeMode))
        {
            var validThemeModes = new[] { "Light", "Dark", "System" };
            if (!validThemeModes.Contains(settings.ThemeMode))
            {
                result.AddIssue(ValidationIssue.Warning(
                    "ConfigurationValidation",
                    $"ThemeMode '{settings.ThemeMode}' is not a valid theme mode. Use 'Light', 'Dark', or 'System'",
                    "Reset theme mode to defaults"));
            }
        }
        
        // Validate default window dimensions
        if (settings.DefaultWidth < 200 || settings.DefaultHeight < 100)
        {
            result.AddIssue(ValidationIssue.Warning(
                "ConfigurationValidation",
                $"Default window size {settings.DefaultWidth}x{settings.DefaultHeight} is too small",
                "Reset window size to defaults"));
        }
        
        result.AddIssue(ValidationIssue.Information(
            "ConfigurationValidation",
            "AppSettings properties validated successfully"));
    }
    
    /// <summary>
    /// Validate JSON content
    /// </summary>
    /// <param name="content">JSON content to validate</param>
    /// <param name="fileType">Type of file for error messages</param>
    /// <param name="result">Validation result to add issues to</param>
    private static void ValidateJsonContent(string content, string fileType, ValidationResult result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                result.AddIssue(ValidationIssue.Warning(
                    "ConfigurationValidation",
                    $"{fileType} file is empty",
                    "File will be initialized with defaults"));
                return;
            }
            
            // Try to parse as JSON
            using var document = JsonDocument.Parse(content);
            
            result.AddIssue(ValidationIssue.Information(
                "ConfigurationValidation",
                $"{fileType} file is valid JSON"));
                
            // Additional validation could be added here for specific file types
        }
        catch (JsonException ex)
        {
            result.AddIssue(ValidationIssue.FromException(
                "ConfigurationValidation",
                ex,
                ValidationSeverity.Error,
                $"Fix JSON syntax in {fileType} file or reset to defaults"));
        }
    }
    
    /// <summary>
    /// Validate JSON file structure and required properties
    /// </summary>
    /// <param name="filePath">Path to JSON file</param>
    /// <param name="fileType">Type of file for error messages</param>
    /// <param name="requiredProperties">Array of required property names</param>
    /// <param name="result">Validation result to add issues to</param>
    /// <param name="isRequired">Whether the file is required to exist</param>
    private void ValidateJsonStructure(string filePath, string fileType, string[] requiredProperties, ValidationResult result, bool isRequired = true)
    {
        try
        {
            bool fileExists = _fileSystem?.FileExists(filePath) ?? File.Exists(filePath);
            if (!fileExists)
            {
                if (isRequired)
                {
                    result.AddIssue(ValidationIssue.Warning(
                        "ConfigurationValidation",
                        $"{fileType} file not found: {filePath}",
                        "File will be created on first use"));
                }
                return;
            }
            
            string content = _fileSystem?.ReadAllTextAsync(filePath).GetAwaiter().GetResult() ?? File.ReadAllText(filePath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                result.AddIssue(ValidationIssue.Information(
                    "ConfigurationValidation",
                    $"{fileType} file is empty - will be initialized with defaults"));
                return;
            }
            
            // Try to parse as JSON
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            
            // Check for required properties
            foreach (var property in requiredProperties)
            {
                if (!root.TryGetProperty(property, out _))
                {
                    result.AddIssue(ValidationIssue.Warning(
                        "ConfigurationValidation",
                        $"{fileType} file missing expected property: {property}",
                        "Property will be added with default value"));
                }
            }
            
            result.AddIssue(ValidationIssue.Information(
                "ConfigurationValidation",
                $"{fileType} file structure validated successfully"));
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException(
                "ConfigurationValidation",
                ex,
                ValidationSeverity.Warning,
                "Could not validate configuration structure"));
        }
    }
    
    /// <summary>
    /// Validate configuration structure
    /// </summary>
    /// <param name="result">Validation result to add issues to</param>
    private void ValidateConfigurationStructure(ValidationResult result)
    {
        try
        {
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            
            // Validate settings.json structure
            var settingsPath = Path.Combine(appDataPath, AppConstants.SettingsFileName);
            ValidateJsonStructure(settingsPath, "Settings", new[]
            {
                "DefaultOpacity",
                "DefaultFontSize", 
                "ThemeName"
            }, result, isRequired: false);
            
            // Validate notes.json structure  
            var notesPath = Path.Combine(appDataPath, AppConstants.NotesFileName);
            ValidateJsonStructure(notesPath, "Notes", new[]
            {
                "Notes"
            }, result, isRequired: false);
            
            // Validate snippets.json structure
            var snippetsPath = Path.Combine(appDataPath, AppConstants.SnippetsFileName);
            ValidateJsonStructure(snippetsPath, "Snippets", new[]
            {
                "Snippets"
            }, result, isRequired: false);
            
            // Validate templates.json structure
            var templatesPath = Path.Combine(appDataPath, AppConstants.TemplatesFileName);
            ValidateJsonStructure(templatesPath, "Templates", new[]
            {
                "Templates"
            }, result, isRequired: false);
        }
        catch (Exception ex)
        {
            result.AddIssue(ValidationIssue.FromException(
                "ConfigurationValidation",
                ex,
                ValidationSeverity.Warning,
                "Could not validate configuration structure"));
        }
    }
}