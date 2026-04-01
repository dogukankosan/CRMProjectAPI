// AnnouncementValidation.cs
public static class AnnouncementValidation
{
    public static List<string> ValidateCreate(AnnouncementCreateDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Title))
            errors.Add("Başlık zorunludur");
        else if (dto.Title.Length > 200)
            errors.Add("Başlık 200 karakterden uzun olamaz");

        if (string.IsNullOrWhiteSpace(dto.Content))
            errors.Add("İçerik zorunludur");

        if (dto.Priority < 1 || dto.Priority > 3)
            errors.Add("Öncelik 1-3 arasında olmalıdır");

        return errors;
    }

    public static List<string> ValidateFile(IFormFile file)
    {
        var errors = new List<string>();

        var allowed = new HashSet<string>
            { ".pdf", ".xls", ".xlsx", ".doc", ".docx",
              ".jpg", ".jpeg", ".png", ".gif", ".txt", ".zip" };

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowed.Contains(ext))
            errors.Add($"{file.FileName} — desteklenmeyen dosya formatı");

        if (file.Length > 20 * 1024 * 1024)
            errors.Add($"{file.FileName} — dosya boyutu 20MB'dan büyük olamaz");

        return errors;
    }
}