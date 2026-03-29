using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Validations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectAPI.Controllers
{
    [ApiController]
    [Route("api/user")]
    [Authorize] // Tüm controller login zorunlu
    public class UserController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IWebHostEnvironment _env;

        public UserController(DapperContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ── JWT yardımcıları ─────────────────────────────────────────────────
        private int GetUserId() =>
            int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : 0;

        private int GetCompanyId() =>
            int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : 0;

        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
        private bool IsAdmin() => User.IsInRole("Admin");   // SuperAdmin bu role girmez, ayrı kontrol
        private bool IsUser() => User.IsInRole("User");

        // ────────────────────────────────────────────────────────────────────
        #region CRUD

        /// <summary>
        /// Tüm kullanıcı listesi — Admin ve SuperAdmin
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> List()
        {
            const string sql = @"
                SELECT 
                    ID, Username, EMailAddress, FullName, PhoneNumber,
                    Picture, ISAdmin, Status, SendEmail, CompanyID,
                    CreatedDate, UpdatedDate
                FROM Users WITH (NOLOCK)
                ORDER BY ID DESC
            ";
            using var connection = _context.CreateConnection();
            IEnumerable<UserListDto> users = await connection.QueryAsync<UserListDto>(sql);
            return Ok(ApiResponse<IEnumerable<UserListDto>>.Ok(users));
        }

        /// <summary>
        /// Firmaya göre kullanıcılar:
        ///   - SuperAdmin/Admin → herkese
        ///   - User → sadece kendi firması
        /// </summary>
        [HttpGet("by-customer/{customerId:int}")]
        public async Task<IActionResult> GetByCustomer(int customerId)
        {
            if (IsUser() && GetCompanyId() != customerId)
                return Forbid();

            const string sql = @"
                SELECT 
                    ID, Username, EMailAddress, FullName, PhoneNumber,
                    Picture, ISAdmin, Status, SendEmail, CompanyID,
                    CreatedDate, UpdatedDate
                FROM Users WITH (NOLOCK)
                WHERE CompanyID = @CompanyID
                ORDER BY ISAdmin DESC, Username ASC
            ";
            using var connection = _context.CreateConnection();
            IEnumerable<UserListDto> users = await connection.QueryAsync<UserListDto>(sql, new { CompanyID = customerId });
            return Ok(ApiResponse<IEnumerable<UserListDto>>.Ok(users));
        }

        /// <summary>
        /// Kullanıcı detay:
        ///   - SuperAdmin → herkesi görebilir
        ///   - Admin → SuperAdmin hariç herkesi görebilir
        ///   - User → sadece kendisini
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            int callerId = GetUserId();

            if (IsUser())
            {
                if (callerId != id)
                    return Forbid();
            }
            else if (IsAdmin() && !IsSuperAdmin())
            {
                // Admin, SuperAdmin'i göremez
                using var checkConn = _context.CreateConnection();
                byte isAdmin = await checkConn.QueryFirstOrDefaultAsync<byte>(
                    "SELECT ISAdmin FROM Users WITH (NOLOCK) WHERE ID = @ID", new { ID = id });
                if (isAdmin == 2)
                    return Forbid();
            }

            const string sql = @"
                SELECT 
                    ID, Username, EMailAddress, FullName, PhoneNumber,
                    Picture, ISAdmin, Status, SendEmail, CompanyID,
                    CreatedDate, UpdatedDate
                FROM Users WITH (NOLOCK)
                WHERE ID = @ID
            ";
            using var connection = _context.CreateConnection();
            UserDto? user = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { ID = id });
            if (user == null)
                return NotFound(ApiResponse.NotFound("Kullanıcı bulunamadı"));

            return Ok(ApiResponse<UserDto>.Ok(user));
        }

        /// <summary>
        /// Kullanıcı oluştur — Admin ve SuperAdmin.
        /// Admin max ISAdmin=1 (User veya Admin) verebilir, SuperAdmin yetkisi veremez.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
        {
            // Admin SuperAdmin yetkisi veremez
            if (IsAdmin() && !IsSuperAdmin() && dto.ISAdmin == 2)
                return BadRequest(ApiResponse.Fail("SuperAdmin yetkisi veremezsiniz"));

            List<string> validationErrors = UserValidation.ValidateCreate(dto);
            if (validationErrors.Any())
                return BadRequest(ApiResponse.Fail(validationErrors));

            using var connection = _context.CreateConnection();

            bool usernameExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Username = @Username) THEN 1 ELSE 0 END",
                new { dto.Username });
            if (usernameExists)
                return BadRequest(ApiResponse.Fail("Bu kullanıcı adı zaten kullanılıyor"));

            bool emailExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE EMailAddress = @EMailAddress) THEN 1 ELSE 0 END",
                new { dto.EMailAddress });
            if (emailExists)
                return BadRequest(ApiResponse.Fail("Bu e-posta adresi zaten kullanılıyor"));

            string hashedPassword = PasswordHelper.Hash(dto.Password);

            const string sql = @"
                INSERT INTO Users (
                    Username, Password, EMailAddress, Picture,
                    CompanyID, ISAdmin, Status, FullName,
                    PhoneNumber, SendEmail, CreatedDate
                ) VALUES (
                    @Username, @Password, @EMailAddress, @Picture,
                    @CompanyID, @ISAdmin, @Status, @FullName,
                    @PhoneNumber, @SendEmail, GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";
            int newId = await connection.QuerySingleAsync<int>(sql, new
            {
                dto.Username,
                Password = hashedPassword,
                dto.EMailAddress,
                dto.Picture,
                dto.CompanyID,
                dto.ISAdmin,
                dto.Status,
                dto.FullName,
                dto.PhoneNumber,
                dto.SendEmail
            });

            return Ok(ApiResponse<int>.Ok(newId, "Kullanıcı başarıyla oluşturuldu"));
        }

        /// <summary>
        /// Kullanıcı güncelle:
        ///   - SuperAdmin → herkesi güncelleyebilir
        ///   - Admin:
        ///       • Kendini güncelleyemez (ISAdmin, CompanyID, Status değiştirme yok)
        ///       • SuperAdmin'i güncelleyemez
        ///       • Başka Admin'i güncelleyemez
        ///       • User'a max ISAdmin=1 verebilir
        ///   - User:
        ///       • Sadece kendini (Username, Email, FullName, Phone, Picture, Password)
        ///       • ISAdmin ve CompanyID değiştiremez
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto)
        {
            int callerId = GetUserId();
            using var connection = _context.CreateConnection();

            // Hedef kullanıcının mevcut bilgilerini çek
            var target = await connection.QueryFirstOrDefaultAsync<(int ID, byte ISAdmin, int CompanyID)>(
                "SELECT ID, ISAdmin, CompanyID FROM Users WHERE ID = @ID", new { ID = id });

            if (target.ID == 0)
                return NotFound(ApiResponse.NotFound("Kullanıcı bulunamadı"));

            if (IsSuperAdmin())
            {
                // SuperAdmin için kısıtlama yok, her şeyi yapabilir.
            }
            else if (IsAdmin())
            {
                // GÜNCELLEME: Admin kendisini güncelleyebilir.
                if (callerId == id)
                {
                    // Güvenlik: Admin kendi yetkisini veya şirketini bu yolla değiştiremez.
                    dto.ISAdmin = target.ISAdmin;
                    dto.CompanyID = target.CompanyID;
                }
                else
                {
                    // Admin başka birini güncelliyorsa:
                    // 1. SuperAdmin'i veya başka bir Admin'i güncelleyemez.
                    if (target.ISAdmin >= 1)
                        return Forbid();

                    // 2. Başkasına SuperAdmin yetkisi veremez.
                    if (dto.ISAdmin == 2)
                        return BadRequest(ApiResponse.Fail("SuperAdmin yetkisi veremezsiniz"));
                }
            }
            else if (IsUser())
            {
                // User sadece kendini güncelleyebilir
                if (callerId != id)
                    return Forbid();

                // User yetki ve şirket değiştiremez
                dto.ISAdmin = target.ISAdmin;
                dto.CompanyID = target.CompanyID;
            }
            else
            {
                return Forbid();
            }

            // --- Validasyonlar ---
            List<string> validationErrors = UserValidation.ValidateUpdate(dto);
            if (validationErrors.Any())
                return BadRequest(ApiResponse.Fail(validationErrors));

            // Username unique kontrolü (kendisi hariç)
            bool usernameExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Username = @Username AND ID != @ID) THEN 1 ELSE 0 END",
                new { dto.Username, ID = id });
            if (usernameExists)
                return BadRequest(ApiResponse.Fail("Bu kullanıcı adı zaten kullanılıyor"));

            // Email unique kontrolü (kendisi hariç)
            bool emailExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE EMailAddress = @EMailAddress AND ID != @ID) THEN 1 ELSE 0 END",
                new { dto.EMailAddress, ID = id });
            if (emailExists)
                return BadRequest(ApiResponse.Fail("Bu e-posta adresi zaten kullanılıyor"));

            // --- Veritabanı Güncelleme ---
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                string hashedPassword = PasswordHelper.Hash(dto.Password);
                const string sqlWithPass = @"
            UPDATE Users SET
                Username     = @Username,
                Password     = @Password,
                EMailAddress = @EMailAddress,
                Picture      = @Picture,
                CompanyID    = @CompanyID,
                ISAdmin      = @ISAdmin,
                Status       = @Status,
                FullName     = @FullName,
                PhoneNumber  = @PhoneNumber,
                SendEmail    = @SendEmail,
                UpdatedDate  = GETDATE()
            WHERE ID = @ID";

                await connection.ExecuteAsync(sqlWithPass, new
                {
                    dto.Username,
                    Password = hashedPassword,
                    dto.EMailAddress,
                    dto.Picture,
                    dto.CompanyID,
                    dto.ISAdmin,
                    dto.Status,
                    dto.FullName,
                    dto.PhoneNumber,
                    dto.SendEmail,
                    ID = id
                });
            }
            else
            {
                const string sqlWithoutPass = @"
            UPDATE Users SET
                Username     = @Username,
                EMailAddress = @EMailAddress,
                Picture      = @Picture,
                CompanyID    = @CompanyID,
                ISAdmin      = @ISAdmin,
                Status       = @Status,
                FullName     = @FullName,
                PhoneNumber  = @PhoneNumber,
                SendEmail    = @SendEmail,
                UpdatedDate  = GETDATE()
            WHERE ID = @ID";

                await connection.ExecuteAsync(sqlWithoutPass, new
                {
                    dto.Username,
                    dto.EMailAddress,
                    dto.Picture,
                    dto.CompanyID,
                    dto.ISAdmin,
                    dto.Status,
                    dto.FullName,
                    dto.PhoneNumber,
                    dto.SendEmail,
                    ID = id
                });
            }

            return Ok(ApiResponse.Ok("Kullanıcı başarıyla güncellendi"));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();

            const string checkSql = @"
        SELECT
            (SELECT COUNT(*) FROM Tickets WHERE CreatedByUserID  = @ID AND Status NOT IN (2,3) AND IsDeleted = 0) AS OpenTickets,
            (SELECT COUNT(*) FROM Tickets WHERE AssignedToUserID = @ID AND Status NOT IN (2,3) AND IsDeleted = 0) AS AssignedTickets,
            (SELECT COUNT(*) FROM KnowledgeBase WHERE CreatedBy = @ID) AS ArticleCount
    ";
            var counts = await connection.QueryFirstOrDefaultAsync(checkSql, new { ID = id });
            if (counts != null)
            {
                int openTickets = (int)counts.OpenTickets;
                int assignedTickets = (int)counts.AssignedTickets;
                int articleCount = (int)counts.ArticleCount;

                List<string> reasons = new();
                if (openTickets > 0) reasons.Add($"{openTickets} açık destek talebi");
                if (assignedTickets > 0) reasons.Add($"{assignedTickets} üzerine atanmış talep");
                if (articleCount > 0) reasons.Add($"{articleCount} bilgi bankası makalesi");

                if (reasons.Any())
                    return BadRequest(ApiResponse.Fail(
                        $"Kullanıcı silinemez. Bağlı kayıtlar var: {string.Join(", ", reasons)}"));
            }

            await connection.ExecuteAsync(
                "DELETE FROM TicketFiles WHERE UploadedByUserID = @ID", new { ID = id });
            await connection.ExecuteAsync(
                "DELETE FROM Tickets WHERE (CreatedByUserID = @ID OR AssignedToUserID = @ID) AND IsDeleted = 1",
                new { ID = id });
            await connection.ExecuteAsync(
                "UPDATE Tickets SET AssignedToUserID = NULL WHERE AssignedToUserID = @ID", new { ID = id });

            int affected = await connection.ExecuteAsync(
                "DELETE FROM Users WHERE ID = @ID", new { ID = id });
            if (affected == 0)
                return NotFound(ApiResponse.NotFound("Kullanıcı bulunamadı"));

            return Ok(ApiResponse.Ok("Kullanıcı başarıyla silindi"));
        }

        /// <summary>
        /// Statü değiştir — sadece SuperAdmin
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            const string sql = @"
    UPDATE Users SET
        Status      = CASE WHEN Status = 1 THEN 0 ELSE 1 END,
        SendEmail   = CASE WHEN Status = 1 THEN 0 ELSE SendEmail END,
        UpdatedDate = GETDATE()
    WHERE ID = @ID;
    SELECT Status FROM Users WHERE ID = @ID;
