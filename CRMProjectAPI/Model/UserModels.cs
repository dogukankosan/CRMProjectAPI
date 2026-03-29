namespace CRMProjectAPI.Models
{
    // ==================== USER DTO'LARI ====================

    public class UserDto
    {
        public int ID { get; set; }
        public string Username { get; set; } = string.Empty;

        public string EMailAddress { get; set; } = string.Empty;
        public string? Picture { get; set; }            // Opsiyonel
        public int CompanyID { get; set; }
        public byte ISAdmin { get; set; } = 0;
        public bool Status { get; set; } = true;
        public string? FullName { get; set; }           // Opsiyonel
        public string? PhoneNumber { get; set; }        // Opsiyonel
        public bool SendEmail { get; set; } = true;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }

    public class UserListDto
    {
        public int ID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string EMailAddress { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Picture { get; set; }
        public byte ISAdmin { get; set; } = 0;
        public bool Status { get; set; }
        public bool SendEmail { get; set; }
        public int CompanyID { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }

    public class UserCreateDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string EMailAddress { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public int CompanyID { get; set; }
        public byte ISAdmin { get; set; } = 0;
        public bool Status { get; set; } = true;
        public string FullName { get; set; } = string.Empty;  // ← zorunlu
        public string? PhoneNumber { get; set; }
        public bool SendEmail { get; set; } = true;
    }
    public class UserUpdateDto
    {
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string EMailAddress { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public int CompanyID { get; set; }
        public byte ISAdmin { get; set; } = 0;
        public bool Status { get; set; }
        public string FullName { get; set; } = string.Empty;  // ← zorunlu
        public string? PhoneNumber { get; set; }
        public bool SendEmail { get; set; }
    }
}