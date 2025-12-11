using System.Reflection;
using DevSticky.Interfaces;
using DevSticky.Services.Fallbacks;

namespace DevSticky.Services;

/// <summary>
/// Service for managing service fallback mechanisms during startup failures
/// Implements Requirements 4.4, 4.5 for service fallback behavior
/// </summary>
public class ServiceFallbackManager : IServiceFallbackManager
{
    private readonly IErrorHandler _errorHandler;
    private readonly Dictionary<Type, Type> _fallbackRegistrations = new();
    private readonly Dictionary<string, string> _embeddedResources = new();
    private readonly HashSet<string> _nonCriticalServices = new();

    public ServiceFallbackManager(IErrorHandler errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        RegisterDefaultFallbacks();
        RegisterEmbeddedResources();
        RegisterNonCriticalServices();
    }

    /// <summary>
    /// Attempt to create a fallback implementation for a failed service
    /// </summary>
    public T? CreateFallbackService<T>(string serviceName, Exception originalException) where T : class
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var serviceType = typeof(T);
            
            if (!_fallbackRegistrations.TryGetValue(serviceType, out var fallbackType))
            {
                return null;
            }

            // Create fallback service instance
            var fallbackInstance = CreateFallbackInstance(fallbackType);
            
            if (fallbackInstance is T typedInstance)
            {
                return typedInstance;
            }

