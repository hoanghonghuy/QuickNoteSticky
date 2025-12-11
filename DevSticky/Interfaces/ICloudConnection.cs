using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for cloud provider connection management.
/// Handles authentication and connection lifecycle.
/// </summary>
public interface ICloudConnection
{
    /// <summary>
    /// Gets the currently connected cloud provider, or null if not connected.
    /// </summary>
    CloudProvider? CurrentProvider { get; }

    /// <summary>
    /// Gets the current sync status.
    /// </summary>
    SyncStatus Status { get; }

    /// <summary>
    /// Connects to a cloud provider and authenticates.
    /// </summary>
    /// <param name="provider">The cloud provider to connect to.</param>
    /// <returns>True if connection was successful, false otherwise.</returns>
    Task<bool> ConnectAsync(CloudProvider provider);

    /// <summary>
    /// Disconnects from the current cloud provider.
    /// </summary>
    Task DisconnectAsync();
}
