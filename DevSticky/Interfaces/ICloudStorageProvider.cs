namespace DevSticky.Interfaces;

/// <summary>
/// Information about a file stored in cloud storage.
/// </summary>
public class CloudFileInfo
{
    /// <summary>
    /// The name of the file.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The full path of the file in cloud storage.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// The last modified date of the file.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// The ETag or version identifier for the file.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Whether this item is a folder.
    /// </summary>
    public bool IsFolder { get; set; }
}

/// <summary>
/// Interface for cloud storage providers (OneDrive, Google Drive).
/// Provides methods for authentication and file operations.
/// </summary>
public interface ICloudStorageProvider : IDisposable
{
    /// <summary>
    /// Gets the name of the cloud provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether the provider is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Authenticates with the cloud provider using OAuth 2.0.
    /// </summary>
    /// <returns>True if authentication was successful, false otherwise.</returns>
    Task<bool> AuthenticateAsync();

    /// <summary>
    /// Signs out from the cloud provider and clears stored tokens.
    /// </summary>
    Task SignOutAsync();

    /// <summary>
    /// Uploads a file to cloud storage.
    /// </summary>
    /// <param name="remotePath">The path in cloud storage where the file should be stored.</param>
    /// <param name="content">The file content as bytes.</param>
    /// <returns>The ETag/version of the uploaded file, or null if upload failed.</returns>
    Task<string?> UploadFileAsync(string remotePath, byte[] content);

    /// <summary>
    /// Downloads a file from cloud storage.
    /// </summary>
    /// <param name="remotePath">The path of the file in cloud storage.</param>
    /// <returns>The file content as bytes, or null if download failed.</returns>
    Task<byte[]?> DownloadFileAsync(string remotePath);

    /// <summary>
    /// Deletes a file from cloud storage.
    /// </summary>
    /// <param name="remotePath">The path of the file to delete.</param>
    /// <returns>True if deletion was successful, false otherwise.</returns>
    Task<bool> DeleteFileAsync(string remotePath);

    /// <summary>
    /// Lists files in a cloud storage folder.
    /// </summary>
    /// <param name="remotePath">The path of the folder to list. Use empty string for root.</param>
    /// <returns>A list of files and folders in the specified path.</returns>
    Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string remotePath);

    /// <summary>
    /// Gets information about a specific file.
    /// </summary>
    /// <param name="remotePath">The path of the file.</param>
    /// <returns>File information, or null if the file doesn't exist.</returns>
    Task<CloudFileInfo?> GetFileInfoAsync(string remotePath);

    /// <summary>
    /// Creates a folder in cloud storage if it doesn't exist.
    /// </summary>
    /// <param name="remotePath">The path of the folder to create.</param>
    /// <returns>True if the folder was created or already exists, false otherwise.</returns>
    Task<bool> CreateFolderAsync(string remotePath);
}
