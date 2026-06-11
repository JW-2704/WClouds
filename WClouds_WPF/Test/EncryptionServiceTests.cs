using System;
using System.Text;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

public class EncryptionServiceTests
{
    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalData()
    {
        byte[] original = Encoding.UTF8.GetBytes("Hello, WClouds!");
        var (encrypted, nonce) = EncryptionService.Encrypt(original);
        byte[] decrypted = EncryptionService.Decrypt(encrypted, nonce);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesNonEmptyNonce()
    {
        var (_, nonce) = EncryptionService.Encrypt(new byte[] { 1, 2, 3 });

        Assert.False(string.IsNullOrWhiteSpace(nonce));
        // Nonce is base64 of 12 bytes → 16 base64 chars
        Assert.Equal(16, nonce.Length);
    }

    [Fact]
    public void Encrypt_ProducesOutputLongerThanInput()
    {
        byte[] input = new byte[100];
        var (encrypted, _) = EncryptionService.Encrypt(input);

        // Output = ciphertext (same length as input) + 16-byte GCM tag
        Assert.Equal(input.Length + 16, encrypted.Length);
    }

    [Fact]
    public void Decrypt_WithWrongNonce_Throws()
    {
        byte[] original = Encoding.UTF8.GetBytes("secret data");
        var (encrypted, _) = EncryptionService.Encrypt(original);

        // Use a different (all-zeros) nonce → authentication should fail
        string wrongNonce = Convert.ToBase64String(new byte[12]);

        Assert.ThrowsAny<Exception>(() => EncryptionService.Decrypt(encrypted, wrongNonce));
    }

    [Fact]
    public void Encrypt_EmptyInput_EncryptsAndDecryptsSuccessfully()
    {
        byte[] empty = Array.Empty<byte>();
        var (encrypted, nonce) = EncryptionService.Encrypt(empty);
        byte[] decrypted = EncryptionService.Decrypt(encrypted, nonce);

        Assert.Empty(decrypted);
    }

    [Fact]
    public void TwoEncryptCalls_ProduceDifferentNonces()
    {
        byte[] data = Encoding.UTF8.GetBytes("test");
        var (_, nonce1) = EncryptionService.Encrypt(data);
        var (_, nonce2) = EncryptionService.Encrypt(data);

        // Nonces are random; collision probability is negligible
        Assert.NotEqual(nonce1, nonce2);
    }
}
