using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;

namespace CRMProjectAPI.Validations
{
    public static class UserValidation
    {
        public static List<string> ValidateCreate(UserCreateDto dto)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Username))
                errors.Add("Kullanıcı adı zorunludur");
            else if (dto.Username.Length < 3)
                errors.Add("Kullanıcı adı en az 3 karakter olmalıdır");
            else if (dto.Username.Length > 100)
                errors.Add("Kullanıcı adı en fazla 100 karakter olabilir");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Username, @"^[a-zA-Z0-9._\-]+$"))
                errors.Add("Kullanıcı adı sadece harf, rakam, nokta, alt çizgi ve tire içerebilir");
            if (string.IsNullOrWhiteSpace(dto.FullName))
                errors.Add("Ad Soyad zorunludur");
            if (string.IsNullOrWhiteSpace(dto.Password))
                errors.Add("Şifre zorunludur");
            else if (dto.Password.Length < 6)
                errors.Add("Şifre en az 6 karakter olmalıdır");

            if (string.IsNullOrWhiteSpace(dto.EMailAddress))
                errors.Add("E-posta zorunludur");
            else if (!ValidationHelper.IsValidEmail(dto.EMailAddress))
                errors.Add("Geçerli bir e-posta adresi giriniz");
            if (dto.ISAdmin > 2)
                errors.Add("Geçersiz yetki seviyesi");
            if (dto.CompanyID <= 0)
                errors.Add("Şirket seçimi zorunludur");

            if (!string.IsNullOrEmpty(dto.PhoneNumber) && !ValidationHelper.IsValidPhone(dto.PhoneNumber))
                errors.Add("Geçerli bir telefon numarası giriniz");

            return errors;
        }

        public static List<string> ValidateUpdate(UserUpdateDto dto)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Username))
                errors.Add("Kullanıcı adı zorunludur");
            else if (dto.Username.Length < 3)
                errors.Add("Kullanıcı adı en az 3 karakter olmalıdır");
            else if (dto.Username.Length > 100)
                errors.Add("Kullanıcı adı en fazla 100 karakter olabilir");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Username, @"^[a-zA-Z0-9._\-]+$"))
                errors.Add("Kullanıcı adı sadece harf, rakam, nokta, alt çizgi ve tire içerebilir");
            if (string.IsNullOrWhiteSpace(dto.FullName))
                errors.Add("Ad Soyad zorunludur");
            if (!string.IsNullOrWhiteSpace(dto.Password) && dto.Password.Length < 6)
                errors.Add("Şifre en az 6 karakter olmalıdır");
            if (dto.ISAdmin > 2)
                errors.Add("Geçersiz yetki seviyesi");
            if (string.IsNullOrWhiteSpace(dto.EMailAddress))
                errors.Add("E-posta zorunludur");
            else if (!ValidationHelper.IsValidEmail(dto.EMailAddress))
                errors.Add("Geçerli bir e-posta adresi giriniz");

            if (dto.CompanyID <= 0)
                errors.Add("Şirket seçimi zorunludur");

            if (!string.IsNullOrEmpty(dto.PhoneNumber) && !ValidationHelper.IsValidPhone(dto.PhoneNumber))
                errors.Add("Geçerli bir telefon numarası giriniz");

            return errors;
        }
    }
}