using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Registry for cloud storage providers following the Open/Closed Principle.
/// Allows registration of new providers without modifying existing code.
/// </summary>
public interface ICloudProviderRegistry
{
    /// <summary>
    /// Registers a cloud storage provider factory.
    /// </summary>
    /// <param name="provider">The cloud provider type.</param>
    /// <param name="factory">Factory function to create provider instances.</param>
    void RegisterProvider(CloudProvider provider, Func<ICloudStorageProvider> factory);

    /// <summary>
    /// Creates a cloud storage provider instance.
    /// </summary>
    /// <param name="provider">The cloud provider type to create.</param>
    /// <returns>A new instance of the cloud storage provider.</returns>
    /// <exception cref="ArgumentException">Thrown when provider is not registered.</exception>
    ICloudStorageProvider CreateProvider(CloudProvider provider);

    /// <summary>
    /// Gets all available registered cloud providers.
    /// </summary>
    /// <returns>Collection of registered cloud provider types.</returns>
    IEnumerable<CloudProvider> GetAvailableProviders();

    /// <summary>
    /// Checks if a provider is registered.
    /// </summary>
    /// <param name="provider">The cloud provider type to check.</param>
    /// <returns>True if the provider is registered, false otherwise.</returns>
    bool IsProviderRegistered(CloudProvider provider);
}
