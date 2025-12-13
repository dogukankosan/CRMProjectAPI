using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;

namespace CRMProjectAPI.Validations
{
    public static class CompanyValidation
    {
        public static List<string> Validate(CompanyDto dto)
        {
            var errors = new List<string>();

            if (ValidationHelper.IsNullOrEmpty(dto.CompanyName))
                errors.Add("Firma adı zorunludur.");

            if (ValidationHelper.IsNullOrEmpty(dto.ShortCompanyName))
                errors.Add("Kısa firma adı zorunludur.");

            if (!ValidationHelper.IsValidEmail(dto.Email))
                errors.Add("Geçerli bir email adresi giriniz.");

            if (!ValidationHelper.IsValidPhone(dto.Phone))
                errors.Add("Geçerli bir telefon numarası giriniz.");

            if (!ValidationHelper.IsValidUrl(dto.WebSiteLink))
                errors.Add("Geçerli bir web site adresi giriniz.");

            // Opsiyonel URL'ler
            ValidateUrl(dto.InstagramLink, "Instagram linki geçersiz.", errors);
            ValidateUrl(dto.LinkedinLink, "LinkedIn linki geçersiz.", errors);
            ValidateUrl(dto.YoutubeLink, "YouTube linki geçersiz.", errors);
            ValidateUrl(dto.ExternalWebLink, "Harici web linki geçersiz.", errors);
            ValidateUrl(dto.CanonicalUrl, "Canonical URL geçersiz.", errors);

            if (dto.FoundedYear.HasValue &&
                (dto.FoundedYear < 1800 || dto.FoundedYear > DateTime.Now.Year))
                errors.Add("Kuruluş yılı geçersiz.");

            return errors;
        }

        private static void ValidateUrl(string? url, string error, List<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(url) && !ValidationHelper.IsValidUrl(url))
                errors.Add(error);
        }
    }
}
