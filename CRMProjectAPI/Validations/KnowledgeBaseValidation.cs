using CRMProjectAPI.Models;
namespace CRMProjectAPI.Validations
{
    public static class KnowledgeBaseValidation
    {
        public static List<string> Validate(KnowledgeBaseCreateDto dto)
        {
            List<string> errors = new();
            if (string.IsNullOrWhiteSpace(dto.Title))
                errors.Add("Başlık zorunludur");
            else if (dto.Title.Length > 300)
                errors.Add("Başlık 300 karakterden uzun olamaz");
            if (string.IsNullOrWhiteSpace(dto.Description))
                errors.Add("Açıklama zorunludur");
            if (dto.LogoProductIDs == null || !dto.LogoProductIDs.Any())
                errors.Add("En az bir Müşteri ürünü seçilmelidir");
            if (string.IsNullOrWhiteSpace(dto.Category))
                errors.Add("Kategori seçilmelidir");
            if (!string.IsNullOrWhiteSpace(dto.CodeBlock) && string.IsNullOrWhiteSpace(dto.CodeLanguage))
                errors.Add("Kod bloğu girilmişse dil seçilmelidir");

            // ✅ YENİ — VideoLink kontrolü
            if (!string.IsNullOrWhiteSpace(dto.VideoLink))
            {
                if (dto.VideoLink.Length > 500)
                    errors.Add("Video linki 500 karakterden uzun olamaz");
                else if (!Uri.TryCreate(dto.VideoLink, UriKind.Absolute, out var uri)
                         || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    errors.Add("Geçerli bir video URL'si giriniz");
            }

            return errors;
        }
    }
}