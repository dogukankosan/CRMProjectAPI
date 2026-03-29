namespace CRMProjectUI.Models
{
    // ==========================================
    // TICKET LIST DTO
    // ==========================================

    public class TicketListDto
    {
        public int ID { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public short? LogoProductID { get; set; }
        public int CreatedByUserID { get; set; }
        public int? AssignedToUserID { get; set; }
        public string? Description { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CustomerImportance { get; set; }
        public DateTime? TakenInProgressDate { get; set; }
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public DateTime? OpenedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public int WorkingMinute { get; set; }
        public string? CreatedByPicture { get; set; }
        public string? AssignedToPicture { get; set; }
        public string? CreatedByPhone { get; set; }
        public string? CreatedByEmail { get; set; }

        // JOIN'den gelen alanlar
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? LogoProductName { get; set; }
        public string? CreatedByName { get; set; }
        public string? AssignedToName { get; set; }

        public string StatusText => Status switch
        {
            0 => "Beklemede",
            1 => "İşlemde",
            2 => "Başarılı Kapandı",
            3 => "Çözülemedi",
            4 => "Müşteri Bize Dönecek",
            5 => "Müşteriye Geri Döneceğiz",
            6 => "İptal Edildi",
            _ => "Bilinmiyor"
        };

        public string InProgressDuration
        {
            get
            {
                if (Status != 1 || !TakenInProgressDate.HasValue) return "-";
                var diff = DateTime.Now - TakenInProgressDate.Value;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} dk";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} sa {(int)diff.TotalMinutes % 60} dk";
                return $"{(int)diff.TotalDays} gün {(int)diff.TotalHours % 24} sa";
            }
        }

        public string StatusBadgeClass => Status switch
        {
            0 => "badge-warning",
            1 => "badge-info",
            2 => "badge-success",
            3 => "badge-danger",
            4 => "badge-secondary",
            5 => "badge-primary",
            6 => "badge-dark",
            _ => "badge-light"
        };

        public string StatusClass => StatusBadgeClass;

        public string StatusBadgeColor => Status switch
        {
            0 => "#f59e0b",
            1 => "#3b82f6",
            2 => "#10b981",
            3 => "#ef4444",
            4 => "#6b7280",
            5 => "#8b5cf6",
            6 => "#374151",
            _ => "#9ca3af"
        };

        public string PriorityText => Priority switch
        {
            1 => "Düşük",
            2 => "Normal",
            3 => "Yüksek",
            4 => "Kritik",
            _ => "Normal"
        };

        public string PriorityBadgeClass => Priority switch
        {
            1 => "badge-light",
            2 => "badge-info",
            3 => "badge-warning",
            4 => "badge-danger",
            _ => "badge-info"
        };

        public string PriorityClass => PriorityBadgeClass;

        public string PriorityBadgeColor => Priority switch
        {
            1 => "#9ca3af",
            2 => "#3b82f6",
            3 => "#f59e0b",
            4 => "#ef4444",
            _ => "#3b82f6"
        };

        public bool IsClosed => Status == 2 || Status == 3 || Status == 6;
        public bool IsAssigned => AssignedToUserID.HasValue;

        public string WorkingMinuteDisplay
        {
            get
            {
                if (WorkingMinute < 60) return $"{WorkingMinute} dk";
                int h = WorkingMinute / 60;
                int m = WorkingMinute % 60;
                return m > 0 ? $"{h} sa {m} dk" : $"{h} sa";
            }
        }

        public string ElapsedText
        {
            get
            {
                if (!OpenedDate.HasValue) return "-";
                var diff = DateTime.Now - OpenedDate.Value;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} dk önce";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} sa önce";
                if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} gün önce";
                if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} ay önce";
                return $"{(int)(diff.TotalDays / 365)} yıl önce";
            }
        }
    }

    // ==========================================
    // TICKET DETAY DTO
    // ==========================================
    public class TicketDto
    {
        public int ID { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public short? LogoProductID { get; set; }
        public int CreatedByUserID { get; set; }
        public DateTime? TakenInProgressDate { get; set; }
        public int? AssignedToUserID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public DateTime? OpenedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public int WorkingMinute { get; set; }
        public string? SolutionNote { get; set; }

        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? LogoProductName { get; set; }
        public string? CreatedByName { get; set; }
        public string? AssignedToName { get; set; }
        public string? AssignedToPicture { get; set; }
        public string? CreatedByPicture { get; set; }

        public List<TicketFileDto> Files { get; set; } = new();
        public List<TicketCommentDto> Comments { get; set; } = new();

        public string StatusText => Status switch
        {
            0 => "Beklemede",
            1 => "İşlemde",
            2 => "Başarılı Kapandı",
            3 => "Çözülemedi",
            4 => "Müşteri Bize Dönecek",
            5 => "Müşteriye Geri Döneceğiz",
            6 => "İptal Edildi",
            _ => "Bilinmiyor"
        };

        public string StatusBadgeClass => Status switch
        {
            0 => "badge-warning",
            1 => "badge-info",
            2 => "badge-success",
            3 => "badge-danger",
            4 => "badge-secondary",
            5 => "badge-primary",
            6 => "badge-dark",
            _ => "badge-light"
        };

        public string StatusClass => StatusBadgeClass;

        public string StatusBadgeColor => Status switch
        {
            0 => "#f59e0b",
            1 => "#3b82f6",
            2 => "#10b981",
            3 => "#ef4444",
            4 => "#6b7280",
            5 => "#8b5cf6",
            6 => "#374151",
            _ => "#9ca3af"
        };

        public string PriorityText => Priority switch
        {
            1 => "Düşük",
            2 => "Normal",
            3 => "Yüksek",
            4 => "Kritik",
            _ => "Normal"
        };

        public string PriorityBadgeClass => Priority switch
        {
            1 => "badge-light",
            2 => "badge-info",
            3 => "badge-warning",
            4 => "badge-danger",
            _ => "badge-info"
        };

        public string PriorityClass => PriorityBadgeClass;

        public string PriorityBadgeColor => Priority switch
        {
            1 => "#9ca3af",
            2 => "#3b82f6",
            3 => "#f59e0b",
            4 => "#ef4444",
            _ => "#3b82f6"
        };

        public bool IsClosed => Status == 2 || Status == 3 || Status == 6;
        public bool IsAssigned => AssignedToUserID.HasValue;

        public string WorkingMinuteDisplay
        {
            get
            {
                if (WorkingMinute < 60) return $"{WorkingMinute} dk";
                int h = WorkingMinute / 60;
                int m = WorkingMinute % 60;
                return m > 0 ? $"{h} sa {m} dk" : $"{h} sa";
            }
        }

        public string ElapsedText
        {
            get
            {
                if (!OpenedDate.HasValue) return "-";
                var diff = DateTime.Now - OpenedDate.Value;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} dk önce";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} sa önce";
                if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} gün önce";
                if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} ay önce";
                return $"{(int)(diff.TotalDays / 365)} yıl önce";
            }
        }
    }

    // ==========================================
    // TICKET OLUŞTURMA DTO
    // ==========================================
    public class TicketCreateDto
    {
        public int CustomerID { get; set; }
        public int LogoProductID { get; set; }
        public DateTime? OpenedDate { get; set; }
        public int CreatedByUserID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte Priority { get; set; } = 2;
    }

    // ==========================================
    // TICKET DURUM GÜNCELLEME DTO
    // ==========================================
    public class TicketStatusUpdateDto
    {
        public byte Status { get; set; }
        public string? SolutionNote { get; set; }
        public string? CancelReason { get; set; }
        public int WorkingMinute { get; set; } = 0;
    }

    // ==========================================
    // TICKET DEVİR DTO
    // ==========================================
    public class TicketAssignDto
    {
        public int AssignedToUserID { get; set; }
    }

    // ==========================================
    // TICKET FILE DTO
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

        public string FileIconColor => FileType switch
        {
            "PDF" => "#ef4444",
            "Excel" => "#10b981",
            "Word" => "#3b82f6",
            "Image" => "#f59e0b",
            "Text" => "#6b7280",
            _ => "#9ca3af"
        };
    }

    // ==========================================
    // TICKET COMMENT DTO
    // ==========================================
    public class TicketCommentDto
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public int UserID { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        public string? UserFullName { get; set; }
        public string? UserPicture { get; set; }
        public byte UserIsAdmin { get; set; }

        public string RoleBadge => UserIsAdmin switch
        {
            2 => "Süper Admin",
            1 => "Admin",
            _ => "Kullanıcı"
        };

        public string RoleBadgeColor => UserIsAdmin switch
        {
            2 => "#ef4444",
            1 => "#667eea",
            _ => "#6b7280"
        };
    }

    public class TicketCommentCreateDto
    {
        public int TicketID { get; set; }
        public int UserID { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    // ==========================================
    // DASHBOARD — SUPERADMIN
    // ==========================================
    public class SuperAdminDashboardDto
    {
        public SuperAdminStatsDto? Stats { get; set; }
        public int LowTicketCount { get; set; }
        public List<ExpiringContractDto> ExpiringContracts { get; set; } = new();
        public List<DailyCountDto> DailyCounts { get; set; } = new();
        public List<StatusDistDto> StatusDistribution { get; set; } = new();
        public List<TicketListDto> UrgentTickets { get; set; } = new();
    }

    public class SuperAdminStatsDto
    {
        public int? Total { get; set; }
        public int? OpenCount { get; set; }
        public int? Waiting { get; set; }
        public int? InProgress { get; set; }
        public int? Resolved { get; set; }
        public int? Failed { get; set; }
        public int? Cancelled { get; set; }
        public int? WaitingCustomer { get; set; }
        public int? CriticalOpen { get; set; }
        public int? TodayOpened { get; set; }
        public int? TodayResolved { get; set; }
        public int? ThisMonthOpened { get; set; }
        public int? ThisMonthResolved { get; set; }
        public double? AvgWorkingMinute { get; set; }

        public string AvgWorkingDisplay
        {
            get
            {
                int t = (int)AvgWorkingMinute;
                if (t < 60) return $"{t} dk";
                return $"{t / 60} sa {t % 60} dk";
            }
        }
    }

    // ==========================================
    // DASHBOARD — ADMIN
    // ==========================================
    public class AdminPersonalDashboardDto
    {
        public AdminPersonalStatsDto? Stats { get; set; }
        public List<TicketListDto> MyOpenTickets { get; set; } = new();
        public List<TicketListDto> RecentResolved { get; set; } = new();
    }

    public class AdminPersonalStatsDto
    {
        public int? Total { get; set; }
        public int? OpenCount { get; set; }
        public int? Resolved { get; set; }
        public int? Failed { get; set; }
        public int? TodayResolved { get; set; }
        public int? ThisWeekResolved { get; set; }
        public int? ThisMonthResolved { get; set; }
        public double? AvgWorkingMinute { get; set; }

        public string AvgWorkingDisplay
        {
            get
            {
                int t = (int)AvgWorkingMinute;
                if (t < 60) return $"{t} dk";
                return $"{t / 60} sa {t % 60} dk";
            }
        }
    }

    // ==========================================
    // DASHBOARD — USER
    // ==========================================
    public class UserPersonalDashboardDto
    {
        public UserCompanyStatsDto? CompanyStats { get; set; }
        public UserPersonalStatsDto? MyStats { get; set; }
        public List<TicketListDto> MyOpenTickets { get; set; } = new();
        public List<TicketListDto> CompanyOpenTickets { get; set; } = new();
    }

    public class UserCompanyStatsDto
    {
        public int? Total { get; set; }
        public int? OpenCount { get; set; }
        public int? Resolved { get; set; }
        public int? Failed { get; set; }
        public int? ThisMonthOpened { get; set; }
        public int? ThisMonthResolved { get; set; }
        public double? AvgWorkingMinute { get; set; }

        public string AvgWorkingDisplay
        {
            get
            {
                int t = (int)AvgWorkingMinute;
                if (t < 60) return $"{t} dk";
                return $"{t / 60} sa {t % 60} dk";
            }
        }
    }

    public class UserPersonalStatsDto
    {
        public int? Total { get; set; }
        public int? OpenCount { get; set; }
        public int? Resolved { get; set; }
        public int? ThisMonthOpened { get; set; }
    }

    // ==========================================
    // SHARED DASHBOARD DTO'LARI
    // ==========================================
    public class ExpiringContractDto
    {
        public int ID { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? ShortName { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public int DaysLeft { get; set; }

        public string DaysLeftClass => DaysLeft switch
        {
            <= 7 => "danger",
            <= 15 => "warning",
            _ => "info"
        };
    }

    public class DailyCountDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class StatusDistDto
    {
        public byte Status { get; set; }
        public int Count { get; set; }

        public string StatusText => Status switch
        {
            0 => "Beklemede",
            1 => "İşlemde",
            2 => "Başarılı Kapandı",
            3 => "Çözülemedi",
            4 => "Müşteri Dönecek",
            5 => "Biz Döneceğiz",
            6 => "İptal Edildi",
            _ => "Bilinmiyor"
        };

        public string Color => Status switch
        {
            0 => "#94a3b8",
            1 => "#3b82f6",
            2 => "#10b981",
            3 => "#ef4444",
            4 => "#f59e0b",
            5 => "#8b5cf6",
            6 => "#374151",
            _ => "#9ca3af"
        };
    }

    // Geriye dönük uyumluluk
    public class TicketStatsDto
    {
        public int? Total { get; set; }
        public int? OpenCount { get; set; }
        public int? InProgress { get; set; }
        public int? Resolved { get; set; }
        public int? Failed { get; set; }
        public int? WaitingCustomer { get; set; }
        public int? WaitingUs { get; set; }
        public int? CriticalOpen { get; set; }
        public int? Waiting { get; set; }
        public double? AvgWorkingMinute { get; set; }

        public string AvgWorkingDisplay
        {
            get
            {
                int total = (int)(AvgWorkingMinute ?? 0);
                if (total < 60) return $"{total} dk";
                int h = total / 60;
                int m = total % 60;
                return m > 0 ? $"{h} sa {m} dk" : $"{h} sa";
            }
        }

        public string AvgWorkingTimeText => AvgWorkingDisplay;
    }

    public class TicketDashboardDto
    {
        public TicketStatsDto? Stats { get; set; }
        public List<TicketListDto> UrgentTickets { get; set; } = new();
        public List<ExpiringContractDto> ExpiringContracts { get; set; } = new();
        public List<DailyCountDto> DailyCounts { get; set; } = new();
        public List<StatusDistDto> StatusDistribution { get; set; } = new();
    }

    public class UserTicketDashboardDto
    {
        public TicketStatsDto? Stats { get; set; }
        public List<TicketListDto> MyTickets { get; set; } = new();
        public List<TicketListDto> OpenTickets { get; set; } = new();
    }
    public class MyNotificationsDto
    {
        public int Count { get; set; }
        public List<TicketListDto> Tickets { get; set; } = new();
    }

    public class CompanyNotificationsDto
    {
        public int CommentCount { get; set; }
        public int FileCount { get; set; }
        public int TotalCount { get; set; }
        public List<NotificationCommentDto> Comments { get; set; } = new();
        public List<NotificationFileDto> Files { get; set; } = new();
    }

    public class NotificationCommentDto
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? TicketNo { get; set; }
        public string? Title { get; set; }
        public string? UserFullName { get; set; }
        public byte UserIsAdmin { get; set; }

        public string ShortComment => Comment.Length > 50 ? Comment.Substring(0, 50) + "…" : Comment;
        public string ElapsedText
        {
            get
            {
                var diff = DateTime.Now - CreatedDate;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} dk önce";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} sa önce";
                return $"{(int)diff.TotalDays} gün önce";
            }
        }
    }

    public class NotificationFileDto
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public DateTime UploadedDate { get; set; }
        public string? TicketNo { get; set; }
        public string? Title { get; set; }
        public string? UploadedByName { get; set; }

        public string ElapsedText
        {
            get
            {
                var diff = DateTime.Now - UploadedDate;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} dk önce";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} sa önce";
                return $"{(int)diff.TotalDays} gün önce";
            }
        }
    }
    public class UserDashboardDto : UserTicketDashboardDto { }
    public class AdminDashboardDto : TicketDashboardDto { }
}