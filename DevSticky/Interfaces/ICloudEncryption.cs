namespace DevSticky.Interfaces;

/// <summary>
/// Interface for managing encryption settings for cloud sync.
/// </summary>
public interface ICloudEncryption
{
    /// <summary>
    /// Sets the encryption passphrase for cloud sync.
    /// </summary>
    /// <param name="passphrase">The passphrase to use for encryption.</param>
    void SetEncryptionPassphrase(string passphrase);
}
