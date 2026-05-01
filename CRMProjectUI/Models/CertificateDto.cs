namespace CRMProjectUI.Models
{
    public class CertificateDto
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public string UserFullName { get; set; } = "";
        public string? UserPicture { get; set; }
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public int UploadedByUserID { get; set; }
        public string UploadedByName { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
    public class CertificateCreateDto
    {
        public int UserID { get; set; }
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public string FileBase64 { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
    }
    public class CertificateUpdateDto
    {
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public string? FileBase64 { get; set; }
        public string? OriginalFileName { get; set; }
    }
}