namespace CRMProjectAPI.Models
{
    // ==========================================
    // CUSTOMER DTO'LARI
    // ==========================================

    public class CustomerDto
    {
        public int ID { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string CustomerType { get; set; } = "Kurumsal";
        public string? BulutERPUsername { get; set; }
        public string? BulutERPPassword { get; set; }

        // Vergi Bilgileri
        public string? VKN { get; set; }
        public string? TC { get; set; }

        // Yetkili Bilgileri
        public string? OfficialName { get; set; }
        public string? OfficialTitle { get; set; }
        public string? OfficialPhone { get; set; }
        public string? OfficialEmail { get; set; }

        // İletişim
        public string? CompanyEmail { get; set; }
        public string? Phone1 { get; set; }
        public string? Phone2 { get; set; }

        // Adres
        public int? CityDistrictID { get; set; }
        public string? Address { get; set; }

        // Adres Detayları (JOIN'den gelir)
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? PostaKodu { get; set; }
        // SemtBucakBelde ve Mahalle DB'den silindi — kaldırıldı

        // CRM
        public string? Importance { get; set; }
        public int TicketCount { get; set; }
        public byte Status { get; set; } = 1;

        // Logo ERP (şifreli gelir/gider)
        public string? LogoWebServiceUserName { get; set; }
        public string? LogoWebServicePassword { get; set; }
        public string? SQLPassword { get; set; }

        // Sözleşme
        public string? ContractPath { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public bool HasMaintenanceContract { get; set; } = false;  // ← EKLE


        // Notlar
        public string? InternalNotes { get; set; }

        // Audit
        public int? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }

        // İlişkili Veriler
        public List<int>? LogoProductIDs { get; set; }
        public List<LogoProductDto>? LogoProducts { get; set; }
        public List<CustomerFileDto>? Files { get; set; }
    }
    public class CustomerListDto
    {
        public int ID { get; set; }
        public string? CustomerCode { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string CustomerType { get; set; } = string.Empty;
        public string? VKN { get; set; }
        public string? TC { get; set; }
        public string? BulutERPUsername { get; set; }
        public string? BulutERPPassword { get; set; }
        public string? OfficialName { get; set; }
        public string? WsUsername { get; set; }
        public string? WsPassword { get; set; }
        public string? Phone1 { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Importance { get; set; }
        public int TicketCount { get; set; }
        public byte Status { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public bool HasMaintenanceContract { get; set; }  // ← EKLE

        public DateTime CreatedDate { get; set; }
    }

    public class CustomerSelectDto
    {
        public int ID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerCode { get; set; }
        public string? VKN { get; set; }
    }

    // ==========================================
    // CUSTOMER FILES DTO'LARI
    // ==========================================

    public class CustomerFileDto
    {
        public int ID { get; set; }
        public int CustomerID { get; set; }
        public string Category { get; set; } = "Genel";
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? FileHash { get; set; }
        public string FileType { get; set; } = "Other";
        public string? Description { get; set; }
        public string? Tags { get; set; }
        public short Version { get; set; } = 1;
        public int? ParentFileID { get; set; }
        public bool IsDeleted { get; set; }

        // Audit
        public int? UploadedBy { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime UploadedDate { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
    }

    // ==========================================
    // LOGO PRODUCTS DTO'LARI
    // ==========================================

    public class LogoProductDto
    {
        public short ID { get; set; }
        public string LogoProductName { get; set; } = string.Empty;
    }

    public class CustomerLogoProductDto
    {
        // Composite PK — ID yok
        public int CustomerID { get; set; }
        public short LogoProductID { get; set; }
        public string? LogoProductName { get; set; }
        public int? AssignedBy { get; set; }
        public DateTime AssignedDate { get; set; }
    }

    // ==========================================
    // LOCATION DTO'LARI
    // ==========================================

    public class CityDistrictDto
    {
        public int ID { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? PostaKodu { get; set; }
        // SemtBucakBelde ve Mahalle DB'den silindi — kaldırıldı
        public string DisplayText => $"{Il} / {Ilce}";
    }
    public class CitySelectDto
    {
        public string Il { get; set; } = string.Empty;
    }
    public class DistrictSelectDto
    {
        public int ID { get; set; }
        public string? Ilce { get; set; }
        // SemtBucakBelde ve Mahalle DB'den silindi — kaldırıldı
        public string DisplayText => Ilce ?? string.Empty;
    }
}