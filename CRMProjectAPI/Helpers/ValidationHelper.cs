using System.Globalization;
using System.Text.RegularExpressions;

namespace CRMProjectAPI.Helpers
{
    public static class ValidationHelper
    {
        #region Regex Patterns
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250)
        );
        // PhoneRegex — artık IsValidTurkishPhone içinde kullanılıyor
        private static readonly Regex TurkishPhoneRegex = new(
            @"^5[0-9]{9}$",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250)
        );
        #endregion

        #region TC Kimlik No
        public static bool IsValidTcKimlik(string? tcKimlik)
        {
            if (string.IsNullOrEmpty(tcKimlik) || tcKimlik.Length != 11)
                return false;
            ReadOnlySpan<char> span = tcKimlik.AsSpan();
            if (span[0] == '0') return false;
            Span<int> digits = stackalloc int[11];
            for (int i = 0; i < 11; i++)
            {
                if (!char.IsDigit(span[i])) return false;
                digits[i] = span[i] - '0';
            }
            int oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
            int evenSum = digits[1] + digits[3] + digits[5] + digits[7];
            int digit10 = ((oddSum * 7) - evenSum) % 10;
            if (digit10 < 0) digit10 += 10;
            if (digits[9] != digit10) return false;
            int sum = 0;
            for (int i = 0; i < 10; i++) sum += digits[i];
            return digits[10] == sum % 10;
        }
        #endregion

        #region Email
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
                return false;
            try
            {
                return EmailRegex.IsMatch(email);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
        #endregion

        #region Telefon
        public static bool IsValidPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            string cleaned = phone.Trim();
            int digitCount = 0;
            foreach (char c in cleaned)
            {
                if (char.IsDigit(c)) digitCount++;
                else if (c != '+' && c != '-' && c != '(' && c != ')' && c != ' ')
                    return false; // geçersiz karakter
            }
            // ITU-T E.164: min 10, max 15 rakam
            return digitCount >= 10 && digitCount <= 15;
        }
        public static bool IsValidTurkishPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            string normalized = NormalizePhone(phone);
            // GSM: 5XX XXX XX XX
            if (normalized.Length == 10 && TurkishPhoneRegex.IsMatch(normalized))
                return true;
            // Sabit hat: 2XX, 3XX, 4XX
            if (normalized.Length == 10 &&
               (normalized.StartsWith("2") || normalized.StartsWith("3") || normalized.StartsWith("4")))
                return true;

            return false;
        }
        public static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            Span<char> buffer = stackalloc char[phone.Length];
            int index = 0;
            foreach (char c in phone)
                if (char.IsDigit(c)) buffer[index++] = c;
            string digits = new(buffer[..index]);
            if (digits.StartsWith("90") && digits.Length == 12) return digits[2..];
            if (digits.StartsWith("0") && digits.Length == 11) return digits[1..];
            return digits;
        }
        public static string FormatPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            string normalized = NormalizePhone(phone);
            // Türkiye GSM
            if (normalized.Length == 10 && normalized.StartsWith("5"))
                return $"+90 {normalized[..3]} {normalized[3..6]} {normalized[6..8]} {normalized[8..]}";
            // Türkiye sabit hat
            if (normalized.Length == 10)
                return $"+90 ({normalized[..3]}) {normalized[3..6]} {normalized[6..8]} {normalized[8..]}";
            return phone ?? string.Empty;
        }
        #endregion

        #region IBAN
        public static bool IsValidIban(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return false;
            string normalized = iban.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (normalized.Length != 26 || !normalized.StartsWith("TR"))
                return false;
            foreach (char c in normalized)
                if (!char.IsLetterOrDigit(c)) return false;
            return CalculateIbanMod97(normalized) == 1;
        }
        public static string FormatIban(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return string.Empty;
            string normalized = iban.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (normalized.Length != 26) return iban;
            return string.Join(" ",
                normalized[..4], normalized[4..8],
                normalized[8..12], normalized[12..16],
                normalized[16..20], normalized[20..24],
                normalized[24..]);
        }
        private static int CalculateIbanMod97(string iban)
        {
            string rearranged = iban[4..] + iban[..4];
            int remainder = 0;
            foreach (char c in rearranged)
            {
                int value = char.IsLetter(c) ? c - 'A' + 10 : c - '0';
                remainder = value >= 10
                    ? (remainder * 100 + value) % 97
                    : (remainder * 10 + value) % 97;
            }
            return remainder;
        }
        #endregion

        #region Vergi Numarası
        public static bool IsValidVergiNo(string? vergiNo)
        {
            if (string.IsNullOrWhiteSpace(vergiNo)) return false;
            Span<char> buffer = stackalloc char[vergiNo.Length];
            int index = 0;
            foreach (char c in vergiNo)
                if (char.IsDigit(c)) buffer[index++] = c;
            if (index != 10) return false;
            ReadOnlySpan<char> digits = buffer[..10];
            // stackalloc — heap allocation yok
            Span<int> v = stackalloc int[10];
            for (int i = 0; i < 10; i++)
                v[i] = digits[i] - '0';
            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int tmp = (v[i] + 10 - (i + 1)) % 10;
                int power = 1;
                for (int j = 0; j < 10 - (i + 1); j++) power *= 2;
                sum += (tmp * power) % 9;
                if (tmp != 0 && tmp % 9 == 0) sum += 9;
            }
            return (10 - (sum % 10)) % 10 == v[9];
        }
        #endregion

        #region URL

        public static bool IsValidUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > 2048)
                return false;
            return Uri.TryCreate(url, UriKind.Absolute, out var result)
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
        #endregion

        #region Şifre
        public static bool IsStrongPassword(string? password, int minLength = 8)
        {
            if (string.IsNullOrEmpty(password) || password.Length < minLength)
                return false;
            bool hasLower = false, hasUpper = false,
                 hasDigit = false, hasSpecial = false;
            foreach (char c in password)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSpecial = true;
            }
            int categories = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0)
                           + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);
            return categories >= 3;
        }
        public static int CalculatePasswordStrength(string? password)
        {
            if (string.IsNullOrEmpty(password)) return 0;
            int score = Math.Min(password.Length * 4, 40);
            if (password.Any(char.IsLower)) score += 10;
            if (password.Any(char.IsUpper)) score += 15;
            if (password.Any(char.IsDigit)) score += 15;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score += 20;
            return Math.Min(score, 100);
        }
        #endregion

        #region Genel
        public static bool IsNullOrEmpty(string? value) => string.IsNullOrWhiteSpace(value);
        public static bool HasMinLength(string? value, int min) => !string.IsNullOrEmpty(value) && value.Length >= min;
        public static bool HasMaxLength(string? value, int max) => string.IsNullOrEmpty(value) || value.Length <= max;
        public static bool HasLengthBetween(string? value, int min, int max)
            => !string.IsNullOrEmpty(value) && value.Length >= min && value.Length <= max;
        public static bool IsAlphabetic(string? value) => !string.IsNullOrEmpty(value) && value.All(char.IsLetter);
        public static bool IsNumeric(string? value) => !string.IsNullOrEmpty(value) && value.All(char.IsDigit);
        public static bool IsAlphanumeric(string? value) => !string.IsNullOrEmpty(value) && value.All(char.IsLetterOrDigit);
        #endregion

        #region Tarih
        public static bool IsValidDate(string? dateString, string format = "dd.MM.yyyy")
        {
            if (string.IsNullOrWhiteSpace(dateString)) return false;
            return DateTime.TryParseExact(dateString, format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _);
        }
        public static bool IsMinimumAge(DateTime birthDate, int minimumAge)
        {
            int age = DateTime.Today.Year - birthDate.Year;  // Int32 → int
            if (birthDate.Date > DateTime.Today.AddYears(-age)) age--;
            return age >= minimumAge;
        }
        #endregion

    }
}