namespace DevSticky.Interfaces;

/// <summary>
/// Service for encrypting and decrypting data using AES-256 encryption.
/// Used for cloud synchronization to protect note content.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts data using AES-256 encryption with the provided passphrase.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="passphrase">The passphrase used to derive the encryption key.</param>
    /// <returns>The encrypted data including IV and salt.</returns>
    byte[] Encrypt(byte[] data, string passphrase);

    /// <summary>
    /// Decrypts data that was encrypted using the Encrypt method.
    /// </summary>
    /// <param name="encryptedData">The encrypted data including IV and salt.</param>
    /// <param name="passphrase">The passphrase used to derive the decryption key.</param>
    /// <returns>The decrypted data.</returns>
    byte[] Decrypt(byte[] encryptedData, string passphrase);

    /// <summary>
    /// Derives a cryptographic key from a passphrase using PBKDF2.
    /// </summary>
    /// <param name="passphrase">The passphrase to derive the key from.</param>
    /// <param name="salt">The salt to use for key derivation.</param>
    /// <returns>The derived key as a base64 string.</returns>
    string DeriveKey(string passphrase, byte[] salt);
}
