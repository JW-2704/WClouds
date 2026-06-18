// KI: Neue Datei – übernimmt die gesamte Ver- und Entschlüsselung (AES-256-GCM)
using System;
using System.IO;
using System.Security.Cryptography;

namespace WClouds_WPF.Logic
{
    // AI Agent: Umbau von "ein statischer AES-Key pro Geraet" auf "RSA-2048-
    // Keypair pro WClouds-Account". Der alte Ansatz (ein einziger Key,
    // gebunden an das Windows-Geraeteprofil, nie uebertragen) machte Sharing
    // kryptographisch unmoeglich - ein Empfaenger hatte immer einen anderen
    // lokalen Key und konnte nie entschluesseln. Jetzt: pro Datei ein
    // zufaelliger Data-Encryption-Key (DEK), der Inhalt wird wie bisher mit
    // AES-256-GCM verschluesselt, der DEK selbst wird pro berechtigtem User
    // mit dessen RSA-Public-Key gewrappt und liegt nur verschluesselt auf
    // dem Server.
    public static class EncryptionService
    {
        private static RSA? _rsa;

        private static string KeysDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WClouds", "keys");

        private static string KeyPath(string identifier) => Path.Combine(KeysDir, $"{identifier}.dat");

        // AI Agent: Reihenfolge-Problem geloest: beim Registrieren ist die
        // userId erst NACH dem Server-Response bekannt, der Keypair muss
        // aber VOR dem Register-Request existieren (Public Key ist Teil
        // des Bodys). Deshalb wird der Key zunaechst unter einem temporaeren
        // Bezeichner erzeugt und erst nach Erhalt der echten userId via
        // PersistKeypairForUser umbenannt.
        public static string GenerateAndStoreNewKeypair(string tempIdentifier)
        {
            Directory.CreateDirectory(KeysDir);
            using RSA rsa = RSA.Create(2048);
            byte[] privateKeyBytes = rsa.ExportRSAPrivateKey();
            byte[] encrypted = ProtectedData.Protect(privateKeyBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyPath(tempIdentifier), encrypted);
            return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        }

        public static void PersistKeypairForUser(string tempIdentifier, int userId)
        {
            File.Move(KeyPath(tempIdentifier), KeyPath(userId.ToString()), overwrite: true);
        }

        public static void DiscardTempKeypair(string tempIdentifier)
        {
            string path = KeyPath(tempIdentifier);
            if (File.Exists(path)) File.Delete(path);
        }

        // AI Agent: wird bei Login aufgerufen - laedt NUR einen lokal
        // existierenden Private Key, generiert NIEMALS einen neuen. Ein
        // Login auf einem neuen Geraet wuerde sonst (wenn man naiv "Key
        // fehlt -> neu erzeugen + hochladen" implementiert) den
        // Server-Public-Key ueberschreiben und alle bestehenden, dafuer
        // gewrappten DEKs permanent unbrauchbar machen. Multi-Device wird
        // bewusst nicht unterstuetzt - der Private Key lebt nur auf dem
        // Geraet, auf dem der Account registriert wurde.
        public static void Initialize(int userId)
        {
            string path = KeyPath(userId.ToString());
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "Kein lokaler Schlüssel für diesen Account gefunden. " +
                    "Dieser Account wurde vermutlich auf einem anderen Gerät " +
                    "registriert – Multi-Device wird nicht unterstützt.");

            byte[] encrypted = File.ReadAllBytes(path);
            byte[] privateKeyBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            RSA rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            _rsa = rsa;
        }

        private static RSA OwnRsa => _rsa
            ?? throw new InvalidOperationException("EncryptionService.Initialize() wurde noch nicht aufgerufen.");

        public static byte[] GenerateDek()
        {
            byte[] dek = new byte[32];
            RandomNumberGenerator.Fill(dek);
            return dek;
        }

        // AI Agent: RSA-2048 + OAEP-SHA256 - kein Zusatzpaket noetig (.NET
        // BCL), ein 32-Byte-DEK passt bequem in die ~190 Byte Payload-
        // Kapazitaet. X25519/ECDH waere eleganter, braucht aber zusaetzlich
        // eine Shared-Secret-Ableitung (HKDF) - unnoetiger Aufwand fuer die
        // Datenmengen eines Uni-Projekts.
        public static string WrapKey(byte[] dek, string recipientPublicKeyBase64)
        {
            using RSA recipientRsa = RSA.Create();
            recipientRsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(recipientPublicKeyBase64), out _);
            byte[] wrapped = recipientRsa.Encrypt(dek, RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(wrapped);
        }

        public static byte[] UnwrapKey(string wrappedKeyBase64)
        {
            byte[] wrapped = Convert.FromBase64String(wrappedKeyBase64);
            return OwnRsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256);
        }

        public static string GetOwnPublicKeyBase64() => Convert.ToBase64String(OwnRsa.ExportSubjectPublicKeyInfo());

        // KI Start | Prompt: Dateien verschlüsseln und entschlüsseln
        // AI Agent: nimmt jetzt einen expliziten DEK-Parameter statt des
        // alten statischen, geraetegebundenen Felds.
        public static (byte[] EncryptedData, string NonceHex) Encrypt(byte[] plainData, byte[] dek)
        {
            byte[] nonce = new byte[12]; // 96 bit Nonce für AES-GCM
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherText = new byte[plainData.Length];
            byte[] tag = new byte[16]; // Authentifizierungs-Tag

            using AesGcm aes = new AesGcm(dek, 16);
            aes.Encrypt(nonce, plainData, cipherText, tag);

            // Tag hinten anhängen damit wir beim Entschlüsseln alles haben
            byte[] encryptedWithTag = new byte[cipherText.Length + tag.Length];
            Buffer.BlockCopy(cipherText, 0, encryptedWithTag, 0, cipherText.Length);
            Buffer.BlockCopy(tag, 0, encryptedWithTag, cipherText.Length, tag.Length);

            return (encryptedWithTag, Convert.ToHexString(nonce));
        }

        // Datei entschlüsseln – braucht verschlüsselte Bytes, den DEK und den Nonce (Hex)
        public static byte[] Decrypt(byte[] encryptedData, byte[] dek, string nonceHex)
        {
            byte[] nonce = Convert.FromHexString(nonceHex);

            // Tag wieder vom Ende trennen
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[encryptedData.Length - 16];
            Buffer.BlockCopy(encryptedData, 0, cipherText, 0, cipherText.Length);
            Buffer.BlockCopy(encryptedData, cipherText.Length, tag, 0, 16);

            byte[] plainData = new byte[cipherText.Length];

            using AesGcm aes = new AesGcm(dek, 16);
            aes.Decrypt(nonce, cipherText, tag, plainData);

            return plainData;
        }
        // KI Ende
    }
}
