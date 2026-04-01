namespace CRMProjectUI.Models
{
    public class KnowledgeBaseListDto
    {
        public int ID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? CodeLanguage { get; set; }
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public string? VideoLink { get; set; }        // ✅ YENİ
        public bool IsActive { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? CreatedBy { get; set; }  // ← ekle
        public int FileCount { get; set; }
        public string? ProductNames { get; set; }

        public List<string> ProductNameList =>
            string.IsNullOrEmpty(ProductNames)
                ? new List<string>()
                : ProductNames.Split(',').Select(x => x.Trim()).ToList();
    }

    public class KnowledgeBaseDto
    {
        public int ID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? CodeBlock { get; set; }
        public string? CodeLanguage { get; set; }
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public bool IsActive { get; set; }
        public string? VideoLink { get; set; }        // ✅ YENİ
        public int? CreatedBy { get; set; }  // ← EKLE
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public List<KnowledgeBaseProductDto> Products { get; set; } = new();
        public List<KnowledgeBaseFileDto> Files { get; set; } = new();
    }
    public class KnowledgeBaseProductDto
    {
        public int KnowledgeBaseID { get; set; }
        public short LogoProductID { get; set; }
        public string? LogoProductName { get; set; }
    }

    public class KnowledgeBaseCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? CodeBlock { get; set; }
        public string? CodeLanguage { get; set; }
        public string? VideoLink { get; set; }        // ✅ YENİ
        public List<short> LogoProductIDs { get; set; } = new();
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class KnowledgeBaseFileDto
    {
        public int ID { get; set; }
        public int KnowledgeBaseID { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }

        public string FileSizeDisplay => FileSizeBytes switch
        {
            < 1024 => $"{FileSizeBytes} B",
            < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
            _ => $"{FileSizeBytes / (1024.0 * 1024):F1} MB"
        };

        public string FileIconClass => FileType switch
        {
            "PDF" => "fa-file-pdf text-danger",
            "Excel" => "fa-file-excel text-success",
            "Word" => "fa-file-word text-primary",
            "Image" => "fa-file-image text-warning",
            "Text" => "fa-file-alt text-secondary",
            "Archive" => "fa-file-archive text-secondary",
            _ => "fa-file text-muted"
        };

        public string FileIconColor => FileType switch
        {
            "PDF" => "#ef4444",
            "Excel" => "#10b981",
            "Word" => "#3b82f6",
            "Image" => "#f59e0b",
            "Text" => "#6b7280",
            "Archive" => "#6b7280",
            _ => "#9ca3af"
        };
    }
}