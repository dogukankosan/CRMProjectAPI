namespace CRMProjectAPI.Models
{
    public class CertificateCreateDto
    {
        public int UserID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string FileBase64 { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
    }
    public class CertificateUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        // Dosya güncelleme opsiyonel — gelmezse eski dosya kalır
        public string? FileBase64 { get; set; }
        public string? OriginalFileName { get; set; }
    }
    public class CertificateDto
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserPicture { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int UploadedByUserID { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}