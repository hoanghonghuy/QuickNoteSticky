using DevSticky.Interfaces;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for encryption service.
/// **Feature: devsticky-v2, Property 18: Encryption round-trip**
/// **Validates: Requirements 5.11**
/// </summary>
public class EncryptionPropertyTests
{
    private readonly IEncryptionService _encryptionService;

    public EncryptionPropertyTests()
    {
        _encryptionService = new EncryptionService();
    }

    /// <summary>
    /// Property 18: Encryption round-trip
    /// For any note content and passphrase, encrypting then decrypting 
    /// should return the original content exactly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Encrypt_ThenDecrypt_ShouldReturnOriginalData()
    {
        return Prop.ForAll(
            DataGenerator(),
            PassphraseGenerator(),
            (data, passphrase) =>
            {
                var encrypted = _encryptionService.Encrypt(data, passphrase);
                var decrypted = _encryptionService.Decrypt(encrypted, passphrase);

                return data.SequenceEqual(decrypted);
            });
    }

    /// <summary>
    /// Property: Encrypted data should be different from original data
    /// (unless data is very small and happens to match by chance, which is extremely unlikely)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Encrypt_ShouldProduceDifferentOutput()
    {
        return Prop.ForAll(
            DataGenerator(),
            PassphraseGenerator(),
            (data, passphrase) =>
            {
                var encrypted = _encryptionService.Encrypt(data, passphrase);

                // Encrypted data should be larger (includes salt + IV)
                // and should not equal the original data
                return encrypted.Length > data.Length && !data.SequenceEqual(encrypted);
            });
    }

    /// <summary>
    /// Property: Same data encrypted twice should produce different ciphertext
    /// (due to random IV and salt)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Encrypt_SameDataTwice_ShouldProduceDifferentCiphertext()
    {
        return Prop.ForAll(
            DataGenerator(),
            PassphraseGenerator(),
            (data, passphrase) =>
            {
                var encrypted1 = _encryptionService.Encrypt(data, passphrase);
                var encrypted2 = _encryptionService.Encrypt(data, passphrase);

                // Due to random IV and salt, encrypting the same data twice
                // should produce different ciphertext
                return !encrypted1.SequenceEqual(encrypted2);
            });
    }

    /// <summary>
    /// Property: Decryption with wrong passphrase should fail
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Decrypt_WithWrongPassphrase_ShouldFailOrProduceDifferentData()
    {
        return Prop.ForAll(
            DataGenerator(),
            PassphraseGenerator(),
            PassphraseGenerator(),
            (data, correctPassphrase, wrongPassphrase) =>
            {
                // Skip if passphrases happen to be the same
                if (correctPassphrase == wrongPassphrase)
                    return true;

                var encrypted = _encryptionService.Encrypt(data, correctPassphrase);

                try
                {
                    var decrypted = _encryptionService.Decrypt(encrypted, wrongPassphrase);
                    // If decryption succeeds (unlikely), the data should be different
                    return !data.SequenceEqual(decrypted);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    // Expected: decryption should fail with wrong passphrase
                    return true;
                }
            });
    }

    /// <summary>
    /// Property: Key derivation should be deterministic for same passphrase and salt
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeriveKey_SameInputs_ShouldProduceSameKey()
    {
        return Prop.ForAll(
            PassphraseGenerator(),
            SaltGenerator(),
            (passphrase, salt) =>
            {
                var key1 = _encryptionService.DeriveKey(passphrase, salt);
                var key2 = _encryptionService.DeriveKey(passphrase, salt);

                return key1 == key2;
            });
    }

    /// <summary>
    /// Property: Different salts should produce different keys
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeriveKey_DifferentSalts_ShouldProduceDifferentKeys()
    {
        return Prop.ForAll(
            PassphraseGenerator(),
            SaltGenerator(),
            SaltGenerator(),
            (passphrase, salt1, salt2) =>
            {
                // Skip if salts happen to be the same
                if (salt1.SequenceEqual(salt2))
                    return true;

                var key1 = _encryptionService.DeriveKey(passphrase, salt1);
                var key2 = _encryptionService.DeriveKey(passphrase, salt2);

                return key1 != key2;
            });
    }

    /// <summary>
    /// Generates random byte arrays for testing (simulating note content)
    /// </summary>
    private static Arbitrary<byte[]> DataGenerator()
    {
        var gen = from size in Gen.Choose(1, 1000)
                  from bytes in Gen.ArrayOf(size, Arb.Generate<byte>())
                  select bytes;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates random passphrases for testing
    /// </summary>
    private static Arbitrary<string> PassphraseGenerator()
    {
        var gen = from length in Gen.Choose(8, 32)
                  from chars in Gen.ArrayOf(length, Gen.Elements(
                      "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*".ToCharArray()))
                  select new string(chars);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates random salt bytes for testing
    /// </summary>
    private static Arbitrary<byte[]> SaltGenerator()
    {
        var gen = Gen.ArrayOf(16, Arb.Generate<byte>());
        return Arb.From(gen);
    }
}
