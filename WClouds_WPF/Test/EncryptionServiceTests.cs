using System;
using System.Text;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

// AI Agent: EncryptionService wurde von "ein statischer Key pro Geraet" auf
// "DEK pro Datei + RSA-Keypair pro Account" umgebaut - alle Tests hier
// nehmen jetzt einen expliziten DEK-Parameter, und es kommen Tests fuer
// WrapKey/UnwrapKey (das eigentliche Sharing-Fundament) dazu.
public class EncryptionServiceTests
{
    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalData()
    {
        byte[] dek = EncryptionService.GenerateDek();
        byte[] original = Encoding.UTF8.GetBytes("Hello, WClouds!");
        var (encrypted, nonce) = EncryptionService.Encrypt(original, dek);
        byte[] decrypted = EncryptionService.Decrypt(encrypted, dek, nonce);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesNonEmptyNonce()
    {
        byte[] dek = EncryptionService.GenerateDek();
        var (_, nonce) = EncryptionService.Encrypt(new byte[] { 1, 2, 3 }, dek);

        Assert.False(string.IsNullOrWhiteSpace(nonce));
        // AI Agent: Nonce ist Hex (Convert.ToHexString), nicht Base64 -
        // 12 Bytes -> 24 Hex-Zeichen. Die alte Assertion (16) passte schon
        // vorher nicht zur tatsaechlichen Hex-Kodierung.
        Assert.Equal(24, nonce.Length);
    }

    [Fact]
    public void Encrypt_ProducesOutputLongerThanInput()
    {
        byte[] dek = EncryptionService.GenerateDek();
        byte[] input = new byte[100];
        var (encrypted, _) = EncryptionService.Encrypt(input, dek);

        // Output = ciphertext (same length as input) + 16-byte GCM tag
        Assert.Equal(input.Length + 16, encrypted.Length);
    }

    [Fact]
    public void Decrypt_WithWrongNonce_Throws()
    {
        byte[] dek = EncryptionService.GenerateDek();
        byte[] original = Encoding.UTF8.GetBytes("secret data");
        var (encrypted, _) = EncryptionService.Encrypt(original, dek);

        // Use a different (all-zeros) nonce → authentication should fail
        string wrongNonce = Convert.ToHexString(new byte[12]);

        Assert.ThrowsAny<Exception>(() => EncryptionService.Decrypt(encrypted, dek, wrongNonce));
    }

    [Fact]
    public void Decrypt_WithWrongDek_Throws()
    {
        byte[] dek = EncryptionService.GenerateDek();
        byte[] otherDek = EncryptionService.GenerateDek();
        byte[] original = Encoding.UTF8.GetBytes("secret data");
        var (encrypted, nonce) = EncryptionService.Encrypt(original, dek);

        Assert.ThrowsAny<Exception>(() => EncryptionService.Decrypt(encrypted, otherDek, nonce));
    }

    [Fact]
    public void Encrypt_EmptyInput_EncryptsAndDecryptsSuccessfully()
    {
        byte[] dek = EncryptionService.GenerateDek();
        byte[] empty = Array.Empty<byte>();
        var (encrypted, nonce) = EncryptionService.Encrypt(empty, dek);
        byte[] decrypted = EncryptionService.Decrypt(encrypted, dek, nonce);

        Assert.Empty(decrypted);
    }

    [Fact]
    public void TwoEncryptCalls_ProduceDifferentNonces()
    {
        byte[] dek = EncryptionService.GenerateDek();
        byte[] data = Encoding.UTF8.GetBytes("test");
        var (_, nonce1) = EncryptionService.Encrypt(data, dek);
        var (_, nonce2) = EncryptionService.Encrypt(data, dek);

        // Nonces are random; collision probability is negligible
        Assert.NotEqual(nonce1, nonce2);
    }

    [Fact]
    public void GenerateDek_ProducesDifferentKeysEachTime()
    {
        byte[] dek1 = EncryptionService.GenerateDek();
        byte[] dek2 = EncryptionService.GenerateDek();

        Assert.Equal(32, dek1.Length);
        Assert.NotEqual(dek1, dek2);
    }

    [Fact]
    public void WrapKey_ThenUnwrapKey_ReturnsOriginalDek()
    {
        string tempId = $"test-{Guid.NewGuid()}";
        string publicKey = EncryptionService.GenerateAndStoreNewKeypair(tempId);
        int fakeUserId = Math.Abs(tempId.GetHashCode());
        EncryptionService.PersistKeypairForUser(tempId, fakeUserId);
        EncryptionService.Initialize(fakeUserId);

        byte[] dek = EncryptionService.GenerateDek();
        string wrapped = EncryptionService.WrapKey(dek, publicKey);
        byte[] unwrapped = EncryptionService.UnwrapKey(wrapped);

        Assert.Equal(dek, unwrapped);
    }

    [Fact]
    public void Initialize_WithoutExistingKey_Throws()
    {
        int neverRegisteredUserId = -Math.Abs(Guid.NewGuid().GetHashCode());

        Assert.Throws<InvalidOperationException>(() => EncryptionService.Initialize(neverRegisteredUserId));
    }
}
