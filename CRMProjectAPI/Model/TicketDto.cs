namespace CRMProjectAPI.Models
{
    // ==========================================
    // TICKET DTO'LARI
    // ==========================================

    /// <summary>
    /// Ticket detay — GetById
    /// </summary>
    public class TicketDto
    {
        public int ID { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public short? LogoProductID { get; set; }
        public int CreatedByUserID { get; set; }
        public int? AssignedToUserID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? TakenInProgressDate => AssignedDate;

        public byte Priority { get; set; }
        public byte Status { get; set; }
        public string? AssignedToPicture { get; set; }
        public string? CreatedByPicture { get; set; }
        public DateTime OpenedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public int WorkingMinute { get; set; }
        public string? SolutionNote { get; set; }

        // JOIN'den gelen alanlar
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? LogoProductName { get; set; }
        public string? CreatedByName { get; set; }
        public string? AssignedToName { get; set; }

        // İlişkili veriler
        public List<TicketFileDto> Files { get; set; } = new();
        public List<TicketCommentDto> Comments { get; set; } = new();

        // View helper'ları
        public string StatusText => Status switch
        {
            0 => "Beklemede",
            1 => "İşlemde",
            2 => "Başarılı Kapandı",
            3 => "Çözülemedi",
            4 => "Müşteri Bize Dönecek",
            5 => "Müşteriye Geri Döneceğiz",
            _ => "Bilinmiyor"
        };

        public string PriorityText => Priority switch
        {
            1 => "Düşük",
            2 => "Normal",
            3 => "Yüksek",
            4 => "Kritik",
            _ => "Normal"
        };

        public bool IsClosed => Status == 2 || Status == 3;
    }

    /// <summary>
    /// Ticket liste — Liste ekranı
    /// </summary>
    public class TicketListDto
    {
        public int ID { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public short? LogoProductID { get; set; }
        public int CreatedByUserID { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? AssignedToUserID { get; set; }
        public string Title { get; set; } = string.Empty;
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public DateTime OpenedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public int WorkingMinute { get; set; }

        // JOIN'den gelen alanlar
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? LogoProductName { get; set; }
        public string? CreatedByName { get; set; }
        public string? AssignedToName { get; set; }

        // ── YENİ EKLENENLER ──
        public string? CreatedByPicture { get; set; }
        public string? AssignedToPicture { get; set; }
        public string? CreatedByPhone { get; set; }
        public string? CreatedByEmail { get; set; }
        public string? CustomerImportance { get; set; }
        public DateTime? TakenInProgressDate => AssignedDate;

        // View helper'ları
        public string StatusText => Status switch
        {
            0 => "Beklemede",
            1 => "İşlemde",
            2 => "Başarılı Kapandı",
            3 => "Çözülemedi",
            4 => "Müşteri Bize Dönecek",
            5 => "Müşteriye Geri Döneceğiz",
            _ => "Bilinmiyor"
        };

        public string PriorityText => Priority switch
        {
            1 => "Düşük",
            2 => "Normal",
            3 => "Yüksek",
            4 => "Kritik",
            _ => "Normal"
        };

        public string StatusBadgeClass => Status switch
        {
            0 => "badge-warning",
            1 => "badge-info",
            2 => "badge-success",
            3 => "badge-danger",
            4 => "badge-secondary",
            5 => "badge-dark",
            _ => "badge-light"
        };

        public string PriorityBadgeClass => Priority switch
        {
            1 => "badge-light",
            2 => "badge-info",
            3 => "badge-warning",
            4 => "badge-danger",
            _ => "badge-info"
        };

        public bool IsClosed => Status == 2 || Status == 3;
        public bool IsAssigned => AssignedToUserID.HasValue;
    }

    /// <summary>
    /// Ticket oluşturma — User ve SuperAdmin
    /// </summary>
    public class TicketCreateDto
    {
        public int CustomerID { get; set; }
        public short LogoProductID { get; set; }
        public int CreatedByUserID { get; set; }
        public DateTime? OpenedDate { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte Priority { get; set; } = 2; // Default: Normal
    }

    /// <summary>
    /// Ticket durum güncelleme — Admin ve SuperAdmin
    /// </summary>
    public class TicketStatusUpdateDto
    {
        public byte Status { get; set; }
        public string? SolutionNote { get; set; }
        public string? CancelReason { get; set; } // ← EKLE
        public int WorkingMinute { get; set; } = 0;
    }

    /// <summary>
    /// Ticket devir — Admin ve SuperAdmin
    /// </summary>
    public class TicketAssignDto
    {
        public int AssignedToUserID { get; set; }
    }

    // ==========================================
    // TICKET FILE DTO'LARI
    // ==========================================

    public class TicketFileDto
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? FileHash { get; set; }
        public string FileType { get; set; } = "Other";
        public int? UploadedByUserID { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime UploadedDate { get; set; }
        public bool IsDeleted { get; set; }

        // View helper'ları
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
            _ => "fa-file text-muted"
        };
    }

    // ==========================================
    // TICKET COMMENT DTO'LARI
    // ==========================================

    public class TicketCommentDto
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public int UserID { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        // JOIN'den gelen alanlar
        public string? UserFullName { get; set; }
        public string? UserPicture { get; set; }
        public byte UserIsAdmin { get; set; }

        // View helper
        public string RoleBadge => UserIsAdmin switch
        {
            2 => "Süper Admin",
            1 => "Admin",
            _ => "Kullanıcı"
        };
    }

    public class TicketCommentCreateDto
    {
        public int TicketID { get; set; }
        public int UserID { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    // ==========================================
    // DASHBOARD DTO'LARI
    // ==========================================

    public class TicketStatsDto
    {
        public int Total { get; set; }
        public int OpenCount { get; set; }
        public int InProgress { get; set; }
        public int Resolved { get; set; }
        public int Failed { get; set; }
        public int WaitingCustomer { get; set; }
        public int WaitingUs { get; set; }
        public double AvgWorkingMinute { get; set; }

        // View helper
        public string AvgWorkingDisplay
        {
            get
            {
                int total = (int)AvgWorkingMinute;
                if (total < 60) return $"{total} dk";
                int hours = total / 60;
                int mins = total % 60;
                return mins > 0 ? $"{hours} sa {mins} dk" : $"{hours} sa";
            }
        }
    }
}