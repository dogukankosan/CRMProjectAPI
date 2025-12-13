using System.Text.RegularExpressions;

namespace CRMProjectAPI.Helpers
{
    public static class ValidationHelper
    {
        /// <summary>
        /// TC Kimlik No doğrulama (11 hane + algoritma kontrolü)
        /// </summary>
        public static bool IsValidTcKimlik(string? tcKimlik)
        {
            if (string.IsNullOrEmpty(tcKimlik) || tcKimlik.Length != 11)
                return false;
            if (!tcKimlik.All(char.IsDigit))
                return false;
            if (tcKimlik[0] == '0')
                return false;
            int[] digits = tcKimlik.Select(c => int.Parse(c.ToString())).ToArray();
            // 10. hane kontrolü
            int oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
            int evenSum = digits[1] + digits[3] + digits[5] + digits[7];
            int digit10 = ((oddSum * 7) - evenSum) % 10;
            if (digit10 < 0) digit10 += 10;
            if (digits[9] != digit10)
                return false;
            // 11. hane kontrolü
            int sum = digits.Take(10).Sum();
            if (digits[10] != sum % 10)
                return false;
            return true;
        }

        /// <summary>
        /// Email formatı doğrulama
        /// </summary>
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }
        /// <summary>
        /// Telefon numarası doğrulama (Türkiye formatı)
        /// </summary>
        public static bool IsValidPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;
            // Sadece rakamları al
            string digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
            // 10 veya 11 haneli olmalı (5xx veya 05xx)
            return digitsOnly.Length == 10 || digitsOnly.Length == 11;
        }
        /// <summary>
        /// Telefon numarasını formatla (5XX XXX XX XX)
        /// </summary>
        public static string FormatPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;
            string? digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
            // Başındaki 0'ı kaldır
            if (digitsOnly.StartsWith("0"))
                digitsOnly = digitsOnly[1..];
            if (digitsOnly.Length != 10)
                return phone;
            return $"{digitsOnly[0..3]} {digitsOnly[3..6]} {digitsOnly[6..8]} {digitsOnly[8..10]}";
        }

        /// <summary>
        /// IBAN doğrulama (Türkiye)
        /// </summary>
        public static bool IsValidIban(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban))
                return false;
            // Boşlukları temizle ve büyük harfe çevir
            iban = iban.Replace(" ", "").ToUpper();
            // TR + 24 rakam = 26 karakter
            if (iban.Length != 26 || !iban.StartsWith("TR"))
                return false;
            // Sadece alfanumerik olmalı
            if (!iban.All(char.IsLetterOrDigit))
                return false;
            return true;
        }

        /// <summary>
        /// Vergi numarası doğrulama (10 hane)
        /// </summary>
        public static bool IsValidVergiNo(string? vergiNo)
        {
            if (string.IsNullOrWhiteSpace(vergiNo))
                return false;
            string? digitsOnly = new string(vergiNo.Where(char.IsDigit).ToArray());
            return digitsOnly.Length == 10;
        }

        /// <summary>
        /// URL formatı doğrulama
        /// </summary>
        public static bool IsValidUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            return Uri.TryCreate(url, UriKind.Absolute, out var result)
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Güçlü şifre kontrolü (min 6 karakter, harf + rakam)
        /// </summary>
        public static bool IsStrongPassword(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
                return false;
            bool hasLetter = password.Any(char.IsLetter);
            bool hasDigit = password.Any(char.IsDigit);
            return hasLetter && hasDigit;
        }

        /// <summary>
        /// Boş veya whitespace kontrolü
        /// </summary>
        public static bool IsNullOrEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Minimum uzunluk kontrolü
        /// </summary>
        public static bool HasMinLength(string? value, int minLength)
        {
            return !string.IsNullOrEmpty(value) && value.Length >= minLength;
        }

        /// <summary>
        /// Maximum uzunluk kontrolü
        /// </summary>
        public static bool HasMaxLength(string? value, int maxLength)
        {
            return string.IsNullOrEmpty(value) || value.Length <= maxLength;
        }
    }
}