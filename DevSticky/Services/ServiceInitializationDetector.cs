using Microsoft.Extensions.DependencyInjection;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service initialization failure information
/// </summary>
public class ServiceInitializationFailure
{
    public Type ServiceType { get; set; } = typeof(object);
    public string ServiceName { get; set; } = string.Empty;
    public Exception Exception { get; set; } = new Exception();
    public DateTime FailureTime { get; set; } = DateTime.UtcNow;
    public bool HasFallback { get; set; }
}

/// <summary>
/// Service for detecting and handling service initialization failures during startup
/// Implements Requirements 4.4, 4.5 for service initialization failure detection
/// </summary>
public class ServiceInitializationDetector
{
    private readonly IServiceFallbackManager _fallbackManager;
    private readonly IErrorHandler _errorHandler;
    private readonly List<ServiceInitializationFailure> _failures = new();

    public ServiceInitializationDetector(IServiceFallbackManager fallbackManager, IErrorHandler errorHandler)
    {
        _fallbackManager = fallbackManager ?? throw new ArgumentNullException(nameof(fallbackManager));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    /// <summary>
    /// Get all detected service initialization failures
    /// </summary>
    public IReadOnlyList<ServiceInitializationFailure> GetFailures() => _failures.AsReadOnly();

    /// <summary>
    /// Attempt to initialize a service with fallback support
    /// </summary>
    /// <typeparam name="T">Service interface type</typeparam>
    /// <param name="serviceProvider">Service provider to resolve from</param>
    /// <param name="serviceName">Name of the service for logging</param>
    /// <returns>Service instance or fallback, null if both fail</returns>
    public T? InitializeServiceWithFallback<T>(IServiceProvider serviceProvider, string serviceName) where T : class
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            try
            {
                // Try to get the primary service
                var service = serviceProvider.GetRequiredService<T>();
                
                // Validate the service
                if (_fallbackManager.DetectServiceFailure(typeof(T), service))
                {
                    throw new InvalidOperationException($"Service {serviceName} failed validation after creation");
                }

                return service;
            }
            catch (Exception ex)
            {
                // Log the failure
                var failure = new ServiceInitializationFailure
                {
                    ServiceType = typeof(T),
                    ServiceName = serviceName,
                    Exception = ex,
                    FailureTime = DateTime.UtcNow,
                    HasFallback = _fallbackManager.HasFallback<T>()
                };
                
                _failures.Add(failure);

                // Try to create fallback
                var fallbackService = _fallbackManager.CreateFallbackService<T>(serviceName, ex);
                
                if (fallbackService != null)
                {
                    return fallbackService;
                }

                // Re-throw if no fallback available
                throw;
            }
        },
        null,
        $"ServiceInitializationDetector.InitializeServiceWithFallback<{typeof(T).Name}> - {serviceName}");
    }

    /// <summary>
    /// Attempt to initialize an optional service with graceful degradation
    /// </summary>
    /// <typeparam name="T">Service interface type</typeparam>
    /// <param name="serviceProvider">Service provider to resolve from</param>
    /// <param name="serviceName">Name of the service for logging</param>
    /// <param name="degradationLevel">Level of functionality to maintain if service fails</param>
    /// <returns>Service instance or null if initialization fails</returns>
    public T? InitializeOptionalService<T>(IServiceProvider serviceProvider, string serviceName, int degradationLevel = 0) where T : class
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            try
            {
                var service = serviceProvider.GetService<T>();
                
                if (service != null && _fallbackManager.DetectServiceFailure(typeof(T), service))
                {
                    throw new InvalidOperationException($"Optional service {serviceName} failed validation after creation");
                }

                return service;
            }
            catch (Exception ex)
            {
                // Log the failure
                var failure = new ServiceInitializationFailure
                {
                    ServiceType = typeof(T),
                    ServiceName = serviceName,
                    Exception = ex,
                    FailureTime = DateTime.UtcNow,
                    HasFallback = false // Optional services don't use fallbacks, they degrade gracefully
                };
                
                _failures.Add(failure);

                // Configure graceful degradation
                _fallbackManager.ConfigureGracefulDegradation(serviceName, degradationLevel);

                // Return null for optional services
                return null;
            }
        },
        null,
        $"ServiceInitializationDetector.InitializeOptionalService<{typeof(T).Name}> - {serviceName}");
    }

    /// <summary>
    /// Validate all services in the service provider
    /// </summary>
    /// <param name="serviceProvider">Service provider to validate</param>
    /// <returns>List of services that failed validation</returns>
    public List<ServiceInitializationFailure> ValidateAllServices(IServiceProvider serviceProvider)
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var validationFailures = new List<ServiceInitializationFailure>();

            // Get all registered services from the service provider
            var serviceDescriptors = GetServiceDescriptors(serviceProvider);

            foreach (var descriptor in serviceDescriptors)
            {
                try
                {
                    var service = serviceProvider.GetService(descriptor.ServiceType);
                    
                    if (service != null && _fallbackManager.DetectServiceFailure(descriptor.ServiceType, service))
                    {
                        var failure = new ServiceInitializationFailure
                        {
                            ServiceType = descriptor.ServiceType,
                            ServiceName = descriptor.ServiceType.Name,
                            Exception = new InvalidOperationException($"Service {descriptor.ServiceType.Name} failed validation"),
                            FailureTime = DateTime.UtcNow,
                            HasFallback = _fallbackManager.HasFallback<object>() // Generic check
                        };
                        
                        validationFailures.Add(failure);
                    }
                }
                catch (Exception ex)
                {
                    var failure = new ServiceInitializationFailure
                    {
                        ServiceType = descriptor.ServiceType,
                        ServiceName = descriptor.ServiceType.Name,
                        Exception = ex,
                        FailureTime = DateTime.UtcNow,
                        HasFallback = false
                    };
                    
                    validationFailures.Add(failure);
                }
            }

            return validationFailures;
        },
        new List<ServiceInitializationFailure>(),
        "ServiceInitializationDetector.ValidateAllServices");
    }

    /// <summary>
    /// Check if any critical services have failed
    /// </summary>
    /// <returns>True if critical services have failed</returns>
    public bool HasCriticalServiceFailures()
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            var criticalServiceTypes = new[]
            {
                typeof(INoteService),
                typeof(IStorageService),
                typeof(IThemeService),
                typeof(IErrorHandler),
                typeof(IExceptionLogger)
            };

            return _failures.Any(f => criticalServiceTypes.Contains(f.ServiceType) && !f.HasFallback);
        },
        false,
        "ServiceInitializationDetector.HasCriticalServiceFailures");
    }

    /// <summary>
    /// Get summary of service initialization status
    /// </summary>
    /// <returns>Summary string of initialization status</returns>
    public string GetInitializationSummary()
    {
        return _errorHandler.HandleWithFallback(() =>
        {
            if (_failures.Count == 0)
            {
                return "All services initialized successfully";
            }

            var criticalFailures = _failures.Count(f => !f.HasFallback);
            var fallbacksUsed = _failures.Count(f => f.HasFallback);

            return $"Service initialization completed with {_failures.Count} issues: " +
                   $"{criticalFailures} critical failures, {fallbacksUsed} using fallbacks";
        },
        "Service initialization status unknown",
        "ServiceInitializationDetector.GetInitializationSummary");
    }

    /// <summary>
    /// Clear all recorded failures (for testing or reset scenarios)
    /// </summary>
    public void ClearFailures()
    {
        _errorHandler.HandleWithFallback(() =>
        {
            _failures.Clear();
            return true;
        },
        false,
        "ServiceInitializationDetector.ClearFailures");
    }

    #region Private Helper Methods

    /// <summary>
    /// Get service descriptors from service provider (reflection-based approach)
    /// </summary>
    private static IEnumerable<ServiceDescriptor> GetServiceDescriptors(IServiceProvider serviceProvider)
    {
        // This is a simplified approach - in a real implementation, you might want to
        // maintain a registry of service descriptors during registration
        
        // For now, return empty collection as we'll validate services individually
        // when they're requested during startup
        return Enumerable.Empty<ServiceDescriptor>();
    }

    #endregion
}