using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;

namespace CRMProjectAPI.Validations
{
    public static class CompanyValidation
    {
        public static List<string> Validate(CompanyDto dto)
        {
            List<string> errors = new List<string>();
            // ZORUNLU ALANLAR
            ValidateRequired(dto.CompanyName, "Firma adı", 100, errors);
            ValidateRequired(dto.ShortCompanyName, "Kısa firma adı", 50, errors);
            ValidateRequired(dto.Address, "Adres", 300, errors);
            ValidateRequired(dto.WebSiteTitle, "Web sitesi adı", 100, errors);
            ValidateRequired(dto.LogoPath, "Firma logosu", 300, errors);
            ValidateRequired(dto.FaviconPath, "İcon", 300, errors);
            // Email — zorunlu + format
            if (string.IsNullOrWhiteSpace(dto.Email))
                errors.Add("E-posta adresi zorunludur");
            else
            {
                ValidateMaxLength(dto.Email, "E-posta adresi", 100, errors);
                if (!ValidationHelper.IsValidEmail(dto.Email))
                    errors.Add("Geçerli bir e-posta adresi giriniz");
            }
            // Phone — zorunlu + format
            if (string.IsNullOrWhiteSpace(dto.Phone))
                errors.Add("Telefon numarası zorunludur");
            else
            {
                ValidateMaxLength(dto.Phone, "Telefon numarası", 25, errors);
                if (!ValidationHelper.IsValidPhone(dto.Phone))
                    errors.Add("Geçerli bir telefon numarası giriniz");
            }
            // WebSiteLink — zorunlu + URL format
            ValidateRequiredUrl(dto.WebSiteLink, "Web sitesi linki", 150, errors);
            // OPSİYONEL ALANLAR
            ValidateOptionalMaxLength(dto.GoogleMapsEmbed, "Google Maps embed kodu", 1000, errors);
            ValidateOptionalMaxLength(dto.Slogan, "Slogan", 200, errors);
            ValidateOptionalPhone(dto.Phone2, "İkinci telefon", 25, errors);
            ValidateOptionalMaxLength(dto.MetaTitle, "Meta başlık", 70, errors, " (SEO için önerilir)");
            ValidateOptionalMaxLength(dto.MetaDescription, "Meta açıklama", 160, errors, " (SEO için önerilir)");
            ValidateOptionalMaxLength(dto.MetaKeywords, "Meta anahtar kelimeler", 300, errors);
            ValidateOptionalMaxLength(dto.SectorDescription, "Sektör açıklaması", 500, errors);
            ValidateOptionalMaxLength(dto.AboutUsShort, "Kısa hakkımızda metni", 500, errors);
            ValidateOptionalMaxLength(dto.AboutUs, "Hakkımızda metni", 50000, errors);
            ValidateOptionalMaxLength(dto.Vision, "Vizyon metni", 1000, errors);
            ValidateOptionalMaxLength(dto.Mission, "Misyon metni", 1000, errors);
            ValidateOptionalMaxLength(dto.WorkingHours, "Çalışma saatleri", 200, errors);
            ValidateOptionalUrl(dto.CanonicalUrl, "Canonical URL", 200, errors);
            ValidateOptionalUrl(dto.InstagramLink, "Instagram linki", 200, errors);
            ValidateOptionalUrl(dto.LinkedinLink, "LinkedIn linki", 200, errors);
            ValidateOptionalUrl(dto.YoutubeLink, "YouTube linki", 200, errors);
            ValidateOptionalUrl(dto.ExternalWebLink, "Harici web linki", 200, errors);
            // FoundedYear
            if (dto.FoundedYear.HasValue)
            {
                int currentYear = DateTime.Now.Year;
                if (dto.FoundedYear < 1800 || dto.FoundedYear > currentYear)
                    errors.Add($"Kuruluş yılı 1800 ile {currentYear} arasında olmalıdır");
            }
            return errors;
        }

        #region Helpers
        private static void ValidateRequired(string? value, string fieldName, int maxLength, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add($"{fieldName} zorunludur");
            else if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
        }
        private static void ValidateRequiredUrl(string? value, string fieldName, int maxLength, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{fieldName} zorunludur");
                return;
            }
            if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
            else if (!ValidationHelper.IsValidUrl(value))
                errors.Add($"Geçerli bir {fieldName.ToLower()} giriniz");
        }
        private static void ValidateOptionalUrl(string? value, string fieldName, int maxLength, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
            else if (!ValidationHelper.IsValidUrl(value))
                errors.Add($"Geçerli bir {fieldName.ToLower()} giriniz");
        }
        private static void ValidateOptionalPhone(string? value, string fieldName, int maxLength, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string digitsOnly = new string(value.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 7) return;
            if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
            else if (!ValidationHelper.IsValidPhone(value))
                errors.Add($"Geçerli bir {fieldName.ToLower()} giriniz");
        }
        private static void ValidateMaxLength(string value, string fieldName, int maxLength, List<string> errors)
        {
            if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
        }
        private static void ValidateOptionalMaxLength(string? value, string fieldName, int maxLength, List<string> errors, string? suffix = null)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir{suffix ?? ""}");
        }

        #endregion
    }
}