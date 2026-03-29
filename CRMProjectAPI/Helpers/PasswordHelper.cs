using BCrypt.Net;

namespace CRMProjectAPI.Helpers
{
    public static class PasswordHelper
    {
        // Şifreyi hash'le
        public static string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }
        // Şifreyi doğrula
        public static bool Verify(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}