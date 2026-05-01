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
    [Route("api/certificate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class CertificateController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IWebHostEnvironment _env;
        public CertificateController(DapperContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        // ── JWT yardımcıları ─────────────────────────────────────────────
        private int GetUserId() =>
            int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : 0;
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        // ────────────────────────────────────────────────────────────────
        #region CRUD

        /// <summary>
        /// Tüm sertifikalar — Admin/SuperAdmin görebilir
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List()
        {
            const string sql = @"
                SELECT
                    c.ID, c.UserID, c.Title, c.Notes,
                    c.OriginalFileName, c.RelativePath, c.FileSizeBytes,
                    c.UploadedByUserID, c.CreatedDate, c.UpdatedDate,
                    ISNULL(u.FullName, u.Username)   AS UserFullName,
                    u.Picture                         AS UserPicture,
                    ISNULL(ub.FullName, ub.Username) AS UploadedByName
                FROM Certificates c WITH (NOLOCK)
                INNER JOIN Users u  WITH (NOLOCK) ON c.UserID           = u.ID
                INNER JOIN Users ub WITH (NOLOCK) ON c.UploadedByUserID = ub.ID
                WHERE c.IsDeleted = 0
                ORDER BY c.CreatedDate DESC
            ";
            using var connection = _context.CreateConnection();
            var list = await connection.QueryAsync<CertificateDto>(sql);
            return Ok(ApiResponse<IEnumerable<CertificateDto>>.Ok(list));
        }
        /// <summary>
        /// Belirli bir kullanıcının sertifikaları
        /// </summary>
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            const string sql = @"
                SELECT
                    c.ID, c.UserID, c.Title, c.Notes,
                    c.OriginalFileName, c.RelativePath, c.FileSizeBytes,
                    c.UploadedByUserID, c.CreatedDate, c.UpdatedDate,
                    ISNULL(u.FullName, u.Username)   AS UserFullName,
                    u.Picture                         AS UserPicture,
                    ISNULL(ub.FullName, ub.Username) AS UploadedByName
                FROM Certificates c WITH (NOLOCK)
                INNER JOIN Users u  WITH (NOLOCK) ON c.UserID           = u.ID
                INNER JOIN Users ub WITH (NOLOCK) ON c.UploadedByUserID = ub.ID
                WHERE c.IsDeleted = 0 AND c.UserID = @UserID
                ORDER BY c.CreatedDate DESC
            ";
            using var connection = _context.CreateConnection();
            var list = await connection.QueryAsync<CertificateDto>(sql, new { UserID = userId });
            return Ok(ApiResponse<IEnumerable<CertificateDto>>.Ok(list));
        }
        /// <summary>
        /// Sertifika detay
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            const string sql = @"
                SELECT
                    c.ID, c.UserID, c.Title, c.Notes,
                    c.OriginalFileName, c.RelativePath, c.FileSizeBytes,
                    c.UploadedByUserID, c.CreatedDate, c.UpdatedDate,
                    ISNULL(u.FullName, u.Username)   AS UserFullName,
                    u.Picture                         AS UserPicture,
                    ISNULL(ub.FullName, ub.Username) AS UploadedByName
                FROM Certificates c WITH (NOLOCK)
                INNER JOIN Users u  WITH (NOLOCK) ON c.UserID           = u.ID
                INNER JOIN Users ub WITH (NOLOCK) ON c.UploadedByUserID = ub.ID
                WHERE c.ID = @ID AND c.IsDeleted = 0
            ";
            using var connection = _context.CreateConnection();
            var cert = await connection.QueryFirstOrDefaultAsync<CertificateDto>(sql, new { ID = id });
            if (cert == null)
                return NotFound(ApiResponse.NotFound("Sertifika bulunamadı"));
            return Ok(ApiResponse<CertificateDto>.Ok(cert));
        }
        /// <summary>
        /// Sertifika ekle — Admin kendi adına veya başkası adına ekleyebilir
        /// Yükleme yapan kişi UploadedByUserID olarak kaydedilir
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CertificateCreateDto dto)
        {
            var errors = CertificateValidation.ValidateCreate(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));
            using var connection = _context.CreateConnection();
            // Hedef kullanıcı admin veya superadmin mi kontrol et
            var targetUser = await connection.QueryFirstOrDefaultAsync(
                "SELECT ID, ISAdmin, Status FROM Users WITH (NOLOCK) WHERE ID = @ID",
                new { ID = dto.UserID });
            if (targetUser == null)
                return NotFound(ApiResponse.NotFound("Kullanıcı bulunamadı"));
            if ((byte)targetUser.ISAdmin == 0)
                return BadRequest(ApiResponse.Fail("Sertifika sadece Admin veya SuperAdmin kullanıcılara eklenebilir"));
            if (!(bool)targetUser.Status)
                return BadRequest(ApiResponse.Fail("Pasif kullanıcıya sertifika eklenemez"));
            // Base64 → fiziksel dosya
            byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);
            // Boyut kontrolü — 20MB
            if (fileBytes.Length > 20 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya boyutu 20MB'dan büyük olamaz"));
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string storedFileName = $"{Guid.NewGuid():N}.pdf";
            string folderPath = Path.Combine(webRoot, "uploads", "certificates", dto.UserID.ToString());
            Directory.CreateDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, storedFileName);
            string relativePath = $"/uploads/certificates/{dto.UserID}/{storedFileName}";
            try
            {
                await System.IO.File.WriteAllBytesAsync(fullPath, fileBytes);
            }
            catch
            {
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                throw;
            }
            const string sql = @"
                INSERT INTO Certificates (
                    UserID, Title, Notes,
                    OriginalFileName, StoredFileName, RelativePath,
                    FileSizeBytes, UploadedByUserID, CreatedDate, IsDeleted
                ) VALUES (
                    @UserID, @Title, @Notes,
                    @OriginalFileName, @StoredFileName, @RelativePath,
                    @FileSizeBytes, @UploadedByUserID, GETDATE(), 0
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";
            int newId = await connection.QuerySingleAsync<int>(sql, new
            {
                dto.UserID,
                dto.Title,
                dto.Notes,
                dto.OriginalFileName,
                StoredFileName = storedFileName,
                RelativePath = relativePath,
                FileSizeBytes = (long)fileBytes.Length,
                UploadedByUserID = GetUserId()
            });
            return Ok(ApiResponse<int>.Ok(newId, "Sertifika başarıyla eklendi"));
        }
        /// <summary>
        /// Sertifika güncelle:
        ///   - Admin → sadece kendi yüklediği (UploadedByUserID = kendi ID'si)
        ///   - SuperAdmin → herkesi güncelleyebilir
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CertificateUpdateDto dto)
        {
            var errors = CertificateValidation.ValidateUpdate(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));
            using var connection = _context.CreateConnection();
            var cert = await connection.QueryFirstOrDefaultAsync(
                "SELECT ID, UserID, UploadedByUserID, StoredFileName, RelativePath FROM Certificates WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });
            if (cert == null)
                return NotFound(ApiResponse.NotFound("Sertifika bulunamadı"));
            // Admin kısıtlaması — kendi yüklemediğini güncelleyemez
            if (!IsSuperAdmin() && (int)cert.UploadedByUserID != GetUserId())
                return BadRequest(ApiResponse.Fail("Bu sertifikayı güncelleme yetkiniz yok"));
            string? newRelativePath = null;
            string? newStoredFileName = null;
            long? newFileSize = null;
            // Yeni dosya geldiyse fiziksel işlem
            if (!string.IsNullOrWhiteSpace(dto.FileBase64))
            {
                byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);

                if (fileBytes.Length > 20 * 1024 * 1024)
                    return BadRequest(ApiResponse.Fail("Dosya boyutu 20MB'dan büyük olamaz"));
                string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                // Eski dosyayı sil
                string oldFullPath = Path.Combine(webRoot, ((string)cert.RelativePath).TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath))
                    System.IO.File.Delete(oldFullPath);
                // Yeni dosyayı kaydet
                newStoredFileName = $"{Guid.NewGuid():N}.pdf";
                string folderPath = Path.Combine(webRoot, "uploads", "certificates", ((int)cert.UserID).ToString());
                Directory.CreateDirectory(folderPath);
                string fullPath = Path.Combine(folderPath, newStoredFileName);
                newRelativePath = $"/uploads/certificates/{cert.UserID}/{newStoredFileName}";
                try
                {
                    await System.IO.File.WriteAllBytesAsync(fullPath, fileBytes);
                    newFileSize = (long)fileBytes.Length;
                }
                catch
                {
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                    throw;
                }
            }
            const string sql = @"
                UPDATE Certificates SET
                    Title            = @Title,
                    Notes            = @Notes,
                    OriginalFileName = CASE WHEN @OriginalFileName IS NOT NULL THEN @OriginalFileName ELSE OriginalFileName END,
                    StoredFileName   = CASE WHEN @StoredFileName   IS NOT NULL THEN @StoredFileName   ELSE StoredFileName   END,
                    RelativePath     = CASE WHEN @RelativePath     IS NOT NULL THEN @RelativePath     ELSE RelativePath     END,
                    FileSizeBytes    = CASE WHEN @FileSizeBytes    IS NOT NULL THEN @FileSizeBytes    ELSE FileSizeBytes    END,
                    UpdatedDate      = GETDATE()
                WHERE ID = @ID AND IsDeleted = 0
            ";
            await connection.ExecuteAsync(sql, new
            {
                dto.Title,
                dto.Notes,
                OriginalFileName = string.IsNullOrWhiteSpace(dto.FileBase64) ? null : dto.OriginalFileName,
                StoredFileName = newStoredFileName,
                RelativePath = newRelativePath,
                FileSizeBytes = newFileSize,
                ID = id
            });
            return Ok(ApiResponse.Ok("Sertifika başarıyla güncellendi"));
        }
        /// <summary>
        /// Sertifika sil:
        ///   - Admin → sadece kendi yüklediği
        ///   - SuperAdmin → herkesinkini
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();
            var cert = await connection.QueryFirstOrDefaultAsync(
                "SELECT ID, UploadedByUserID, RelativePath FROM Certificates WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });
            if (cert == null)
                return NotFound(ApiResponse.NotFound("Sertifika bulunamadı"));
            // Admin kısıtlaması
            if (!IsSuperAdmin() && (int)cert.UploadedByUserID != GetUserId())
                return BadRequest(ApiResponse.Fail("Bu sertifikayı silme yetkiniz yok"));
            // Soft delete
            await connection.ExecuteAsync(@"
                UPDATE Certificates SET
                    IsDeleted       = 1,
                    DeletedDate     = GETDATE(),
                    DeletedByUserID = @DeletedBy
                WHERE ID = @ID",
                new { DeletedBy = GetUserId(), ID = id });
            // Fiziksel dosyayı sil
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, ((string)cert.RelativePath).TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
            return Ok(ApiResponse.Ok("Sertifika başarıyla silindi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────
        #region Download

        /// <summary>
        /// Sertifika indir — Admin/SuperAdmin
        /// </summary>
        [HttpGet("{id:int}/download")]
        public async Task<IActionResult> Download(int id)
        {
            using var connection = _context.CreateConnection();
            var cert = await connection.QueryFirstOrDefaultAsync(
                "SELECT OriginalFileName, RelativePath FROM Certificates WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });
            if (cert == null)
                return NotFound(ApiResponse.NotFound("Sertifika bulunamadı"));
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, ((string)cert.RelativePath).TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
                return NotFound(ApiResponse.NotFound("Dosya fiziksel olarak bulunamadı"));
            byte[] bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            string encodedName = Uri.EscapeDataString((string)cert.OriginalFileName);
            Response.Headers.Append("Content-Disposition",
                $"attachment; filename=\"{encodedName}\"; filename*=UTF-8''{encodedName}");
            return File(bytes, "application/pdf", (string)cert.OriginalFileName);
        }
        #endregion
    }
}