            return null;
        },
        null,
        $"ServiceFallbackManager.CreateFallbackService<{typeof(T).Name}> - {serviceName}");
    }

    /// <summary>
    /// Detect if a service initialization has failed
    /// </summary>
    public bool DetectServiceFailure(Type serviceType, object? serviceInstance)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            // Basic failure detection
            if (serviceInstance == null)
                return true;

            // Check if service implements a health check method
            var healthCheckMethod = serviceType.GetMethod("IsHealthy") ?? 
                                  serviceInstance.GetType().GetMethod("IsHealthy");
            
            if (healthCheckMethod != null && healthCheckMethod.ReturnType == typeof(bool))
            {
                try
                {
                    var isHealthy = (bool)healthCheckMethod.Invoke(serviceInstance, null)!;
                    return !isHealthy;
                }
                catch
                {
                    return true; // If health check throws, consider it failed
                }
            }

            // For IDisposable services, check if they're already disposed
            if (serviceInstance is IDisposable disposable)
            {
                try
                {
                    // Try to access a property to see if it throws ObjectDisposedException
                    var properties = serviceInstance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in properties.Take(1)) // Just check first property
                    {
                        if (prop.CanRead && prop.GetMethod?.IsPublic == true)
                        {
                            prop.GetValue(serviceInstance);
                            break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is ObjectDisposedException)
                {
                    return true;
                }
                catch
                {
                    // Other exceptions don't necessarily indicate disposal
                }
            }

            // For any service, try to access a basic property to check if it's functional
            try
            {
                var properties = serviceInstance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties.Take(1)) // Just check first property
                {
                    if (prop.CanRead && prop.GetMethod?.IsPublic == true)
                    {
                        prop.GetValue(serviceInstance);
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Any exception when accessing basic properties indicates service failure
                return true;
            }

            return false; // Service appears to be working
        },
        true, // Default to failed if detection itself fails
        $"ServiceFallbackManager.DetectServiceFailure - {serviceType.Name}");
    }

    /// <summary>
    /// Get fallback to embedded default resources when external resources fail
    /// </summary>
    public string? GetEmbeddedFallbackResource(string resourceType, string resourceKey)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var key = $"{resourceType}:{resourceKey}";
            return _embeddedResources.TryGetValue(key, out var resource) ? resource : null;
        },
        null,
        $"ServiceFallbackManager.GetEmbeddedFallbackResource - {resourceType}:{resourceKey}");
    }

    /// <summary>
    /// Configure graceful degradation for non-critical services
    /// </summary>
    public ServiceFallbackResult ConfigureGracefulDegradation(string serviceName, int degradationLevel)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            if (!_nonCriticalServices.Contains(serviceName))
            {
                return new ServiceFallbackResult
                {
                    IsSuccessful = false,
                    FallbackType = ServiceFallbackType.GracefulDegradation,
                    ServiceName = serviceName,
                    Message = $"Service '{serviceName}' is not registered as non-critical"
                };
            }

            // Configure degradation based on level
            var message = degradationLevel switch
            {
                0 => $"Service '{serviceName}' disabled completely",
                1 => $"Service '{serviceName}' running with minimal functionality",
                2 => $"Service '{serviceName}' running with reduced functionality",
                3 => $"Service '{serviceName}' running with most features available",
                _ => $"Service '{serviceName}' degradation level {degradationLevel} not recognized"
            };

            return new ServiceFallbackResult
            {
                IsSuccessful = true,
                FallbackType = ServiceFallbackType.GracefulDegradation,
                ServiceName = serviceName,
                Message = message
            };
        },
        new ServiceFallbackResult
        {
            IsSuccessful = false,
            FallbackType = ServiceFallbackType.GracefulDegradation,
            ServiceName = serviceName,
            Message = "Failed to configure graceful degradation"
        },
        $"ServiceFallbackManager.ConfigureGracefulDegradation - {serviceName}");
    }

    /// <summary>
    /// Register a fallback implementation for a service type
    /// </summary>
    public void RegisterFallback<TInterface, TFallback>() 
        where TInterface : class 
        where TFallback : class, TInterface
    {
        _errorHandler.HandleWithFallback(() =>
        {
            _fallbackRegistrations[typeof(TInterface)] = typeof(TFallback);
            return true;
        },
        false,
        $"ServiceFallbackManager.RegisterFallback<{typeof(TInterface).Name}, {typeof(TFallback).Name}>");
    }

    /// <summary>
    /// Check if a fallback is available for a service type
    /// </summary>
    public bool HasFallback<T>() where T : class
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            return _fallbackRegistrations.ContainsKey(typeof(T));
        },
        false,
        $"ServiceFallbackManager.HasFallback<{typeof(T).Name}>");
    }

    /// <summary>
    /// Get all registered fallback services
    /// </summary>
    public Dictionary<Type, Type> GetRegisteredFallbacks()
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            return new Dictionary<Type, Type>(_fallbackRegistrations);
        },
        new Dictionary<Type, Type>(),
        "ServiceFallbackManager.GetRegisteredFallbacks");
    }

    #region Private Helper Methods

    /// <summary>
    /// Register default fallback implementations for critical services
    /// </summary>
    private void RegisterDefaultFallbacks()
    {
        // Register fallbacks for critical services
        _fallbackRegistrations[typeof(INoteService)] = typeof(FallbackNoteService);
        _fallbackRegistrations[typeof(IStorageService)] = typeof(FallbackStorageService);
        _fallbackRegistrations[typeof(IThemeService)] = typeof(FallbackThemeService);
    }

    /// <summary>
    /// Register embedded fallback resources
    /// </summary>
    private void RegisterEmbeddedResources()
    {
        // Theme resources
        _embeddedResources["theme:light"] = GenerateLightThemeXaml();
        _embeddedResources["theme:dark"] = GenerateDarkThemeXaml();
        
        // String resources
        _embeddedResources["strings:WelcomeTitle"] = "Welcome to DevSticky";
        _embeddedResources["strings:WelcomeContent"] = "This is a fallback note created when the application is running in safe mode.";
        _embeddedResources["strings:UntitledNote"] = "Untitled Note";
        _embeddedResources["strings:Error"] = "Error";
        _embeddedResources["strings:Settings"] = "Settings";
        _embeddedResources["strings:Exit"] = "Exit";
        
        // Configuration resources
        _embeddedResources["config:default"] = GenerateDefaultConfigJson();
    }

    /// <summary>
    /// Register non-critical services that can be gracefully degraded
    /// </summary>
    private void RegisterNonCriticalServices()
    {
        _nonCriticalServices.Add("CloudSyncService");
        _nonCriticalServices.Add("HotkeyService");
        _nonCriticalServices.Add("MarkdownService");
        _nonCriticalServices.Add("SnippetService");
        _nonCriticalServices.Add("TemplateService");
        _nonCriticalServices.Add("LinkService");
        _nonCriticalServices.Add("ExportService");
    }

    /// <summary>
    /// Create an instance of a fallback service type
    /// </summary>
    private object? CreateFallbackInstance(Type fallbackType)
    {
        try
        {
            // Try to create with IErrorHandler dependency
            var constructorWithErrorHandler = fallbackType.GetConstructor(new[] { typeof(IErrorHandler) });
            if (constructorWithErrorHandler != null)
            {
                return Activator.CreateInstance(fallbackType, _errorHandler);
            }

            // Try parameterless constructor
            var parameterlessConstructor = fallbackType.GetConstructor(Type.EmptyTypes);
            if (parameterlessConstructor != null)
            {
                return Activator.CreateInstance(fallbackType);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generate basic light theme XAML
    /// </summary>
    private static string GenerateLightThemeXaml()
    {
        return @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Color x:Key=""BackgroundColor"">#FFFFFF</Color>
    <Color x:Key=""ForegroundColor"">#000000</Color>
    <Color x:Key=""BorderColor"">#D3D3D3</Color>
    <Color x:Key=""AccentColor"">#1E90FF</Color>
    
    <SolidColorBrush x:Key=""BackgroundBrush"" Color=""{StaticResource BackgroundColor}"" />
    <SolidColorBrush x:Key=""ForegroundBrush"" Color=""{StaticResource ForegroundColor}"" />
    <SolidColorBrush x:Key=""BorderBrush"" Color=""{StaticResource BorderColor}"" />
    <SolidColorBrush x:Key=""AccentBrush"" Color=""{StaticResource AccentColor}"" />
</ResourceDictionary>";
    }

    /// <summary>
    /// Generate basic dark theme XAML
    /// </summary>
    private static string GenerateDarkThemeXaml()
    {
        return @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Color x:Key=""BackgroundColor"">#202020</Color>
    <Color x:Key=""ForegroundColor"">#FFFFFF</Color>
    <Color x:Key=""BorderColor"">#404040</Color>
    <Color x:Key=""AccentColor"">#1E90FF</Color>
    
    <SolidColorBrush x:Key=""BackgroundBrush"" Color=""{StaticResource BackgroundColor}"" />
    <SolidColorBrush x:Key=""ForegroundBrush"" Color=""{StaticResource ForegroundColor}"" />
    <SolidColorBrush x:Key=""BorderBrush"" Color=""{StaticResource BorderColor}"" />
    <SolidColorBrush x:Key=""AccentBrush"" Color=""{StaticResource AccentColor}"" />
</ResourceDictionary>";
    }

    /// <summary>
    /// Generate default configuration JSON
    /// </summary>
    private static string GenerateDefaultConfigJson()
    {
        return @"{
  ""Language"": ""en"",
  ""ThemeMode"": ""System"",
  ""DefaultOpacity"": 0.9,
  ""AutoSave"": true,
  ""AutoSaveInterval"": 5000,
  ""Hotkeys"": {
    ""NewNoteHotkey"": ""Ctrl+Shift+N"",
    ""ToggleVisibilityHotkey"": ""Ctrl+Shift+H"",
    ""QuickCaptureHotkey"": ""Ctrl+Shift+Q"",
    ""SnippetBrowserHotkey"": ""Ctrl+Shift+S""
  }
}";
    }

    #endregion
}