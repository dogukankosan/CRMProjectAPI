namespace CRMProjectUI.Models
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
        public string? Password { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public string? Signature { get; set; }  // ← eklendi
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
    public class MailTestRequest
    {
        public string MailTo { get; set; } = string.Empty;
        public string? Subject { get; set; }
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
    public class MailSettingsUpdateDto
    {
        public string MailFrom { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public string? Signature { get; set; }  // ← eklendi
    }
}