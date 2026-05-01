using CRMProjectAPI.Models;

namespace CRMProjectAPI.Validations
{
    public static class CertificateValidation
    {
        public static List<string> ValidateCreate(CertificateCreateDto dto)
        {
            List<string> errors = new List<string>();
            if (dto.UserID <= 0)
                errors.Add("Kullanıcı seçimi zorunludur");
            if (string.IsNullOrWhiteSpace(dto.Title))
                errors.Add("Sertifika başlığı zorunludur");
            else if (dto.Title.Length > 200)
                errors.Add("Başlık 200 karakterden uzun olamaz");
            if (string.IsNullOrWhiteSpace(dto.OriginalFileName))
                errors.Add("Dosya adı zorunludur");
            if (string.IsNullOrWhiteSpace(dto.FileBase64))
                errors.Add("Sertifika dosyası zorunludur");
            else
            {
                // Base64 format kontrolü
                try
                {
                    Convert.FromBase64String(dto.FileBase64);
                }
                catch
                {
                    errors.Add("Geçersiz dosya formatı (Base64 bekleniyor)");
                }
            }
            if (!string.IsNullOrWhiteSpace(dto.OriginalFileName))
            {
                string ext = Path.GetExtension(dto.OriginalFileName).ToLowerInvariant();
                if (ext != ".pdf")
                    errors.Add("Sadece PDF dosyası yüklenebilir");
            }
            if (!string.IsNullOrWhiteSpace(dto.Notes) && dto.Notes.Length > 1000)
                errors.Add("Not 1000 karakterden uzun olamaz");
            return errors;
        }
        public static List<string> ValidateUpdate(CertificateUpdateDto dto)
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(dto.Title))
                errors.Add("Sertifika başlığı zorunludur");
            else if (dto.Title.Length > 200)
                errors.Add("Başlık 200 karakterden uzun olamaz");
            if (!string.IsNullOrWhiteSpace(dto.Notes) && dto.Notes.Length > 1000)
                errors.Add("Not 1000 karakterden uzun olamaz");
            // FileBase64 geldiyse kontrol et (güncelleme opsiyonel)
            if (!string.IsNullOrWhiteSpace(dto.FileBase64))
            {
                try
                {
                    Convert.FromBase64String(dto.FileBase64);
                }
                catch
                {
                    errors.Add("Geçersiz dosya formatı (Base64 bekleniyor)");
                }
                if (!string.IsNullOrWhiteSpace(dto.OriginalFileName))
                {
                    string ext = Path.GetExtension(dto.OriginalFileName).ToLowerInvariant();
                    if (ext != ".pdf")
                        errors.Add("Sadece PDF dosyası yüklenebilir");
                }
            }
            return errors;
        }
    }
}