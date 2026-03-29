// Models/MailSettingsModels.cs
namespace CRMProjectAPI.Models
{
    public class MailSettingsDto
    {
        public int ID { get; set; }
        public string MailFrom { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string? Signature { get; set; }
        public string? Password { get; set; }  // GET'te null gelir, PUT'ta dolu olursa güncelle
        public int TimeoutSeconds { get; set; } = 30;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }

    public class MailSettingsUpdateDto
    {
        public string MailFrom { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string? Signature { get; set; }
        public string? Password { get; set; }  // boşsa mevcut şifreyi koru
        public int TimeoutSeconds { get; set; } = 30;
    }
    public class MailTestDto
    {
        public string MailTo { get; set; } = string.Empty;
        public string Subject { get; set; } = "Test Maili";
        // Form değerleri — boşsa DB'den al
        public string? MailFrom { get; set; }
        public string? DisplayName { get; set; }
        public string? SmtpHost { get; set; }
        public int? SmtpPort { get; set; }
        public bool? EnableSsl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int? TimeoutSeconds { get; set; }
        public string? Signature { get; set; }
    }
}