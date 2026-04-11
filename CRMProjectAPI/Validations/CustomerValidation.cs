using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;

namespace CRMProjectAPI.Validations
{
    public static class CustomerValidation
    {
        private static readonly HashSet<string> ValidImportance = new() { "VIP", "Önemli", "Normal", "Düşük" };
        private static readonly HashSet<string> ValidCustomerTypes = new() { "Kurumsal", "Bireysel" };

        public static List<string> Validate(CustomerDto dto, bool isSuperAdmin = false, bool isEdit = false)
        {
            List<string> errors = new List<string>();

            // CustomerCode — zorunlu
            ValidateRequired(dto.CustomerCode, "Müşteri kodu", 20, errors);

            // CustomerName — zorunlu
            ValidateRequired(dto.CustomerName, "Müşteri adı", 300, errors);

            // CustomerType — zorunlu
            if (string.IsNullOrWhiteSpace(dto.CustomerType))
                errors.Add("Müşteri tipi zorunludur");
            else if (!ValidCustomerTypes.Contains(dto.CustomerType))
                errors.Add("Müşteri tipi 'Kurumsal' veya 'Bireysel' olmalıdır");

            // VKN / TC
            bool hasVKN = !string.IsNullOrWhiteSpace(dto.VKN);
            bool hasTC = !string.IsNullOrWhiteSpace(dto.TC);

            if (dto.CustomerType == "Kurumsal")
            {
                if (!hasVKN)
                    errors.Add("Kurumsal müşteri için VKN zorunludur");
                else if (!ValidationHelper.IsValidVergiNo(dto.VKN))
                    errors.Add("Geçerli bir VKN giriniz (10 hane, algoritma kontrolü)");
                if (hasTC)
                    errors.Add("Kurumsal müşteride TC kimlik numarası girilemez");
            }
            else if (dto.CustomerType == "Bireysel")
            {
                if (!hasTC)
                    errors.Add("Bireysel müşteri için TC kimlik numarası zorunludur");
                else if (!ValidationHelper.IsValidTcKimlik(dto.TC))
                    errors.Add("Geçerli bir TC kimlik numarası giriniz");
                if (hasVKN)
                    errors.Add("Bireysel müşteride VKN girilemez");
            }

            // OfficialName — zorunlu
            ValidateRequired(dto.OfficialName, "Yetkili adı", 100, errors);

            // OfficialTitle — zorunlu
            ValidateRequired(dto.OfficialTitle, "Yetkili ünvanı", 50, errors);

            // OfficialPhone — zorunlu + format
            if (string.IsNullOrWhiteSpace(dto.OfficialPhone))
                errors.Add("Yetkili telefonu zorunludur");
            else
            {
                ValidateMaxLength(dto.OfficialPhone, "Yetkili telefonu", 20, errors);
                if (!ValidationHelper.IsValidPhone(dto.OfficialPhone))
                    errors.Add("Geçerli bir yetkili telefon numarası giriniz");
            }

            // OfficialEmail — zorunlu + format
            if (string.IsNullOrWhiteSpace(dto.OfficialEmail))
                errors.Add("Yetkili e-posta adresi zorunludur");
            else
            {
                ValidateMaxLength(dto.OfficialEmail, "Yetkili e-posta", 150, errors);
                if (!ValidationHelper.IsValidEmail(dto.OfficialEmail))
                    errors.Add("Geçerli bir yetkili e-posta adresi giriniz");
            }

            // CompanyEmail — zorunlu + format
            if (string.IsNullOrWhiteSpace(dto.CompanyEmail))
                errors.Add("Şirket e-posta adresi zorunludur");
            else
            {
                ValidateMaxLength(dto.CompanyEmail, "Şirket e-posta adresi", 150, errors);
                if (!ValidationHelper.IsValidEmail(dto.CompanyEmail))
                    errors.Add("Geçerli bir şirket e-posta adresi giriniz");
            }

            // CityDistrictID — zorunlu
            if (!dto.CityDistrictID.HasValue || dto.CityDistrictID <= 0)
                errors.Add("İl/İlçe seçimi zorunludur");

            // Address — zorunlu
            ValidateRequired(dto.Address, "Adres", 500, errors);

            // Sözleşme tarihleri:
            // SuperAdmin → her zaman zorunlu
            // Admin yeni kayıt (!isEdit) → zorunlu
            // Admin düzenleme (isEdit) → zorunlu değil, DB CASE WHEN ile koruyor
            bool contractRequired = isSuperAdmin || !isEdit;

            if (contractRequired && !dto.ContractStartDate.HasValue)
                errors.Add("Sözleşme başlangıç tarihi zorunludur");

            if (contractRequired && !dto.ContractEndDate.HasValue)
                errors.Add("Sözleşme bitiş tarihi zorunludur");

            if (dto.ContractStartDate.HasValue && dto.ContractEndDate.HasValue)
                if (dto.ContractEndDate < dto.ContractStartDate)
                    errors.Add("Sözleşme bitiş tarihi, başlangıç tarihinden önce olamaz");

            // Importance — zorunlu (Admin yeni kayıtta girebilir, düzenlemede hidden ile geliyor)
            if (string.IsNullOrWhiteSpace(dto.Importance))
                errors.Add("Önem derecesi zorunludur");
            else if (!ValidImportance.Contains(dto.Importance))
                errors.Add("Önem derecesi 'VIP', 'Önemli', 'Normal' veya 'Düşük' olmalıdır");

            // TicketCount
            if (dto.TicketCount < 0)
                errors.Add("Destek talebi sayısı negatif olamaz");

            // Logo ürünleri — zorunlu
            if (dto.LogoProductIDs == null || !dto.LogoProductIDs.Any())
                errors.Add("En az bir Müşteri ürünü seçilmelidir");

            // OPSİYONEL ALANLAR
            ValidateOptionalMaxLength(dto.BulutERPUsername, "BulutERP kullanıcı adı", 256, errors);
            ValidateOptionalMaxLength(dto.BulutERPPassword, "BulutERP şifresi", 256, errors);
            ValidateOptionalMaxLength(dto.ShortName, "Kısa ad", 50, errors);
            ValidateOptionalPhone(dto.Phone1, "Telefon numarası", 20, errors);
            ValidateOptionalPhone(dto.Phone2, "İkinci telefon", 20, errors);
            ValidateOptionalMaxLength(dto.LogoWebServiceUserName, "Logo web servis kullanıcı adı", 100, errors);
            ValidateOptionalMaxLength(dto.LogoWebServicePassword, "Logo web servis şifresi", 256, errors);
            ValidateOptionalMaxLength(dto.SQLPassword, "SQL şifresi", 256, errors);
            ValidateOptionalMaxLength(dto.ContractPath, "Sözleşme dosya yolu", 500, errors);
            ValidateOptionalMaxLength(dto.InternalNotes, "İç notlar", 50000, errors);

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

        private static void ValidateMaxLength(string value, string fieldName, int maxLength, List<string> errors)
        {
            if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
        }

        private static void ValidateOptionalMaxLength(string? value, string fieldName, int maxLength, List<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
        }

        private static void ValidateOptionalPhone(string? value, string fieldName, int maxLength, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return;
            if (value.Length > maxLength)
                errors.Add($"{fieldName} en fazla {maxLength} karakter olabilir");
            else if (!ValidationHelper.IsValidPhone(value))
                errors.Add($"Geçerli bir {fieldName.ToLower()} giriniz");
        }

        #endregion
    }
}