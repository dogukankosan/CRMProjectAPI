// AnnouncementDto.cs (UI Models)
namespace CRMProjectUI.Models
{
    public class AnnouncementDto
    {
        public int ID { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public byte Priority { get; set; }
        public string? CreatedByName { get; set; }
        public int CreatedByUserID { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool IsActive { get; set; }
        public List<AnnouncementFileDto> Files { get; set; } = new();
    }
    // AnnouncementFormViewModel.cs
    public class AnnouncementFormViewModel
    {
        public AnnouncementCreateDto CreateDto { get; set; } = new();
        public string FormAction { get; set; } = "";
        public bool IsEdit { get; set; }
        public int AnnouncementId { get; set; }
        public List<AnnouncementFileDto> ExistingFiles { get; set; } = new();
    }   

    public class AnnouncementFileDto
    {
        public int ID { get; set; }
        public int AnnouncementID { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string FileExtension { get; set; } = "";
        public string MimeType { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime UploadedDate { get; set; }
    }

    public class AnnouncementCreateDto
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public byte Priority { get; set; } = 1;
    }
}