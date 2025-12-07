using System.Security.Cryptography;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service for encrypting and decrypting data using AES-256 encryption.
/// Uses PBKDF2 for key derivation from passphrase.
/// </summary>
public class EncryptionService : IEncryptionService
{
    // AES-256 requires a 256-bit (32-byte) key
    private const int KeySize = 256;
    private const int KeySizeBytes = KeySize / 8; // 32 bytes
    
    // AES block size is 128 bits (16 bytes)
    private const int BlockSize = 128;
    private const int IvSizeBytes = BlockSize / 8; // 16 bytes
    
    // Salt size for PBKDF2
    private const int SaltSizeBytes = 16;
    
    // Number of iterations for PBKDF2 (OWASP recommends at least 600,000 for SHA-256)
    private const int Iterations = 100000;

    /// <inheritdoc />
    public byte[] Encrypt(byte[] data, string passphrase)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));
        
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));

        // Generate a random salt for key derivation
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        
        // Derive key from passphrase using PBKDF2
        var key = DeriveKeyBytes(passphrase, salt);
        
        // Generate a random IV for this encryption
        var iv = RandomNumberGenerator.GetBytes(IvSizeBytes);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Combine salt + IV + encrypted data
        // Format: [salt (16 bytes)][IV (16 bytes)][encrypted data]
        var result = new byte[SaltSizeBytes + IvSizeBytes + encryptedData.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSizeBytes);
        Buffer.BlockCopy(iv, 0, result, SaltSizeBytes, IvSizeBytes);
        Buffer.BlockCopy(encryptedData, 0, result, SaltSizeBytes + IvSizeBytes, encryptedData.Length);

        return result;
    }

    /// <inheritdoc />
    public byte[] Decrypt(byte[] encryptedData, string passphrase)
    {
        if (encryptedData == null || encryptedData.Length == 0)
            throw new ArgumentException("Encrypted data cannot be null or empty.", nameof(encryptedData));
        
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));

        // Minimum size: salt + IV + at least one block of encrypted data
        var minSize = SaltSizeBytes + IvSizeBytes + 1;
        if (encryptedData.Length < minSize)
            throw new ArgumentException($"Encrypted data is too short. Minimum size is {minSize} bytes.", nameof(encryptedData));

        // Extract salt, IV, and encrypted content
        var salt = new byte[SaltSizeBytes];
        var iv = new byte[IvSizeBytes];
        var cipherText = new byte[encryptedData.Length - SaltSizeBytes - IvSizeBytes];

        Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSizeBytes);
        Buffer.BlockCopy(encryptedData, SaltSizeBytes, iv, 0, IvSizeBytes);
        Buffer.BlockCopy(encryptedData, SaltSizeBytes + IvSizeBytes, cipherText, 0, cipherText.Length);

        // Derive key from passphrase using the extracted salt
        var key = DeriveKeyBytes(passphrase, salt);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    /// <inheritdoc />
    public string DeriveKey(string passphrase, byte[] salt)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));
        
        if (salt == null || salt.Length == 0)
            throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));

        var keyBytes = DeriveKeyBytes(passphrase, salt);
        return Convert.ToBase64String(keyBytes);
    }

    /// <summary>
    /// Derives a key from passphrase using PBKDF2 with SHA-256.
    /// </summary>
    private static byte[] DeriveKeyBytes(string passphrase, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            passphrase,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);
        
        return pbkdf2.GetBytes(KeySizeBytes);
    }
}