";
            using var connection = _context.CreateConnection();
            bool? newStatus = await connection.QueryFirstOrDefaultAsync<bool?>(sql, new { ID = id });
            if (newStatus == null)
                return NotFound(ApiResponse.NotFound("Kullanıcı bulunamadı"));
            return Ok(ApiResponse<bool>.Ok(newStatus.Value,
                newStatus.Value ? "Kullanıcı aktif edildi" : "Kullanıcı pasif edildi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Resim Yükleme

        /// <summary>
        /// Resim yükle:
        ///   - SuperAdmin → herkese
        ///   - Admin → SuperAdmin hariç herkese
        ///   - User → sadece kendine
        /// </summary>
        [HttpPost("{id:int}/picture")]
        public async Task<IActionResult> UploadPicture(int id, IFormFile file)
        {
            int callerId = GetUserId();
            using var connection = _context.CreateConnection();

            if (IsSuperAdmin())
            {
                // Her şey serbest
            }
            else if (IsAdmin())
            {
                byte targetIsAdmin = await connection.QueryFirstOrDefaultAsync<byte>(
                    "SELECT ISAdmin FROM Users WITH (NOLOCK) WHERE ID = @ID", new { ID = id });
                if (targetIsAdmin == 2)
                    return Forbid();
            }
            else
            {
                if (callerId != id)
                    return Forbid();
            }

            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            string[] allowed = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension))
                return BadRequest(ApiResponse.Fail("Sadece JPG, PNG, GIF ve WEBP formatları desteklenir"));
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Resim boyutu 5MB'dan büyük olamaz"));

            string? oldPicture = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT Picture FROM Users WHERE ID = @ID", new { ID = id });

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string folderPath = Path.Combine(webRoot, "uploads", "users");
            Directory.CreateDirectory(folderPath);
            string fileName = $"{Guid.NewGuid():N}{extension}";
            string fullPath = Path.Combine(folderPath, fileName);
            string relativePath = $"/uploads/users/{fileName}";

            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            await connection.ExecuteAsync(
                "UPDATE Users SET Picture = @Picture, UpdatedDate = GETDATE() WHERE ID = @ID",
                new { Picture = relativePath, ID = id });

            if (!string.IsNullOrEmpty(oldPicture))
            {
                string oldFullPath = Path.Combine(webRoot, oldPicture.TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath))
                    System.IO.File.Delete(oldFullPath);
            }

            return Ok(ApiResponse<string>.Ok(relativePath, "Profil resmi güncellendi"));
        }

        /// <summary>
        /// Resim sil — UploadPicture ile aynı kural
        /// </summary>
        [HttpDelete("{id:int}/picture")]
        public async Task<IActionResult> DeletePicture(int id)
        {
            int callerId = GetUserId();
            using var connection = _context.CreateConnection();

            if (IsSuperAdmin())
            {
                // Her şey serbest
            }
            else if (IsAdmin())
            {
                byte targetIsAdmin = await connection.QueryFirstOrDefaultAsync<byte>(
                    "SELECT ISAdmin FROM Users WITH (NOLOCK) WHERE ID = @ID", new { ID = id });
                if (targetIsAdmin == 2)
                    return Forbid();
            }
            else
            {
                if (callerId != id)
                    return Forbid();
            }

            string? picture = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT Picture FROM Users WHERE ID = @ID", new { ID = id });
            if (string.IsNullOrEmpty(picture))
                return BadRequest(ApiResponse.Fail("Kullanıcının profil resmi yok"));

            await connection.ExecuteAsync(
                "UPDATE Users SET Picture = NULL, UpdatedDate = GETDATE() WHERE ID = @ID", new { ID = id });

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, picture.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            return Ok(ApiResponse.Ok("Profil resmi silindi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Kullanıcı Adı / Email Kontrol — Tüm roller

        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username, [FromQuery] int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Ok(ApiResponse<bool>.Ok(false));

            const string sql = @"
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Users WITH (NOLOCK)
                    WHERE Username = @Username
                    AND (@ExcludeId = 0 OR ID != @ExcludeId)
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
            ";
            using var connection = _context.CreateConnection();
            bool exists = await connection.QuerySingleAsync<bool>(sql, new { Username = username, ExcludeId = excludeId });
            return Ok(ApiResponse<bool>.Ok(exists));
        }

        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmail([FromQuery] string email, [FromQuery] int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Ok(ApiResponse<bool>.Ok(false));

            const string sql = @"
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Users WITH (NOLOCK)
                    WHERE EMailAddress = @Email
                    AND (@ExcludeId = 0 OR ID != @ExcludeId)
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
            ";
            using var connection = _context.CreateConnection();
            bool exists = await connection.QuerySingleAsync<bool>(sql, new { Email = email, ExcludeId = excludeId });
            return Ok(ApiResponse<bool>.Ok(exists));
        }

        #endregion
  
    }
}