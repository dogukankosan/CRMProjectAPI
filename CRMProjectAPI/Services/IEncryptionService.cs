using System.Security.Cryptography;
using System.Text;

namespace CRMProjectAPI.Services
{
    public interface IEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;
        public EncryptionService(IConfiguration configuration)
        {
            // appsettings.json'dan key al
            string secretKey = configuration["Encryption:SecretKey"]
                ?? "CRMProject2025GizliAnahtar32Chr"; // 32 karakter = 256 bit
            // Key 32 byte olmalı (AES-256)
            _key = Encoding.UTF8.GetBytes(secretKey.PadRight(32).Substring(0, 32));
            // IV 16 byte olmalı
            _iv = Encoding.UTF8.GetBytes(secretKey.PadRight(16).Substring(0, 16));
        }
        /// <summary>
        /// Metni şifreler (AES-256)
        /// </summary>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;
            using Aes aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using MemoryStream ms = new MemoryStream();
            using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (StreamWriter sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            return Convert.ToBase64String(ms.ToArray());
        }
        /// <summary>
        /// Şifreli metni çözer
        /// </summary>
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;
            try
            {
                byte[] buffer = Convert.FromBase64String(cipherText);
                using Aes aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using MemoryStream ms = new MemoryStream(buffer);
                using CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using StreamReader sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                return string.Empty; // Çözülemezse boş dön
            }
        }
        /// <summary>
        /// Şifre hashler (tek yönlü - geri alınamaz)
        /// </summary>
        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;
            // Salt + SHA256
            byte[] salt = GenerateSalt();
            byte[] hash = ComputeHash(password, salt);
            // Salt ve hash'i birleştir: salt:hash
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }
        /// <summary>
        /// Şifre doğrular
        /// </summary>
        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
                return false;
            try
            {
                string[] parts = hashedPassword.Split(':');
                if (parts.Length != 2)
                    return false;
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] storedHash = Convert.FromBase64String(parts[1]);
                byte[] computedHash = ComputeHash(password, salt);
                return storedHash.SequenceEqual(computedHash);
            }
            catch
            {
                return false;
            }
        }
        private static byte[] GenerateSalt()
        {
            byte[] salt = new byte[16];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }
        private static byte[] ComputeHash(string password, byte[] salt)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] saltedPassword = new byte[passwordBytes.Length + salt.Length];
            Buffer.BlockCopy(passwordBytes, 0, saltedPassword, 0, passwordBytes.Length);
            Buffer.BlockCopy(salt, 0, saltedPassword, passwordBytes.Length, salt.Length);
            return sha256.ComputeHash(saltedPassword);
        }
    }
}