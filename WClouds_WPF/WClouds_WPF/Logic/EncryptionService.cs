// KI: Neue Datei – übernimmt die gesamte Ver- und Entschlüsselung (AES-256-GCM)
using System;
using System.Security.Cryptography;

namespace WClouds_WPF.Logic
{
    public static class EncryptionService
    {
        // KI: Key wird einmalig pro App-Start generiert und im Speicher gehalten
        // In einer echten App: Key sicher speichern (z.B. Windows Credential Store)
        private static readonly byte[] Key = GenerateKey();

        private static byte[] GenerateKey()
        {
            byte[] key = new byte[32]; // 256 bit
            RandomNumberGenerator.Fill(key);
            return key;
        }

        // KI: Datei verschlüsseln – gibt (verschlüsselte Bytes, nonce als Base64) zurück
        public static (byte[] EncryptedData, string NonceBase64) Encrypt(byte[] plainData)
        {
            byte[] nonce = new byte[12]; // 96 bit Nonce für AES-GCM
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherText = new byte[plainData.Length];
            byte[] tag = new byte[16]; // Authentifizierungs-Tag

            using AesGcm aes = new AesGcm(Key, 16);
            aes.Encrypt(nonce, plainData, cipherText, tag);

            // KI: Tag hinten anhängen damit wir beim Entschlüsseln alles haben
            byte[] encryptedWithTag = new byte[cipherText.Length + tag.Length];
            Buffer.BlockCopy(cipherText, 0, encryptedWithTag, 0, cipherText.Length);
            Buffer.BlockCopy(tag, 0, encryptedWithTag, cipherText.Length, tag.Length);

            return (encryptedWithTag, Convert.ToBase64String(nonce));
        }

        // KI: Datei entschlüsseln – braucht verschlüsselte Bytes und den Nonce (Base64)
        public static byte[] Decrypt(byte[] encryptedData, string nonceBase64)
        {
            byte[] nonce = Convert.FromBase64String(nonceBase64);

            // KI: Tag wieder vom Ende trennen
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[encryptedData.Length - 16];
            Buffer.BlockCopy(encryptedData, 0, cipherText, 0, cipherText.Length);
            Buffer.BlockCopy(encryptedData, cipherText.Length, tag, 0, 16);

            byte[] plainData = new byte[cipherText.Length];

            using AesGcm aes = new AesGcm(Key, 16);
            aes.Decrypt(nonce, cipherText, tag, plainData);

            return plainData;
        }
    }
}
