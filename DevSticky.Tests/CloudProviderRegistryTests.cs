using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for CloudProviderRegistry.
/// Validates the Open/Closed Principle implementation for cloud providers.
/// </summary>
public class CloudProviderRegistryTests
{
    [Fact]
    public void RegisterProvider_ShouldAllowProviderRegistration()
    {
        // Arrange
        var registry = new CloudProviderRegistry();
        var mockProvider = new MockCloudStorageProvider();

        // Act
        registry.RegisterProvider(CloudProvider.OneDrive, () => mockProvider);

        // Assert
        Assert.True(registry.IsProviderRegistered(CloudProvider.OneDrive));
    }

    [Fact]
    public void CreateProvider_ShouldReturnRegisteredProvider()
    {
        // Arrange
        var registry = new CloudProviderRegistry();
        var mockProvider = new MockCloudStorageProvider();
        registry.RegisterProvider(CloudProvider.OneDrive, () => mockProvider);

        // Act
        var provider = registry.CreateProvider(CloudProvider.OneDrive);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal(mockProvider.ProviderName, provider.ProviderName);
    }

    [Fact]
    public void CreateProvider_ShouldThrowForUnregisteredProvider()
    {
        // Arrange
        var registry = new CloudProviderRegistry();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.CreateProvider(CloudProvider.OneDrive));
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnAllRegisteredProviders()
    {
        // Arrange
        var registry = new CloudProviderRegistry();
        registry.RegisterProvider(CloudProvider.OneDrive, () => new MockCloudStorageProvider());
        registry.RegisterProvider(CloudProvider.GoogleDrive, () => new MockCloudStorageProvider());

        // Act
        var providers = registry.GetAvailableProviders().ToList();

        // Assert
        Assert.Equal(2, providers.Count);
        Assert.Contains(CloudProvider.OneDrive, providers);
        Assert.Contains(CloudProvider.GoogleDrive, providers);
    }

    [Fact]
    public void IsProviderRegistered_ShouldReturnFalseForUnregisteredProvider()
    {
        // Arrange
        var registry = new CloudProviderRegistry();

        // Act
        var isRegistered = registry.IsProviderRegistered(CloudProvider.OneDrive);

        // Assert
        Assert.False(isRegistered);
    }

    [Fact]
    public void RegisterProvider_ShouldAllowOverwritingExistingProvider()
    {
        // Arrange
        var registry = new CloudProviderRegistry();
        var provider1 = new MockCloudStorageProvider { ProviderName = "Provider1" };
        var provider2 = new MockCloudStorageProvider { ProviderName = "Provider2" };

        // Act
        registry.RegisterProvider(CloudProvider.OneDrive, () => provider1);
        registry.RegisterProvider(CloudProvider.OneDrive, () => provider2);
        var result = registry.CreateProvider(CloudProvider.OneDrive);

        // Assert
        Assert.Equal("Provider2", result.ProviderName);
    }

    [Fact]
    public void RegisterProvider_ShouldThrowForNullFactory()
    {
        // Arrange
        var registry = new CloudProviderRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            registry.RegisterProvider(CloudProvider.OneDrive, null!));
    }

    [Fact]
    public void CreateProvider_ShouldCreateNewInstanceEachTime()
    {
        // Arrange
        var registry = new CloudProviderRegistry();
        registry.RegisterProvider(CloudProvider.OneDrive, () => new MockCloudStorageProvider());

        // Act
        var provider1 = registry.CreateProvider(CloudProvider.OneDrive);
        var provider2 = registry.CreateProvider(CloudProvider.OneDrive);

        // Assert
        Assert.NotSame(provider1, provider2);
    }

    /// <summary>
    /// Mock cloud storage provider for testing.
    /// </summary>
    private class MockCloudStorageProvider : ICloudStorageProvider
    {
        public string ProviderName { get; set; } = "Mock";
        public bool IsAuthenticated => false;

        public Task<bool> AuthenticateAsync() => Task.FromResult(false);
        public Task SignOutAsync() => Task.CompletedTask;
        public Task<string?> UploadFileAsync(string remotePath, byte[] content) => Task.FromResult<string?>(null);
        public Task<byte[]?> DownloadFileAsync(string remotePath) => Task.FromResult<byte[]?>(null);
        public Task<bool> DeleteFileAsync(string remotePath) => Task.FromResult(false);
        public Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string remotePath) => 
            Task.FromResult<IReadOnlyList<CloudFileInfo>>(Array.Empty<CloudFileInfo>());
        public Task<CloudFileInfo?> GetFileInfoAsync(string remotePath) => Task.FromResult<CloudFileInfo?>(null);
        public Task<bool> CreateFolderAsync(string remotePath) => Task.FromResult(false);
        public void Dispose() { }
    }
}
