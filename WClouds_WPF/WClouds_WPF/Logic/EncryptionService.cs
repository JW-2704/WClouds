// KI: Neue Datei – übernimmt die gesamte Ver- und Entschlüsselung (AES-256-GCM)
using System;
using System.IO;
using System.Security.Cryptography;

namespace WClouds_WPF.Logic
{
    public static class EncryptionService
    {
        // KI Start | Prompt: Ich brauch wegen dem ApiKey noch das er nicht
        // nach dem schließen der App immer neu generiert wird
        private static readonly byte[] Key = LoadOrCreateKey();

        private static byte[] LoadOrCreateKey()
        {
            // Pfad zur gespeicherten Key-Datei im AppData Ordner des Users
            string keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WClouds", "key.dat"
            );

            // Ordner erstellen falls er noch nicht existiert
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);

            if (File.Exists(keyPath))
            {
                // Verschlüsselte Key-Datei einlesen
                byte[] encrypted = File.ReadAllBytes(keyPath);
                // Mit DPAPI entschlüsseln (nur der aktuelle Windows-User kann das)
                return ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Neuen zufälligen 256-bit Key generieren
                byte[] key = new byte[32];
                RandomNumberGenerator.Fill(key);
                // Key mit DPAPI verschlüsseln bevor er gespeichert wird
                byte[] encrypted = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
                // Verschlüsselten Key in die Datei schreiben
                File.WriteAllBytes(keyPath, encrypted);
                // Unverschlüsselten Key zurückgeben damit er im Speicher verwendet werden kann
                return key;
            }
        }
        // KI Ende 

        // KI Start | Prompt: Dateien verschlüsseln und entschlüsseln
        public static (byte[] EncryptedData, string NonceHex) Encrypt(byte[] plainData)
        {
            byte[] nonce = new byte[12]; // 96 bit Nonce für AES-GCM
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherText = new byte[plainData.Length];
            byte[] tag = new byte[16]; // Authentifizierungs-Tag

            using AesGcm aes = new AesGcm(Key, 16);
            aes.Encrypt(nonce, plainData, cipherText, tag);

            // Tag hinten anhängen damit wir beim Entschlüsseln alles haben
            byte[] encryptedWithTag = new byte[cipherText.Length + tag.Length];
            Buffer.BlockCopy(cipherText, 0, encryptedWithTag, 0, cipherText.Length);
            Buffer.BlockCopy(tag, 0, encryptedWithTag, cipherText.Length, tag.Length);

            return (encryptedWithTag, Convert.ToHexString(nonce));
        }

        

        // Datei entschlüsseln – braucht verschlüsselte Bytes und den Nonce (Base64)
        public static byte[] Decrypt(byte[] encryptedData, string nonceHex)
        {
            byte[] nonce = Convert.FromHexString(nonceHex);

            // Tag wieder vom Ende trennen
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[encryptedData.Length - 16];
            Buffer.BlockCopy(encryptedData, 0, cipherText, 0, cipherText.Length);
            Buffer.BlockCopy(encryptedData, cipherText.Length, tag, 0, 16);

            byte[] plainData = new byte[cipherText.Length];

            using AesGcm aes = new AesGcm(Key, 16);
            aes.Decrypt(nonce, cipherText, tag, plainData);

            return plainData;
        }
        // KI Ende
    }
}
