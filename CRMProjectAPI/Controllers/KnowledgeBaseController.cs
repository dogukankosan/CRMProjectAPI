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
    [Route("api/knowledgebase")]
    [Authorize]
    public class KnowledgeBaseController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<KnowledgeBaseController> _logger;

        public KnowledgeBaseController(
            DapperContext context,
            IWebHostEnvironment env,
            ILogger<KnowledgeBaseController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        private int GetUserId() =>
            int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : 0;
        private int GetCompanyId() =>
            int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : 0;
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
        private bool IsAdmin() => User.IsInRole("Admin") || IsSuperAdmin();
        private bool IsUser() => User.IsInRole("User");

        // ────────────────────────────────────────────────────────────────────
        #region Liste

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] string? search = null,
            [FromQuery] short? logoProduct = null,
            [FromQuery] string? category = null)
        {
            using var connection = _context.CreateConnection();

            if (IsUser() && !IsAdmin())
            {
                // User: sadece kendi firmasının ürünleriyle ilgili, aktif, public
                const string sql = @"
                    SELECT DISTINCT
                        kb.ID, kb.Title, kb.Description, kb.CodeLanguage,
                        kb.Category, kb.IsPublic, kb.IsActive,
                        ISNULL(u.FullName, u.Username) AS CreatedByName,
                        kb.CreatedBy,
                        kb.CreatedDate,
                        (SELECT COUNT(*) FROM KnowledgeBaseFiles f
                         WHERE f.KnowledgeBaseID = kb.ID AND f.IsDeleted = 0) AS FileCount,
                        (SELECT STRING_AGG(lp.LogoProductName, ', ')
                         FROM KnowledgeBaseProducts kbp2
                         INNER JOIN LogoProducts lp ON kbp2.LogoProductID = lp.ID
                         WHERE kbp2.KnowledgeBaseID = kb.ID) AS ProductNames
                    FROM KnowledgeBase kb WITH (NOLOCK)
                    LEFT JOIN Users u WITH (NOLOCK) ON kb.CreatedBy = u.ID
                    INNER JOIN KnowledgeBaseProducts kbp WITH (NOLOCK) ON kb.ID = kbp.KnowledgeBaseID
                    INNER JOIN CustomersLogoProducts clp WITH (NOLOCK)
                        ON kbp.LogoProductID = clp.LogoProductID
                        AND clp.CustomerID   = @CompanyId
                    WHERE kb.IsActive = 1
                      AND kb.IsPublic = 1
                      AND (@Search IS NULL OR kb.Title LIKE '%' + @Search + '%'
                           OR kb.Description LIKE '%' + @Search + '%')
                      AND (@LogoProduct IS NULL OR kbp.LogoProductID = @LogoProduct)
                      AND (@Category IS NULL OR kb.Category = @Category)
                    ORDER BY kb.CreatedDate DESC
                ";
                var items = await connection.QueryAsync<KnowledgeBaseListDto>(sql, new
                {
                    CompanyId = GetCompanyId(),
                    Search = string.IsNullOrEmpty(search) ? null : search,
                    LogoProduct = logoProduct,
                    Category = string.IsNullOrEmpty(category) ? null : category
                });
                return Ok(ApiResponse<IEnumerable<KnowledgeBaseListDto>>.Ok(items));
            }
            else
            {
                // Admin/SuperAdmin: hepsi
                const string sql = @"
                    SELECT DISTINCT
                        kb.ID, kb.Title, kb.Description, kb.CodeLanguage,
                        kb.Category, kb.IsPublic, kb.IsActive,  kb.CreatedBy,  
                        ISNULL(u.FullName, u.Username) AS CreatedByName,
                        kb.CreatedDate,
                        (SELECT COUNT(*) FROM KnowledgeBaseFiles f
                         WHERE f.KnowledgeBaseID = kb.ID AND f.IsDeleted = 0) AS FileCount,
                        (SELECT STRING_AGG(lp.LogoProductName, ', ')
                         FROM KnowledgeBaseProducts kbp2
                         INNER JOIN LogoProducts lp ON kbp2.LogoProductID = lp.ID
                         WHERE kbp2.KnowledgeBaseID = kb.ID) AS ProductNames
                    FROM KnowledgeBase kb WITH (NOLOCK)
                    LEFT JOIN Users u WITH (NOLOCK) ON kb.CreatedBy = u.ID
                    LEFT JOIN KnowledgeBaseProducts kbp WITH (NOLOCK) ON kb.ID = kbp.KnowledgeBaseID
                    WHERE (@Search IS NULL OR kb.Title LIKE '%' + @Search + '%'
                           OR kb.Description LIKE '%' + @Search + '%')
                      AND (@LogoProduct IS NULL OR kbp.LogoProductID = @LogoProduct)
                      AND (@Category IS NULL OR kb.Category = @Category)
                    ORDER BY kb.CreatedDate DESC
                ";
                var items = await connection.QueryAsync<KnowledgeBaseListDto>(sql, new
                {
                    Search = string.IsNullOrEmpty(search) ? null : search,
                    LogoProduct = logoProduct,
                    Category = string.IsNullOrEmpty(category) ? null : category
                });
                return Ok(ApiResponse<IEnumerable<KnowledgeBaseListDto>>.Ok(items));
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Detay

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var connection = _context.CreateConnection();

            const string sql = @"
                SELECT
                    kb.*,
                    ISNULL(u.FullName, u.Username) AS CreatedByName
                FROM KnowledgeBase kb WITH (NOLOCK)
                LEFT JOIN Users u WITH (NOLOCK) ON kb.CreatedBy = u.ID
                WHERE kb.ID = @ID
            ";
            var kb = await connection.QueryFirstOrDefaultAsync<KnowledgeBaseDto>(sql, new { ID = id });
            if (kb == null)
                return NotFound(ApiResponse.NotFound("Makale bulunamadı"));

            // Ürünler
            const string productsSql = @"
                SELECT kbp.KnowledgeBaseID, kbp.LogoProductID, lp.LogoProductName
                FROM KnowledgeBaseProducts kbp WITH (NOLOCK)
                INNER JOIN LogoProducts lp WITH (NOLOCK) ON kbp.LogoProductID = lp.ID
                WHERE kbp.KnowledgeBaseID = @ID
            ";
            kb.Products = (await connection.QueryAsync<KnowledgeBaseProductDto>(
                productsSql, new { ID = id })).ToList();

            // User erişim kontrolü
            if (IsUser() && !IsAdmin())
            {
                if (!kb.IsPublic || !kb.IsActive)
                    return Forbid();

                int companyId = GetCompanyId();
                bool hasProduct = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM KnowledgeBaseProducts kbp
                        INNER JOIN CustomersLogoProducts clp ON kbp.LogoProductID = clp.LogoProductID
                        WHERE kbp.KnowledgeBaseID = @ID AND clp.CustomerID = @CompanyId
                    ) THEN 1 ELSE 0 END",
                    new { ID = id, CompanyId = companyId });

                if (!hasProduct)
                    return Forbid();
            }

            // Dosyalar
            const string filesSql = @"
                SELECT * FROM KnowledgeBaseFiles WITH (NOLOCK)
                WHERE KnowledgeBaseID = @ID AND IsDeleted = 0
                ORDER BY UploadedDate ASC
            ";
            kb.Files = (await connection.QueryAsync<KnowledgeBaseFileDto>(
                filesSql, new { ID = id })).ToList();

            return Ok(ApiResponse<KnowledgeBaseDto>.Ok(kb));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region CRUD — Admin/SuperAdmin

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] KnowledgeBaseCreateDto dto)
        {
            List<string> errors = KnowledgeBaseValidation.Validate(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string sql = @"
                    INSERT INTO KnowledgeBase (
                        Title, Description, CodeBlock, CodeLanguage,
                        Category, IsPublic, IsActive, CreatedBy, CreatedDate
                    ) VALUES (
                        @Title, @Description, @CodeBlock, @CodeLanguage,
                        @Category, @IsPublic, @IsActive, @CreatedBy, GETDATE()
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                ";
                int newId = await connection.QuerySingleAsync<int>(sql, new
                {
                    dto.Title,
                    dto.Description,
                    dto.CodeBlock,
                    dto.CodeLanguage,
                    dto.Category,
                    dto.IsPublic,
                    dto.IsActive,
                    CreatedBy = GetUserId()
                }, transaction);

                // Ürünleri kaydet
                await SaveProducts(connection, transaction, newId, dto.LogoProductIDs);

                transaction.Commit();
                return Ok(ApiResponse<int>.Ok(newId, "Makale oluşturuldu"));
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(int id, [FromBody] KnowledgeBaseCreateDto dto)
        {
            List<string> errors = KnowledgeBaseValidation.Validate(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();

            // Yetki kontrolü — transaction açmadan önce
            if (IsAdmin() && !IsSuperAdmin())
            {
                var existing = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT CreatedBy FROM KnowledgeBase WHERE ID = @ID", new { ID = id });
                if (existing == null)
                    return NotFound(ApiResponse.NotFound("Makale bulunamadı"));
                if ((int?)existing.CreatedBy != GetUserId())
                    return StatusCode(403, ApiResponse.Fail("Bu makaleyi düzenleme yetkiniz yok"));
            }

            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"
                    UPDATE KnowledgeBase SET
                        Title        = @Title,
                        Description  = @Description,
                        CodeBlock    = @CodeBlock,
                        CodeLanguage = @CodeLanguage,
                        Category     = @Category,
                        IsPublic     = @IsPublic,
                        IsActive     = @IsActive,
                        UpdatedBy    = @UpdatedBy,
                        UpdatedDate  = GETDATE()
                    WHERE ID = @ID
                ";
                int affected = await connection.ExecuteAsync(sql, new
                {
                    dto.Title,
                    dto.Description,
                    dto.CodeBlock,
                    dto.CodeLanguage,
                    dto.Category,
                    dto.IsPublic,
                    dto.IsActive,
                    UpdatedBy = GetUserId(),
                    ID = id
                }, transaction);

                if (affected == 0)
                {
                    transaction.Rollback();
                    return NotFound(ApiResponse.NotFound("Makale bulunamadı"));
                }

                // Ürünleri güncelle
                await SaveProducts(connection, transaction, id, dto.LogoProductIDs);

                transaction.Commit();
                return Ok(ApiResponse.Ok("Makale güncellendi"));
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();

            // Admin sadece kendi kaydını silebilir
            if (IsAdmin() && !IsSuperAdmin())
            {
                var existing = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT CreatedBy FROM KnowledgeBase WHERE ID = @ID", new { ID = id });
                if (existing == null)
                    return NotFound(ApiResponse.NotFound("Makale bulunamadı"));
                if ((int?)existing.CreatedBy != GetUserId())
                    return StatusCode(403, ApiResponse.Fail("Bu makaleyi silme yetkiniz yok"));
            }
            // Fiziksel dosyaları sil
            var files = await connection.QueryAsync<KnowledgeBaseFileDto>(
                "SELECT * FROM KnowledgeBaseFiles WHERE KnowledgeBaseID = @ID AND IsDeleted = 0",
                new { ID = id });

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            foreach (var file in files)
            {
                string fullPath = Path.Combine(webRoot, file.RelativePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }

            await connection.ExecuteAsync(
                "DELETE FROM KnowledgeBaseFiles   WHERE KnowledgeBaseID = @ID", new { ID = id });
            await connection.ExecuteAsync(
                "DELETE FROM KnowledgeBaseProducts WHERE KnowledgeBaseID = @ID", new { ID = id });

            int affected = await connection.ExecuteAsync(
                "DELETE FROM KnowledgeBase WHERE ID = @ID", new { ID = id });

            if (affected == 0)
                return NotFound(ApiResponse.NotFound("Makale bulunamadı"));

            return Ok(ApiResponse.Ok("Makale silindi"));
        }

        [HttpPatch("{id:int}/toggle")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            const string sql = @"
                UPDATE KnowledgeBase SET
                    IsActive    = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
                    UpdatedDate = GETDATE()
                WHERE ID = @ID;
                SELECT IsActive FROM KnowledgeBase WHERE ID = @ID;
            ";
            using var connection = _context.CreateConnection();
            var newStatus = await connection.QueryFirstOrDefaultAsync<bool?>(sql, new { ID = id });
            if (newStatus == null)
                return NotFound(ApiResponse.NotFound("Makale bulunamadı"));

            return Ok(ApiResponse<bool>.Ok(newStatus.Value,
                newStatus.Value ? "Makale aktif edildi" : "Makale pasif edildi"));
        }

        [HttpPatch("{id:int}/toggle-public")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> TogglePublic(int id)
        {
            const string sql = @"
                UPDATE KnowledgeBase SET
                    IsPublic    = CASE WHEN IsPublic = 1 THEN 0 ELSE 1 END,
                    UpdatedDate = GETDATE()
                WHERE ID = @ID;
                SELECT IsPublic FROM KnowledgeBase WHERE ID = @ID;
            ";
            using var connection = _context.CreateConnection();
            var newStatus = await connection.QueryFirstOrDefaultAsync<bool?>(sql, new { ID = id });
            if (newStatus == null)
                return NotFound(ApiResponse.NotFound("Makale bulunamadı"));

            return Ok(ApiResponse<bool>.Ok(newStatus.Value,
                newStatus.Value ? "Müşterilere açıldı" : "Müşterilerden gizlendi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Dosyalar

        [HttpPost("{id:int}/files")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UploadFile(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            HashSet<string> allowed = new()
                { ".pdf", ".xls", ".xlsx", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".txt", ".zip" };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension))
                return BadRequest(ApiResponse.Fail("Desteklenmeyen dosya formatı"));
            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya boyutu 50MB'dan büyük olamaz"));

            string fileType = extension switch
            {
                ".pdf" => "PDF",
                ".xls" or ".xlsx" => "Excel",
                ".doc" or ".docx" => "Word",
                ".jpg" or ".jpeg" or ".png" => "Image",
                ".txt" => "Text",
                ".zip" => "Archive",
                _ => "Other"
            };

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string storedName = $"{Guid.NewGuid():N}{extension}";
            string folderPath = Path.Combine(webRoot, "uploads", "knowledgebase", id.ToString());
            Directory.CreateDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, storedName);
            string relativePath = $"/uploads/knowledgebase/{id}/{storedName}";

            await System.IO.File.WriteAllBytesAsync(fullPath, fileBytes);

            const string sql = @"
                INSERT INTO KnowledgeBaseFiles (
                    KnowledgeBaseID, OriginalFileName, StoredFileName, RelativePath,
                    FileExtension, MimeType, FileSizeBytes, FileType,
                    UploadedBy, UploadedDate, IsDeleted
                ) VALUES (
                    @KnowledgeBaseID, @OriginalFileName, @StoredFileName, @RelativePath,
                    @FileExtension, @MimeType, @FileSizeBytes, @FileType,
                    @UploadedBy, GETDATE(), 0
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";
            using var connection = _context.CreateConnection();
            int newId = await connection.QuerySingleAsync<int>(sql, new
            {
                KnowledgeBaseID = id,
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                RelativePath = relativePath,
                FileExtension = extension,
                MimeType = file.ContentType,
                FileSizeBytes = file.Length,
                FileType = fileType,
                UploadedBy = GetUserId()
            });

            return Ok(ApiResponse<int>.Ok(newId, "Dosya yüklendi"));
        }
        [HttpDelete("files/{fileId:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteFile(int fileId)
        {
            using var connection = _context.CreateConnection();

            // Dosyayı ve ilgili makalenin sahibini çek
            var fileInfo = await connection.QueryFirstOrDefaultAsync(@"
        SELECT kf.RelativePath, kb.CreatedBy
        FROM KnowledgeBaseFiles kf
        INNER JOIN KnowledgeBase kb ON kf.KnowledgeBaseID = kb.ID
        WHERE kf.ID = @ID AND kf.IsDeleted = 0",
                new { ID = fileId });

            if (fileInfo == null)
                return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

            // Admin sadece kendi makalesinin dosyasını silebilir
            if (IsAdmin() && !IsSuperAdmin())
            {
                if ((int?)fileInfo.CreatedBy != GetUserId())
                    return StatusCode(403, ApiResponse.Fail("Bu dosyayı silme yetkiniz yok"));
            }

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, ((string)fileInfo.RelativePath).TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            await connection.ExecuteAsync(
                "UPDATE KnowledgeBaseFiles SET IsDeleted = 1 WHERE ID = @ID",
                new { ID = fileId });

            return Ok(ApiResponse.Ok("Dosya silindi"));
        }

        [HttpGet("files/{fileId:int}/download")]
        public async Task<IActionResult> DownloadFile(int fileId)
        {
            using var connection = _context.CreateConnection();
            var file = await connection.QueryFirstOrDefaultAsync<KnowledgeBaseFileDto>(
                "SELECT * FROM KnowledgeBaseFiles WHERE ID = @ID AND IsDeleted = 0",
                new { ID = fileId });

            if (file == null)
                return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, file.RelativePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
                return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

            byte[] bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, file.MimeType, file.OriginalFileName);
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Private Helpers

        private static async Task SaveProducts(
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction transaction,
            int knowledgeBaseId,
            List<short> productIds)
        {
            await connection.ExecuteAsync(
                "DELETE FROM KnowledgeBaseProducts WHERE KnowledgeBaseID = @ID",
                new { ID = knowledgeBaseId }, transaction);

            if (productIds.Any())
            {
                var rows = productIds.Select(pid => new
                {
                    KnowledgeBaseID = knowledgeBaseId,
                    LogoProductID = pid
                });
                await connection.ExecuteAsync(
                    "INSERT INTO KnowledgeBaseProducts (KnowledgeBaseID, LogoProductID) VALUES (@KnowledgeBaseID, @LogoProductID)",
                    rows, transaction);
            }
        }

        #endregion
    }
}