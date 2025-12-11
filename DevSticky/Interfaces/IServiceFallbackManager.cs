using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service fallback types for different failure scenarios
/// </summary>
public enum ServiceFallbackType
{
    FallbackImplementation,
    EmbeddedResources,
    GracefulDegradation,
    MinimalFunctionality
}

/// <summary>
/// Result of a service fallback operation
/// </summary>
public class ServiceFallbackResult
{
    public bool IsSuccessful { get; set; }
    public ServiceFallbackType FallbackType { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service for managing service fallback mechanisms during startup failures
/// </summary>
public interface IServiceFallbackManager
{
    /// <summary>
    /// Attempt to create a fallback implementation for a failed service
    /// </summary>
    /// <typeparam name="T">Service interface type</typeparam>
    /// <param name="serviceName">Name of the failed service</param>
    /// <param name="originalException">Exception that caused the service failure</param>
    /// <returns>Fallback service instance or null if no fallback available</returns>
    T? CreateFallbackService<T>(string serviceName, Exception originalException) where T : class;

    /// <summary>
    /// Detect if a service initialization has failed
    /// </summary>
    /// <param name="serviceType">Type of service to check</param>
    /// <param name="serviceInstance">Service instance to validate</param>
    /// <returns>True if service failed to initialize properly</returns>
    bool DetectServiceFailure(Type serviceType, object? serviceInstance);

    /// <summary>
    /// Get fallback to embedded default resources when external resources fail
    /// </summary>
    /// <param name="resourceType">Type of resource (theme, strings, etc.)</param>
    /// <param name="resourceKey">Key or identifier for the resource</param>
    /// <returns>Embedded resource content or null if not available</returns>
    string? GetEmbeddedFallbackResource(string resourceType, string resourceKey);

    /// <summary>
    /// Configure graceful degradation for non-critical services
    /// </summary>
    /// <param name="serviceName">Name of the non-critical service</param>
    /// <param name="degradationLevel">Level of functionality to maintain</param>
    /// <returns>Result of degradation configuration</returns>
    ServiceFallbackResult ConfigureGracefulDegradation(string serviceName, int degradationLevel);

    /// <summary>
    /// Register a fallback implementation for a service type
    /// </summary>
    /// <typeparam name="TInterface">Service interface</typeparam>
    /// <typeparam name="TFallback">Fallback implementation</typeparam>
    void RegisterFallback<TInterface, TFallback>() 
        where TInterface : class 
        where TFallback : class, TInterface;

    /// <summary>
    /// Check if a fallback is available for a service type
    /// </summary>
    /// <typeparam name="T">Service interface type</typeparam>
    /// <returns>True if fallback is available</returns>
    bool HasFallback<T>() where T : class;

    /// <summary>
    /// Get all registered fallback services
    /// </summary>
    /// <returns>Dictionary of service types and their fallback types</returns>
    Dictionary<Type, Type> GetRegisteredFallbacks();
}