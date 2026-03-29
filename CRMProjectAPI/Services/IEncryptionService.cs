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
        // PBKDF2 parametreleri
        private const int SaltSize = 16;       // 128 bit
        private const int HashSize = 32;       // 256 bit
        private const int Iterations = 100_000;  // OWASP önerisi
        public EncryptionService(IConfiguration configuration)
        {
            string secretKey = configuration["Encryption:SecretKey"]
                ?? throw new InvalidOperationException("Encryption:SecretKey konfigürasyonda tanımlı değil!");
            if (secretKey.Length < 32)
                throw new InvalidOperationException("SecretKey en az 32 karakter olmalı!");
           // Direkt byte çevirme yerine PBKDF2 ile güvenli key türet
            byte[] salt = Encoding.UTF8.GetBytes("crm-encryption-salt");
            _key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(secretKey),
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32
            );
        }

        #region AES-GCM Encryption (CBC yerine GCM — authenticated encryption)

        /// <summary>
        /// Metni şifreler (AES-256-GCM + Rastgele Nonce)
        /// </summary>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize); // 12 byte
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipher = new byte[plainBytes.Length];
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 byte
            using AesGcm aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plainBytes, cipher, tag);
            // Format: nonce(12) + tag(16) + ciphertext
            byte[] result = new byte[nonce.Length + tag.Length + cipher.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
            return Convert.ToBase64String(result);
        }
        /// <summary>
        /// Şifreli metni çözer
        /// </summary>
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            try
            {
                byte[] fullBytes = Convert.FromBase64String(cipherText);
                int nonceSize = AesGcm.NonceByteSizes.MaxSize; // 12
                int tagSize = AesGcm.TagByteSizes.MaxSize;   // 16
                if (fullBytes.Length < nonceSize + tagSize) return string.Empty;
                byte[] nonce = fullBytes[..nonceSize];
                byte[] tag = fullBytes[nonceSize..(nonceSize + tagSize)];
                byte[] cipher = fullBytes[(nonceSize + tagSize)..];
                byte[] plain = new byte[cipher.Length];
                using AesGcm aes = new AesGcm(_key, tagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                // Tampered data veya yanlış key
                return string.Empty;
            }
            catch (FormatException)
            {
                // Geçersiz Base64
                return string.Empty;
            }
        }
        #endregion

        #region Password Hashing (PBKDF2 — OWASP standartları)

        /// <summary>
        /// Şifre hashler (PBKDF2-SHA256)
        /// </summary>
        public string HashPassword(string password)
        {
           if (string.IsNullOrEmpty(password)) return string.Empty;
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(password),
                salt: salt,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize
            );
            // Format: iterations.salt.hash (base64)
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }
        /// <summary>
        /// Şifre doğrular (timing-safe)
        /// </summary>
        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
                return false;
            try
            {
                string[] parts = hashedPassword.Split('.');
                if (parts.Length != 3) return false;
                if (!int.TryParse(parts[0], out int iterations) || iterations <= 0)
                    return false;
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] storedHash = Convert.FromBase64String(parts[2]);
                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password: Encoding.UTF8.GetBytes(password),
                    salt: salt,
                    iterations: iterations,
                    hashAlgorithm: HashAlgorithmName.SHA256,
                    outputLength: storedHash.Length
                );
                // Timing-safe karşılaştırma
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}