namespace CRMProjectUI.Models
{
    public class UserDto
    {
        public int ID { get; set; }
        public string Username { get; set; } = string.Empty;
 
        public string EMailAddress { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public int CompanyID { get; set; }
        public byte ISAdmin { get; set; } = 0;
        public bool Status { get; set; } = true;
        public string FullName { get; set; } = string.Empty; // ← zorunlu
        public string? PhoneNumber { get; set; }
        public bool SendEmail { get; set; } = true;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public static string ApiBaseUrl { get; set; } = "";
        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Username;
        public string RoleText => ISAdmin == 2 ? "Süper Admin"
                                : ISAdmin == 1 ? "Admin"
                                : "Kullanıcı";
        public string StatusText => Status ? "Aktif" : "Pasif";
        public string PictureUrl => !string.IsNullOrEmpty(Picture)
            ? $"{ApiBaseUrl}{Picture}"
            : "/adminThema/assets/img/user.png";
    }

    public class UserListDto
    {
        public int ID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string EMailAddress { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty; // ← zorunlu
        public string? PhoneNumber { get; set; }
        public string? Picture { get; set; }
        public byte ISAdmin { get; set; }
        public bool Status { get; set; }
        public bool SendEmail { get; set; }
        public int CompanyID { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public static string ApiBaseUrl { get; set; } = "";
        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Username;
        public string RoleText => ISAdmin == 2 ? "Süper Admin"
                                : ISAdmin == 1 ? "Admin"
                                : "Kullanıcı";
        public string PictureUrl => !string.IsNullOrEmpty(Picture)
            ? $"{ApiBaseUrl}{Picture}"
            : "/adminThema/assets/img/user.png";
    }

    public class UserCreateDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string EMailAddress { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public int CompanyID { get; set; } = 1;
        public byte ISAdmin { get; set; } = 0;
        public bool Status { get; set; } = true;
        public string FullName { get; set; } = string.Empty; // ← zorunlu
        public string? PhoneNumber { get; set; }
        public bool SendEmail { get; set; } = true;
    }

    public class UserUpdateDto
    {
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string EMailAddress { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public int CompanyID { get; set; } = 1;
        public byte ISAdmin { get; set; }
        public bool Status { get; set; }
        public string FullName { get; set; } = string.Empty; // ← zorunlu
        public string? PhoneNumber { get; set; }
        public bool SendEmail { get; set; }
    }
}