using System.Collections.Concurrent;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Registry for cloud storage providers following the Open/Closed Principle.
/// Allows registration of new providers without modifying existing code.
/// </summary>
public class CloudProviderRegistry : ICloudProviderRegistry
{
    private readonly ConcurrentDictionary<CloudProvider, Func<ICloudStorageProvider>> _providers = new();



    /// <inheritdoc />
    public void RegisterProvider(CloudProvider provider, Func<ICloudStorageProvider> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        _providers[provider] = factory;
    }

    /// <inheritdoc />
    public ICloudStorageProvider CreateProvider(CloudProvider provider)
    {
        if (!_providers.TryGetValue(provider, out var factory))
        {
            throw new ArgumentException($"Cloud provider '{provider}' is not registered.", nameof(provider));
        }

        return factory();
    }

    /// <inheritdoc />
    public IEnumerable<CloudProvider> GetAvailableProviders()
    {
        return _providers.Keys.ToList();
    }

    /// <inheritdoc />
    public bool IsProviderRegistered(CloudProvider provider)
    {
        return _providers.ContainsKey(provider);
    }
}
