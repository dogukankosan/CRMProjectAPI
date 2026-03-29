using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Services;
using CRMProjectAPI.Validations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace CRMProjectAPI.Controllers
{
    [ApiController]
    [Route("api/ticket")]
    [Authorize]
    public class TicketController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IMailService _mailService;
        private readonly ILogger<TicketController> _logger;

        public TicketController(
            DapperContext context,
            IWebHostEnvironment env,
            IMailService mailService,
            ILogger<TicketController> logger)
        {
            _context = context;
            _env = env;
            _mailService = mailService;
            _logger = logger;
        }
     
        // ── JWT yardımcıları ─────────────────────────────────────────────────
        private int GetUserId() =>
            int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : 0;

        private int GetCompanyId() =>
            int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : 0;

        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
        private bool IsAdmin() => User.IsInRole("Admin");
        private bool IsUser() => User.IsInRole("User");

        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Kişisel bildirimler — üzerimdeki veya açtığım açık ticketlar
        /// </summary>
        [HttpGet("my-notifications")]
        public async Task<IActionResult> GetMyNotifications()
        {
            int userId = GetUserId();
            int companyId = GetCompanyId();
            using var connection = _context.CreateConnection();

            string sql;
            if (IsAdmin() || IsSuperAdmin())
            {
                // Admin/SuperAdmin: üzerime atanmış açık ticketlar
                sql = @"
    SELECT TOP 20
        t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
        t.OpenedDate, t.AssignedDate,
        c.CustomerName,
        DATEDIFF(HOUR, t.OpenedDate, GETDATE()) AS WaitingHours
    FROM Tickets t WITH (NOLOCK)
    INNER JOIN Customers c WITH (NOLOCK) ON t.CustomerID = c.ID
    WHERE t.IsDeleted = 0
      AND (
            t.AssignedToUserID = @UserID
            OR t.CreatedByUserID = @UserID
          )
      AND t.Status NOT IN (2, 3, 6)
    ORDER BY t.Priority DESC, t.OpenedDate ASC
";
            }
            else
            {
                // User: kendi açtığı açık ticketlar
                sql = @"
            SELECT TOP 20
        t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
        t.OpenedDate, t.AssignedDate,
        c.CustomerName,
        DATEDIFF(HOUR, t.OpenedDate, GETDATE()) AS WaitingHours
    FROM Tickets t WITH (NOLOCK)
            INNER JOIN Customers c WITH (NOLOCK) ON t.CustomerID = c.ID
            WHERE t.IsDeleted = 0
              AND t.CreatedByUserID = @UserID
              AND t.Status NOT IN (2, 3, 6)
            ORDER BY t.Priority DESC, t.OpenedDate ASC
        ";
            }

            var tickets = (await connection.QueryAsync(sql, new { UserID = userId })).ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                Count = tickets.Count,
                Tickets = tickets
            }));
        }

        /// <summary>
        /// Firma bildirimleri — firmanın açık ticketlarındaki son yorumlar ve dosyalar
        /// </summary>
        [HttpGet("company-notifications")]
        public async Task<IActionResult> GetCompanyNotifications()
        {
            int companyId = GetCompanyId();
            int userId = GetUserId();
            using var connection = _context.CreateConnection();

            string commentsSql;
            string filesSql;
            object commentsParam;
            object filesParam;

            if (IsAdmin() || IsSuperAdmin())
            {
                // Admin/SuperAdmin: üzerindeki ticketlara gelen yorumlar (kendi yazdıkları hariç)
                commentsSql = @"
            SELECT TOP 15
                tc.ID, tc.TicketID, tc.Comment, tc.CreatedDate,
                t.TicketNo, t.Title,
                ISNULL(u.FullName, u.Username) AS UserFullName,
                u.ISAdmin AS UserIsAdmin
            FROM TicketComments tc WITH (NOLOCK)
            INNER JOIN Tickets t WITH (NOLOCK) ON tc.TicketID = t.ID
            INNER JOIN Users   u WITH (NOLOCK) ON tc.UserID   = u.ID
            WHERE t.AssignedToUserID = @UserId
              AND t.IsDeleted = 0
              AND t.Status NOT IN (2, 3, 6)
              AND tc.UserID != @UserId
            ORDER BY tc.CreatedDate DESC
        ";
                filesSql = @"
            SELECT TOP 10
                tf.ID, tf.TicketID, tf.OriginalFileName, tf.FileType, tf.UploadedDate,
                t.TicketNo, t.Title,
                ISNULL(u.FullName, u.Username) AS UploadedByName
            FROM TicketFiles tf WITH (NOLOCK)
            INNER JOIN Tickets t WITH (NOLOCK) ON tf.TicketID        = t.ID
            INNER JOIN Users   u WITH (NOLOCK) ON tf.UploadedByUserID = u.ID
            WHERE t.AssignedToUserID = @UserId
              AND t.IsDeleted  = 0
              AND tf.IsDeleted = 0
              AND t.Status NOT IN (2, 3, 6)
              AND tf.UploadedByUserID != @UserId
            ORDER BY tf.UploadedDate DESC
        ";
                commentsParam = new { UserId = userId };
                filesParam = new { UserId = userId };
            }
            else
            {
                // User: kendi firmasının ticketlarındaki yorumlar ve dosyalar
                commentsSql = @"
            SELECT TOP 15
                tc.ID, tc.TicketID, tc.Comment, tc.CreatedDate,
                t.TicketNo, t.Title,
                ISNULL(u.FullName, u.Username) AS UserFullName,
                u.ISAdmin AS UserIsAdmin
            FROM TicketComments tc WITH (NOLOCK)
            INNER JOIN Tickets t WITH (NOLOCK) ON tc.TicketID = t.ID
            INNER JOIN Users   u WITH (NOLOCK) ON tc.UserID   = u.ID
            WHERE t.CustomerID = @CompanyId
              AND t.IsDeleted  = 0
              AND t.Status NOT IN (2, 3, 6)
            ORDER BY tc.CreatedDate DESC
        ";
                filesSql = @"
            SELECT TOP 10
                tf.ID, tf.TicketID, tf.OriginalFileName, tf.FileType, tf.UploadedDate,
                t.TicketNo, t.Title,
                ISNULL(u.FullName, u.Username) AS UploadedByName
            FROM TicketFiles tf WITH (NOLOCK)
            INNER JOIN Tickets t WITH (NOLOCK) ON tf.TicketID        = t.ID
            INNER JOIN Users   u WITH (NOLOCK) ON tf.UploadedByUserID = u.ID
            WHERE t.CustomerID = @CompanyId
              AND t.IsDeleted  = 0
              AND tf.IsDeleted = 0
              AND t.Status NOT IN (2, 3, 6)
            ORDER BY tf.UploadedDate DESC
        ";
                commentsParam = new { CompanyId = companyId };
                filesParam = new { CompanyId = companyId };
            }

            var comments = (await connection.QueryAsync(commentsSql, commentsParam)).ToList();
            var files = (await connection.QueryAsync(filesSql, filesParam)).ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                CommentCount = comments.Count,
                FileCount = files.Count,
                TotalCount = comments.Count + files.Count,
                Comments = comments,
                Files = files
            }));
        }
        #region Ticket CRUD

        /// <summary>
        /// Ticket listesi:
        ///   - Admin/SuperAdmin → tüm ticketlar
        ///   - User → sadece kendi firmasının ticketları
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List()
        {
            using var connection = _context.CreateConnection();

            string whereClause = IsUser() && !IsSuperAdmin() && !IsAdmin()
        ? "WHERE t.IsDeleted = 0 AND t.CustomerID = @CompanyId AND t.Status NOT IN (2, 3, 6)"
        : "WHERE t.IsDeleted = 0 AND t.Status NOT IN (2, 3, 6)";

            string sql = $@"
    SELECT
        t.ID, t.TicketNo,t.Description, t.CustomerID, t.LogoProductID,
        t.CreatedByUserID, t.AssignedToUserID,
        t.Title, t.Priority, t.Status,
        t.OpenedDate, t.AssignedDate, t.ResolvedDate,
        t.WorkingMinute,
        c.CustomerName, c.CustomerCode,
        c.Importance        AS CustomerImportance,
        lp.LogoProductName,
        ISNULL(cu.FullName, cu.Username) AS CreatedByName,
        ISNULL(au.FullName, au.Username) AS AssignedToName,
        cu.Picture          AS CreatedByPicture,
        au.Picture          AS AssignedToPicture,
        cu.PhoneNumber      AS CreatedByPhone,
        cu.EMailAddress     AS CreatedByEmail,
        t.AssignedDate      AS TakenInProgressDate
    FROM Tickets t WITH (NOLOCK)
    INNER JOIN Customers    c  WITH (NOLOCK) ON t.CustomerID       = c.ID
    LEFT  JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID    = lp.ID
    LEFT  JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID  = cu.ID
    LEFT  JOIN Users        au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
    {whereClause}
    ORDER BY
        CASE t.Status WHEN 0 THEN 0 WHEN 1 THEN 1 ELSE 2 END ASC,
        t.Priority DESC,
        t.OpenedDate DESC
";

            IEnumerable<TicketListDto> tickets = await connection.QueryAsync<TicketListDto>(
                sql, new { CompanyId = GetCompanyId() });

            return Ok(ApiResponse<IEnumerable<TicketListDto>>.Ok(tickets));
        }

        /// <summary>
        /// Ticket detay:
        ///   - Admin/SuperAdmin → herkese
        ///   - User → sadece kendi firmasının ticketı
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var connection = _context.CreateConnection();
            const string sql = @"
    SELECT
        t.*,
        c.CustomerName, c.CustomerCode,
        lp.LogoProductName,
t.AssignedDate AS TakenInProgressDate,
        ISNULL(cu.FullName, cu.Username) AS CreatedByName,
        ISNULL(au.FullName, au.Username) AS AssignedToName,
        cu.Picture AS CreatedByPicture,
        au.Picture AS AssignedToPicture
    FROM Tickets t WITH (NOLOCK)
    INNER JOIN Customers    c  WITH (NOLOCK) ON t.CustomerID       = c.ID
    LEFT  JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID    = lp.ID
    LEFT  JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID  = cu.ID
    LEFT  JOIN Users        au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
    WHERE t.ID = @ID AND t.IsDeleted = 0
";

            TicketDto? ticket = await connection.QueryFirstOrDefaultAsync<TicketDto>(sql, new { ID = id });
            if (ticket == null)
                return NotFound(ApiResponse.NotFound("Ticket bulunamadı"));

            // User kendi firmasının ticketını görebilir
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                if (ticket.CustomerID != GetCompanyId())
                    return Forbid();
            }

            // Dosyalar
            const string filesSql = @"
                SELECT
                    tf.*,
                    ISNULL(u.FullName, u.Username) AS UploadedByName
                FROM TicketFiles tf WITH (NOLOCK)
                LEFT JOIN Users u WITH (NOLOCK) ON tf.UploadedByUserID = u.ID
                WHERE tf.TicketID = @TicketID AND tf.IsDeleted = 0
                ORDER BY tf.UploadedDate ASC
            ";
            ticket.Files = (await connection.QueryAsync<TicketFileDto>(
                filesSql, new { TicketID = id })).ToList();

            // Yorumlar
            const string commentsSql = @"
                SELECT
                    tc.ID, tc.TicketID, tc.UserID, tc.Comment, tc.CreatedDate,
                    ISNULL(u.FullName, u.Username) AS UserFullName,
                    u.Picture AS UserPicture,
                    u.ISAdmin AS UserIsAdmin
                FROM TicketComments tc WITH (NOLOCK)
                INNER JOIN Users u WITH (NOLOCK) ON tc.UserID = u.ID
                WHERE tc.TicketID = @TicketID
                ORDER BY tc.CreatedDate ASC
            ";
            ticket.Comments = (await connection.QueryAsync<TicketCommentDto>(
                commentsSql, new { TicketID = id })).ToList();

            return Ok(ApiResponse<TicketDto>.Ok(ticket));
        }

        /// <summary>
        /// Ticket oluştur:
        ///   - User ve SuperAdmin oluşturabilir
        ///   - Admin oluşturamaz
        ///   Kontroller:
        ///     1. Kullanıcı aktif
        ///     2. Firma aktif
        ///     3. Sözleşme tarihi dolmamış
        ///     4. TicketCount > 0
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TicketCreateDto dto)
        {
       
       

            List<string> errors = TicketValidation.ValidateCreate(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();

            // User kendi firması için ticket açabilir
            if (IsUser())
            {
                if (dto.CustomerID != GetCompanyId())
                    return Forbid();
            }

            // 1. Kullanıcı aktif mi?
            bool userActive = await connection.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN Status = 1 THEN 1 ELSE 0 END FROM Users WITH (NOLOCK) WHERE ID = @ID",
                new { ID = dto.CreatedByUserID });
            if (!userActive)
                return BadRequest(ApiResponse.Fail("Kullanıcı hesabı aktif değil"));

            // 2. Firma aktif mi? 3. Sözleşme dolmamış mı? 4. TicketCount > 0 mu?
            var customer = await connection.QueryFirstOrDefaultAsync(
                @"SELECT Status, ContractEndDate, TicketCount 
                  FROM Customers WITH (NOLOCK) 
                  WHERE ID = @ID",
                new { ID = dto.CustomerID });

            if (customer == null)
                return NotFound(ApiResponse.NotFound("Firma bulunamadı"));

            if ((byte)customer.Status != 1)
                return BadRequest(ApiResponse.Fail("Firma aktif değil, ticket oluşturulamaz"));

            if (customer.ContractEndDate != null && (DateTime)customer.ContractEndDate < DateTime.Today)
                return BadRequest(ApiResponse.Fail("Sözleşme süresi dolmuş, ticket oluşturulamaz"));

            if ((int)customer.TicketCount <= 0)
                return BadRequest(ApiResponse.Fail("Ticket hakkınız kalmamış"));

            // Ticket No üret: TKT-2026-00001
            string year = DateTime.Now.Year.ToString();
            int lastNum = await connection.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(CAST(SUBSTRING(TicketNo, 10, 5) AS INT)), 0) FROM Tickets WITH (NOLOCK) WHERE TicketNo LIKE @Pattern",
                new { Pattern = $"TKT-{year}-%" });
            string ticketNo = $"TKT-{year}-{(lastNum + 1):D5}";

            const string sql = @"
    INSERT INTO Tickets (
        TicketNo, CustomerID, LogoProductID, CreatedByUserID,
        Title, Description, Priority, Status,
        OpenedDate, WorkingMinute, IsDeleted
    ) VALUES (
        @TicketNo, @CustomerID, @LogoProductID, @CreatedByUserID,
        @Title, @Description, @Priority, 0,
        @OpenedDate, 0, 0
    );
    SELECT CAST(SCOPE_IDENTITY() AS INT);
";

            int newId = await connection.QuerySingleAsync<int>(sql, new
            {
                TicketNo = ticketNo,
                dto.CustomerID,
                dto.LogoProductID,
                dto.CreatedByUserID,
                dto.Title,
                dto.Description,
                dto.Priority,
                // Admin/SuperAdmin tarih seçebilir, User için DateTime.Now
                OpenedDate = (IsAdmin() || IsSuperAdmin()) && dto.OpenedDate.HasValue
                             ? dto.OpenedDate.Value
                             : DateTime.Now
            });

            // TicketCount'u 1 azalt
            await connection.ExecuteAsync(
                "UPDATE Customers SET TicketCount = TicketCount - 1 WHERE ID = @ID",
                new { ID = dto.CustomerID });

            return Ok(ApiResponse<int>.Ok(newId, $"Ticket oluşturuldu ({ticketNo})"));
        }

        /// <summary>
        /// Ticket durum güncelleme — sadece Admin ve SuperAdmin
        /// Ticket oluşturulduktan sonra değiştirilemez kuralına göre
        /// sadece status, solutionNote ve workingMinute güncellenir.
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TicketStatusUpdateDto dto)
        {
            // Status 6 (İptal) sadece SuperAdmin yapabilir
            if (dto.Status == 6 && !IsSuperAdmin())
                return Forbid();

            List<string> errors = TicketValidation.ValidateStatusUpdate(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();

            // Mevcut ticket bilgilerini çek
            var ticket = await connection.QueryFirstOrDefaultAsync(
                "SELECT Status, CustomerID, AssignedToUserID FROM Tickets WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });

            if (ticket == null)
                return NotFound(ApiResponse.NotFound("Ticket bulunamadı"));

            // Admin kısıtlaması — SuperAdmin her şeyi yapabilir
            if (IsAdmin() && !IsSuperAdmin())
            {
                int? assignedToUserID = (int?)ticket.AssignedToUserID;

                // Başkasına atanmış ticketa müdahale edemez
                if (assignedToUserID.HasValue && assignedToUserID.Value != GetUserId())
                    return BadRequest(ApiResponse.Fail("Bu ticket başka bir kullanıcıya atanmış, müdahale edemezsiniz"));

                // Atanmamış ticketa sadece İşlemde yapabilir
                if (!assignedToUserID.HasValue && dto.Status != 1)
                    return BadRequest(ApiResponse.Fail("Önce ticketı üzerinize alın"));
            }

            // Zaten kapalıysa güncelleme yapılamaz
            byte currentStatus = (byte)ticket.Status;
            if (currentStatus == 2 || currentStatus == 3 || currentStatus == 6)
                return BadRequest(ApiResponse.Fail("Kapalı veya iptal edilmiş ticket güncellenemez"));

            // İşleme almadan kapatma engeli
            if (currentStatus == 0 && dto.Status != 0 && dto.Status != 1)
                return BadRequest(ApiResponse.Fail("Ticket kapatılmadan önce 'İşlemde' statüsüne alınmalıdır"));

            const string sql = @"
        UPDATE Tickets SET
            Status        = @Status,
            SolutionNote  = CASE WHEN @SolutionNote IS NOT NULL
                                THEN @SolutionNote ELSE SolutionNote END,
            CancelReason  = CASE WHEN @Status = 6 AND @CancelReason IS NOT NULL
                                THEN @CancelReason ELSE CancelReason END,
        WorkingMinute = @WorkingMinute,
            ResolvedDate  = CASE WHEN @Status IN (2,3,6) AND ResolvedDate IS NULL
                                THEN GETDATE() ELSE ResolvedDate END,
            ClosedDate    = CASE WHEN @Status IN (2,3,6) AND ClosedDate IS NULL
                                THEN GETDATE() ELSE ClosedDate END
        WHERE ID = @ID AND IsDeleted = 0
    ";

            await connection.ExecuteAsync(sql, new
            {
                dto.Status,
                dto.SolutionNote,
                dto.CancelReason,
                dto.WorkingMinute,
                ID = id
            });

            // Status 2, 3 veya 6 → mail gönder
            if (dto.Status == 2 || dto.Status == 3 || dto.Status == 6)
            {
                try
                {
                    int customerId = (int)ticket.CustomerID;
                    await SendTicketClosedMailAsync(connection, id, customerId, dto.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ticket kapanma maili gönderilemedi. TicketID: {ID}", id);
                }
            }

            string statusText = dto.Status switch
            {
                0 => "Beklemede",
                1 => "İşlemde",
                2 => "Başarılı Kapandı",
                3 => "Çözülemedi",
                4 => "Müşteri Bize Dönecek",
                5 => "Müşteriye Geri Döneceğiz",
                6 => "İptal Edildi",
                _ => "Güncellendi"
            };

            return Ok(ApiResponse<byte>.Ok(dto.Status, $"Durum: {statusText}"));
        }

        /// <summary>
        /// Ticket devir — Admin ve SuperAdmin
        /// Admin → Admin veya SuperAdmin'e devredebilir
        /// SuperAdmin → Admin veya SuperAdmin'e devredebilir
        /// </summary>
        [HttpPatch("{id:int}/assign")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Assign(int id, [FromBody] TicketAssignDto dto)
        {
            List<string> errors = TicketValidation.ValidateAssign(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();

            // Ticket var mı ve kapalı mı?
            var ticket = await connection.QueryFirstOrDefaultAsync(
                "SELECT Status, AssignedToUserID FROM Tickets WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });

            if (ticket == null)
                return NotFound(ApiResponse.NotFound("Ticket bulunamadı"));

            byte currentStatus = (byte)ticket.Status;
            if (currentStatus == 2 || currentStatus == 3 || currentStatus == 6)
                return BadRequest(ApiResponse.Fail("Kapalı veya iptal edilmiş ticket devredilemez"));

            // Admin kısıtlaması — SuperAdmin her şeyi yapabilir
            if (IsAdmin() && !IsSuperAdmin())
            {
                int? assignedToUserID = (int?)ticket.AssignedToUserID;

                // Başkasına atanmış ticketı devredemez
                if (assignedToUserID.HasValue && assignedToUserID.Value != GetUserId())
                    return BadRequest(ApiResponse.Fail("Bu ticket başka birine atanmış, devredemezsiniz"));
            }

            // Atanacak kullanıcı Admin veya SuperAdmin olmalı
            byte targetIsAdmin = await connection.ExecuteScalarAsync<byte>(
                "SELECT ISAdmin FROM Users WITH (NOLOCK) WHERE ID = @ID AND Status = 1",
                new { ID = dto.AssignedToUserID });

            if (targetIsAdmin == 0)
                return BadRequest(ApiResponse.Fail("Ticket sadece Admin veya SuperAdmin'e devredilebilir"));

            const string sql = @"
 UPDATE Tickets SET
    AssignedToUserID = @UserID,
    AssignedDate     = GETDATE(),
    Status           = 1
WHERE ID = @ID AND IsDeleted = 0
    ";

            await connection.ExecuteAsync(sql, new { UserID = dto.AssignedToUserID, ID = id });

            return Ok(ApiResponse.Ok("Ticket devredildi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ticket Files

        /// <summary>
        /// Dosya yükle:
        ///   - User → kendi firmasının ticketına
        ///   - Admin/SuperAdmin → herhangi bir ticketa
        ///   - Kapalı ticketa dosya eklenemez
        /// </summary>
        [HttpPost("{id:int}/files")]
        public async Task<IActionResult> UploadFile(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            using var connection = _context.CreateConnection();

            // Ticket kontrol
            var ticket = await connection.QueryFirstOrDefaultAsync(
                "SELECT CustomerID, Status FROM Tickets WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });

            if (ticket == null)
                return NotFound(ApiResponse.NotFound("Ticket bulunamadı"));

            // User kendi firmasına dosya ekleyebilir
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                if ((int)ticket.CustomerID != GetCompanyId())
                    return Forbid();
            }

            // Kapalı ticketa dosya eklenemez
            byte status = (byte)ticket.Status;
            if (status == 2 || status == 3 || status == 6)
                return BadRequest(ApiResponse.Fail("Kapalı ticketa dosya eklenemez"));

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            HashSet<string> allowed = new()
                { ".pdf", ".xls", ".xlsx", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".txt" };

            if (!allowed.Contains(extension))
                return BadRequest(ApiResponse.Fail("Desteklenmeyen dosya formatı"));

            if (file.Length > 20 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya boyutu 20MB'dan büyük olamaz"));

            string fileType = extension switch
            {
                ".pdf" => "PDF",
                ".xls" or ".xlsx" => "Excel",
                ".doc" or ".docx" => "Word",
                ".jpg" or ".jpeg" or ".png"
                    or ".gif" or ".bmp" => "Image",
                ".txt" => "Text",
                _ => "Other"
            };

            byte[] fileBytes;
            string fileHash;
            using (MemoryStream ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
                fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
            }

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string storedFileName = $"{Guid.NewGuid():N}{extension}";
            string folderPath = Path.Combine(webRoot, "uploads", "tickets", id.ToString());
            Directory.CreateDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, storedFileName);

            try { await System.IO.File.WriteAllBytesAsync(fullPath, fileBytes); }
            catch
            {
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                throw;
            }

            const string sql = @"
                INSERT INTO TicketFiles (
                    TicketID, OriginalFileName, StoredFileName, RelativePath,
                    FileExtension, MimeType, FileSizeBytes, FileHash, FileType,
                    UploadedByUserID, UploadedDate, IsDeleted
                ) VALUES (
                    @TicketID, @OriginalFileName, @StoredFileName, @RelativePath,
                    @FileExtension, @MimeType, @FileSizeBytes, @FileHash, @FileType,
                    @UploadedByUserID, GETDATE(), 0
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";

            int newId = await connection.QuerySingleAsync<int>(sql, new
            {
                TicketID = id,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                RelativePath = $"/uploads/tickets/{id}/{storedFileName}",
                FileExtension = extension,
                MimeType = file.ContentType,
                FileSizeBytes = file.Length,
                FileHash = fileHash,
                FileType = fileType,
                UploadedByUserID = GetUserId()
            });

            return Ok(ApiResponse<int>.Ok(newId, "Dosya yüklendi"));
        }

        /// <summary>
        /// Dosya indir — ticket sahibi firma veya Admin/SuperAdmin
        /// </summary>
        [HttpGet("files/{fileId:int}/download")]
        public async Task<IActionResult> DownloadFile(int fileId)
        {
            using var connection = _context.CreateConnection();

            const string sql = @"
                SELECT tf.*, t.CustomerID
                FROM TicketFiles tf WITH (NOLOCK)
                INNER JOIN Tickets t WITH (NOLOCK) ON tf.TicketID = t.ID
                WHERE tf.ID = @ID AND tf.IsDeleted = 0
            ";
            var file = await connection.QueryFirstOrDefaultAsync(sql, new { ID = fileId });

            if (file == null)
                return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

            // User kendi firmasının dosyasını indirebilir
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                if ((int)file.CustomerID != GetCompanyId())
                    return Forbid();
            }

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, ((string)file.RelativePath).TrimStart('/'));

            if (!System.IO.File.Exists(fullPath))
                return NotFound(ApiResponse.NotFound("Dosya fiziksel olarak bulunamadı"));

            byte[] bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var originalName = (string)file.OriginalFileName;
            var encodedName = Uri.EscapeDataString(originalName);

            Response.Headers.Append("Content-Disposition",
                $"attachment; filename=\"{encodedName}\"; filename*=UTF-8''{encodedName}");

            return File(bytes, (string)file.MimeType, originalName);
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ticket Comments

        /// <summary>
        /// Yorum ekle:
        ///   - User → kendi firmasının ticketına yorum yapabilir
        ///   - Admin/SuperAdmin → her ticketa yorum yapabilir
        ///   - Kapalı ticketa yorum eklenemez (Status 2 veya 3)
        /// </summary>
        [HttpPost("{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] TicketCommentCreateDto dto)
        {
            dto.TicketID = id;
            dto.UserID = GetUserId();

            List<string> errors = TicketValidation.ValidateComment(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();

            // Ticket kontrol
            var ticket = await connection.QueryFirstOrDefaultAsync(
                "SELECT CustomerID, Status FROM Tickets WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });

            if (ticket == null)
                return NotFound(ApiResponse.NotFound("Ticket bulunamadı"));

            // Kapalı ticketa yorum eklenemez
            byte status = (byte)ticket.Status;
            if (status == 2 || status == 3 || status == 6)
                return BadRequest(ApiResponse.Fail("Kapalı ticketa yorum eklenemez"));

            // User kendi firmasının ticketına yorum yapabilir
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                if ((int)ticket.CustomerID != GetCompanyId())
                    return Forbid();
            }

            const string sql = @"
                INSERT INTO TicketComments (TicketID, UserID, Comment, CreatedDate)
                VALUES (@TicketID, @UserID, @Comment, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";

            int newId = await connection.QuerySingleAsync<int>(sql, new
            {
                dto.TicketID,
                dto.UserID,
                dto.Comment
            });

            return Ok(ApiResponse<int>.Ok(newId, "Yorum eklendi"));
        }

        /// <summary>
        /// Yorumları listele — ticket sahibi firma veya Admin/SuperAdmin
        /// </summary>
        [HttpGet("{id:int}/comments")]
        public async Task<IActionResult> GetComments(int id)
        {
            using var connection = _context.CreateConnection();

            // Ticket kontrol
            var ticket = await connection.QueryFirstOrDefaultAsync(
                "SELECT CustomerID FROM Tickets WITH (NOLOCK) WHERE ID = @ID AND IsDeleted = 0",
                new { ID = id });

            if (ticket == null)
                return NotFound(ApiResponse.NotFound("Ticket bulunamadı"));

            // User kendi firmasının yorumlarını görebilir
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                if ((int)ticket.CustomerID != GetCompanyId())
                    return Forbid();
            }

            const string sql = @"
                SELECT
                    tc.ID, tc.TicketID, tc.UserID, tc.Comment, tc.CreatedDate,
                    ISNULL(u.FullName, u.Username) AS UserFullName,
                    u.Picture AS UserPicture,
                    u.ISAdmin AS UserIsAdmin
                FROM TicketComments tc WITH (NOLOCK)
                INNER JOIN Users u WITH (NOLOCK) ON tc.UserID = u.ID
                WHERE tc.TicketID = @TicketID
                ORDER BY tc.CreatedDate ASC
            ";

            IEnumerable<TicketCommentDto> comments = await connection.QueryAsync<TicketCommentDto>(
                sql, new { TicketID = id });

            return Ok(ApiResponse<IEnumerable<TicketCommentDto>>.Ok(comments));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Dashboard & Stats

        /// <summary>
        /// SuperAdmin dashboard — genel istatistikler
        /// </summary>
        [HttpGet("superadmin-dashboard")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetSuperAdminDashboard()
        {
            using var connection = _context.CreateConnection();

            const string statsSql = @"
        SELECT
            COUNT(*)                                                               AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END)                 AS OpenCount,
            SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END)                          AS Waiting,
            SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END)                          AS InProgress,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                          AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)                          AS Failed,
            SUM(CASE WHEN Status = 6 THEN 1 ELSE 0 END)                          AS Cancelled,
            SUM(CASE WHEN Status IN (4,5) THEN 1 ELSE 0 END)                     AS WaitingCustomer,
            SUM(CASE WHEN Priority = 4 AND Status NOT IN (2,3,6) THEN 1 ELSE 0 END) AS CriticalOpen,
            SUM(CASE WHEN CAST(OpenedDate AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS TodayOpened,
            SUM(CASE WHEN CAST(ClosedDate AS DATE) = CAST(GETDATE() AS DATE) AND Status IN (2,3) THEN 1 ELSE 0 END) AS TodayResolved,
            SUM(CASE WHEN MONTH(OpenedDate) = MONTH(GETDATE()) AND YEAR(OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS ThisMonthOpened,
            SUM(CASE WHEN MONTH(ClosedDate) = MONTH(GETDATE()) AND YEAR(ClosedDate) = YEAR(GETDATE()) AND Status IN (2,3) THEN 1 ELSE 0 END) AS ThisMonthResolved,
            ISNULL(AVG(CAST(WorkingMinute AS FLOAT)), 0)                          AS AvgWorkingMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
    ";
            dynamic? stats = await connection.QueryFirstOrDefaultAsync(statsSql);

            // Sözleşmesi 10 gün kalan müşteriler
            const string contractSql = @"
        SELECT
            ID, CustomerCode, CustomerName, ShortName,
            ContractEndDate,
            DATEDIFF(DAY, GETDATE(), ContractEndDate) AS DaysLeft
        FROM Customers WITH (NOLOCK)
        WHERE ContractEndDate IS NOT NULL
          AND ContractEndDate >= CAST(GETDATE() AS DATE)
          AND ContractEndDate <= DATEADD(DAY, 10, CAST(GETDATE() AS DATE))
          AND Status = 1
        ORDER BY ContractEndDate ASC
    ";
            IEnumerable<dynamic> expiringContracts = await connection.QueryAsync(contractSql);

            // Ticket hakkı 10'dan az müşteriler
            const string lowTicketSql = @"
        SELECT COUNT(*) AS LowTicketCount
        FROM Customers WITH (NOLOCK)
        WHERE TicketCount < 10 AND Status = 1
    ";
            int lowTicketCount = await connection.ExecuteScalarAsync<int>(lowTicketSql);

            // Son 7 günlük ticket trendi
            const string dailySql = @"
        SELECT
            CAST(OpenedDate AS DATE) AS Date,
            COUNT(*)                 AS Count
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND OpenedDate >= DATEADD(DAY, -6, CAST(GETDATE() AS DATE))
        GROUP BY CAST(OpenedDate AS DATE)
        ORDER BY CAST(OpenedDate AS DATE) ASC
    ";
            IEnumerable<dynamic> dailyCounts = await connection.QueryAsync(dailySql);

            // Durum dağılımı
            const string statusDistSql = @"
        SELECT Status, COUNT(*) AS Count
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
        GROUP BY Status
    ";
            IEnumerable<dynamic> statusDistribution = await connection.QueryAsync(statusDistSql);

            // Acil ticketlar
            const string urgentSql = @"
        SELECT TOP 10
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.WorkingMinute,
            c.CustomerName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName,
            ISNULL(au.FullName, au.Username) AS AssignedToName
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers c  WITH (NOLOCK) ON t.CustomerID       = c.ID
        LEFT  JOIN Users     cu WITH (NOLOCK) ON t.CreatedByUserID  = cu.ID
        LEFT  JOIN Users     au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
        WHERE t.IsDeleted = 0
          AND t.Priority IN (3, 4)
          AND t.Status NOT IN (2, 3, 6)
        ORDER BY t.Priority DESC, t.OpenedDate ASC
    ";
            IEnumerable<TicketListDto> urgentTickets = await connection.QueryAsync<TicketListDto>(urgentSql);

            return Ok(ApiResponse<object>.Ok(new
            {
                Stats = stats,
                LowTicketCount = lowTicketCount,
                ExpiringContracts = expiringContracts,
                DailyCounts = dailyCounts,
                StatusDistribution = statusDistribution,
                UrgentTickets = urgentTickets
            }));
        }

        /// <summary>
        /// Admin dashboard — kişisel performans istatistikleri
        /// </summary>
        [HttpGet("admin-dashboard")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetAdminDashboard()
        {
            int userId = GetUserId();
            using var connection = _context.CreateConnection();

            const string statsSql = @"
        SELECT
            COUNT(*)                                                                   AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END)                     AS OpenCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                              AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)                              AS Failed,
            SUM(CASE WHEN CAST(ClosedDate AS DATE) = CAST(GETDATE() AS DATE)
                AND Status IN (2,3) THEN 1 ELSE 0 END)                               AS TodayResolved,
            SUM(CASE WHEN DATEPART(WEEK, ClosedDate) = DATEPART(WEEK, GETDATE())
                AND YEAR(ClosedDate) = YEAR(GETDATE())
                AND Status IN (2,3) THEN 1 ELSE 0 END)                               AS ThisWeekResolved,
            SUM(CASE WHEN MONTH(ClosedDate) = MONTH(GETDATE())
                AND YEAR(ClosedDate) = YEAR(GETDATE())
                AND Status IN (2,3) THEN 1 ELSE 0 END)                               AS ThisMonthResolved,
            ISNULL(AVG(CAST(WorkingMinute AS FLOAT)), 0)                              AS AvgWorkingMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND AssignedToUserID = @UserId
    ";
            dynamic? stats = await connection.QueryFirstOrDefaultAsync(statsSql, new { UserId = userId });

            // Üzerindeki açık ticketlar
            const string myOpenSql = @"
        SELECT TOP 20
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.WorkingMinute,
            c.CustomerName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers c  WITH (NOLOCK) ON t.CustomerID      = c.ID
        LEFT  JOIN Users     cu WITH (NOLOCK) ON t.CreatedByUserID = cu.ID
        WHERE t.IsDeleted = 0
          AND t.AssignedToUserID = @UserId
          AND t.Status NOT IN (2, 3, 6)
        ORDER BY t.Priority DESC, t.OpenedDate ASC
    ";
            IEnumerable<TicketListDto> myOpenTickets =
                await connection.QueryAsync<TicketListDto>(myOpenSql, new { UserId = userId });

            // Son çözülen ticketlar
            const string recentResolvedSql = @"
        SELECT TOP 10
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.ClosedDate, t.WorkingMinute,
            c.CustomerName
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers c WITH (NOLOCK) ON t.CustomerID = c.ID
        WHERE t.IsDeleted = 0
          AND t.AssignedToUserID = @UserId
          AND t.Status IN (2, 3)
        ORDER BY t.ClosedDate DESC
    ";
            IEnumerable<TicketListDto> recentResolved =
                await connection.QueryAsync<TicketListDto>(recentResolvedSql, new { UserId = userId });

            return Ok(ApiResponse<object>.Ok(new
            {
                Stats = stats,
                MyOpenTickets = myOpenTickets,
                RecentResolved = recentResolved
            }));
        }

        /// <summary>
        /// User dashboard — kendi firmasının istatistikleri + kişisel
        /// </summary>
        [HttpGet("user-dashboard")]
        public async Task<IActionResult> GetUserDashboard()
        {
            int userId = GetUserId();
            int companyId = GetCompanyId();

            if (userId == 0 || companyId == 0)
                return Unauthorized(ApiResponse.Fail("Kullanıcı bilgisi alınamadı"));

            using var connection = _context.CreateConnection();

            // Firma istatistikleri
            const string companyStatsSql = @"
        SELECT
            COUNT(*)                                                      AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END)        AS OpenCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                 AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)                 AS Failed,
            SUM(CASE WHEN MONTH(OpenedDate) = MONTH(GETDATE())
                AND YEAR(OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS ThisMonthOpened,
            SUM(CASE WHEN MONTH(ClosedDate) = MONTH(GETDATE())
                AND YEAR(ClosedDate) = YEAR(GETDATE())
                AND Status IN (2,3) THEN 1 ELSE 0 END)                  AS ThisMonthResolved,
            ISNULL(AVG(CAST(WorkingMinute AS FLOAT)), 0)                 AS AvgWorkingMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0 AND CustomerID = @CompanyId
    ";
            dynamic? companyStats = await connection.QueryFirstOrDefaultAsync(
                companyStatsSql, new { CompanyId = companyId });

            // Kişisel istatistikler
            const string myStatsSql = @"
        SELECT
            COUNT(*)                                                      AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END)        AS OpenCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                 AS Resolved,
            SUM(CASE WHEN MONTH(OpenedDate) = MONTH(GETDATE())
                AND YEAR(OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS ThisMonthOpened
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND CreatedByUserID = @UserId
    ";
            dynamic? myStats = await connection.QueryFirstOrDefaultAsync(
                myStatsSql, new { UserId = userId });

            // Benim açık ticketlarım
            const string myOpenSql = @"
        SELECT TOP 10
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.WorkingMinute,
            lp.LogoProductName,
            ISNULL(au.FullName, au.Username) AS AssignedToName
        FROM Tickets t WITH (NOLOCK)
        LEFT JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID    = lp.ID
        LEFT JOIN Users        au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
        WHERE t.IsDeleted = 0
          AND t.CreatedByUserID = @UserId
          AND t.Status NOT IN (2, 3, 6)
        ORDER BY t.Priority DESC, t.OpenedDate ASC
    ";
            IEnumerable<TicketListDto> myOpenTickets =
                await connection.QueryAsync<TicketListDto>(myOpenSql, new { UserId = userId });

            // Firmanın tüm açık ticketları
            const string companyOpenSql = @"
        SELECT TOP 20
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.WorkingMinute,
            lp.LogoProductName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName,
            ISNULL(au.FullName, au.Username) AS AssignedToName
        FROM Tickets t WITH (NOLOCK)
        LEFT JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID    = lp.ID
        LEFT JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID  = cu.ID
        LEFT JOIN Users        au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
        WHERE t.IsDeleted = 0
          AND t.CustomerID = @CompanyId
          AND t.Status NOT IN (2, 3, 6)
        ORDER BY t.Priority DESC, t.OpenedDate ASC
    ";
            IEnumerable<TicketListDto> companyOpenTickets =
                await connection.QueryAsync<TicketListDto>(companyOpenSql, new { CompanyId = companyId });

            return Ok(ApiResponse<object>.Ok(new
            {
                CompanyStats = companyStats,
                MyStats = myStats,
                MyOpenTickets = myOpenTickets,
                CompanyOpenTickets = companyOpenTickets
            }));
        }

        #endregion
        /// <summary>
        /// SuperAdmin raporlama dashboard — detaylı istatistikler (fixed)
        /// </summary>
        [HttpGet("superadmin-report")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetSuperAdminReport()
        {
            using var connection = _context.CreateConnection();

            // ── 1. GENEL İSTATİSTİKLER ──────────────────────────────────────────
            const string generalStatsSql = @"
        SELECT
            COUNT(*)                                                                          AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END)                            AS ActiveCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                                     AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)                                     AS Failed,
            SUM(CASE WHEN Status = 6 THEN 1 ELSE 0 END)                                     AS Cancelled,

            -- Bugün
            SUM(CASE WHEN CAST(OpenedDate AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END)                          AS TodayOpened,
            SUM(CASE WHEN CAST(ClosedDate AS DATE) = CAST(GETDATE() AS DATE) AND Status IN (2,3) THEN 1 ELSE 0 END)      AS TodayResolved,

            -- Bu hafta
            SUM(CASE WHEN OpenedDate >= DATEADD(DAY, -(DATEPART(WEEKDAY, GETDATE())-2), CAST(GETDATE() AS DATE)) THEN 1 ELSE 0 END)                         AS ThisWeekOpened,
            SUM(CASE WHEN ClosedDate >= DATEADD(DAY, -(DATEPART(WEEKDAY, GETDATE())-2), CAST(GETDATE() AS DATE)) AND Status IN (2,3) THEN 1 ELSE 0 END)     AS ThisWeekResolved,

            -- Bu ay
            SUM(CASE WHEN MONTH(OpenedDate) = MONTH(GETDATE()) AND YEAR(OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END)                                    AS ThisMonthOpened,
            SUM(CASE WHEN MONTH(ClosedDate) = MONTH(GETDATE()) AND YEAR(ClosedDate) = YEAR(GETDATE()) AND Status IN (2,3) THEN 1 ELSE 0 END)                AS ThisMonthResolved,

            -- Bu yıl
            SUM(CASE WHEN YEAR(OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END)             AS ThisYearOpened,
            SUM(CASE WHEN YEAR(ClosedDate) = YEAR(GETDATE()) AND Status IN (2,3) THEN 1 ELSE 0 END) AS ThisYearResolved,

            -- Ortalama çözüm süresi
            ISNULL(AVG(CASE WHEN Status IN (2,3) THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgWorkingMinute,
            ISNULL(AVG(CASE WHEN Status IN (2,3) AND MONTH(ClosedDate) = MONTH(GETDATE()) AND YEAR(ClosedDate) = YEAR(GETDATE())
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0) AS ThisMonthAvgMinute,

            -- Başarı oranı
            CASE WHEN SUM(CASE WHEN Status IN (2,3) THEN 1 ELSE 0 END) = 0 THEN 0
                 ELSE CAST(SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS FLOAT) /
                      SUM(CASE WHEN Status IN (2,3) THEN 1 ELSE 0 END) * 100
            END AS SuccessRate,

            -- Kritik açık
            SUM(CASE WHEN Priority = 4 AND Status NOT IN (2,3,6) THEN 1 ELSE 0 END) AS CriticalOpen,
            SUM(CASE WHEN Priority = 3 AND Status NOT IN (2,3,6) THEN 1 ELSE 0 END) AS HighOpen,

            -- Atanmamış
            SUM(CASE WHEN AssignedToUserID IS NULL AND Status NOT IN (2,3,6) THEN 1 ELSE 0 END) AS UnassignedOpen
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
    ";
            dynamic? generalStats = await connection.QueryFirstOrDefaultAsync(generalStatsSql);

            // ── 2. ADMİN / SUPERADMIN PERFORMANSI ───────────────────────────────
            const string adminPerfSql = @"
        SELECT
            u.ID                                            AS UserID,
            ISNULL(u.FullName, u.Username)                 AS UserName,
            u.Picture                                       AS UserPicture,
            u.ISAdmin                                       AS IsAdmin,

            -- Toplam
            COUNT(t.ID)                                     AS TotalAssigned,
            SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END)  AS TotalResolved,
            SUM(CASE WHEN t.Status = 3 THEN 1 ELSE 0 END)  AS TotalFailed,
            SUM(CASE WHEN t.Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS ActiveCount,

            -- Bugün
            SUM(CASE WHEN CAST(t.ClosedDate AS DATE) = CAST(GETDATE() AS DATE)
                      AND t.Status IN (2,3) THEN 1 ELSE 0 END) AS TodayResolved,

            -- Bu hafta
            SUM(CASE WHEN t.ClosedDate >= DATEADD(DAY, -(DATEPART(WEEKDAY, GETDATE())-2), CAST(GETDATE() AS DATE))
                      AND t.Status IN (2,3) THEN 1 ELSE 0 END) AS ThisWeekResolved,

            -- Bu ay
            SUM(CASE WHEN MONTH(t.ClosedDate) = MONTH(GETDATE()) AND YEAR(t.ClosedDate) = YEAR(GETDATE())
                      AND t.Status IN (2,3) THEN 1 ELSE 0 END) AS ThisMonthResolved,

            -- Bu yıl
            SUM(CASE WHEN YEAR(t.ClosedDate) = YEAR(GETDATE())
                      AND t.Status IN (2,3) THEN 1 ELSE 0 END) AS ThisYearResolved,

            -- Ortalama çözüm süresi
            ISNULL(AVG(CASE WHEN t.Status IN (2,3)
                            THEN CAST(t.WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgWorkingMinute,

            -- Başarı oranı
            CASE WHEN SUM(CASE WHEN t.Status IN (2,3) THEN 1 ELSE 0 END) = 0 THEN 0
                 ELSE CAST(SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END) AS FLOAT) /
                      SUM(CASE WHEN t.Status IN (2,3) THEN 1 ELSE 0 END) * 100
            END AS SuccessRate

        FROM Users u WITH (NOLOCK)
        INNER JOIN Tickets t WITH (NOLOCK) ON t.AssignedToUserID = u.ID AND t.IsDeleted = 0
        WHERE u.ISAdmin >= 1
        GROUP BY u.ID, u.FullName, u.Username, u.Picture, u.ISAdmin
        ORDER BY TotalResolved DESC
    ";
            IEnumerable<dynamic> adminPerformance = await connection.QueryAsync(adminPerfSql);

            // ── 3. FİRMA BAZLI İSTATİSTİKLER (Top 15) ───────────────────────────
            const string customerStatsSql = @"
        SELECT TOP 15
            c.ID                                            AS CustomerID,
            c.CustomerName,
            c.CustomerCode,
            c.Importance,
            c.TicketCount                                   AS RemainingTickets,

            COUNT(t.ID)                                     AS TotalTickets,
            SUM(CASE WHEN t.Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS ActiveTickets,
            SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END)  AS ResolvedTickets,
            SUM(CASE WHEN t.Status = 3 THEN 1 ELSE 0 END)  AS FailedTickets,

            SUM(CASE WHEN MONTH(t.OpenedDate) = MONTH(GETDATE())
                      AND YEAR(t.OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS ThisMonthTickets,

            ISNULL(AVG(CASE WHEN t.Status IN (2,3)
                            THEN CAST(t.WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgWorkingMinute,

            CASE WHEN SUM(CASE WHEN t.Status IN (2,3) THEN 1 ELSE 0 END) = 0 THEN 0
                 ELSE CAST(SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END) AS FLOAT) /
                      SUM(CASE WHEN t.Status IN (2,3) THEN 1 ELSE 0 END) * 100
            END AS SuccessRate
        FROM Customers c WITH (NOLOCK)
        LEFT JOIN Tickets t WITH (NOLOCK) ON t.CustomerID = c.ID AND t.IsDeleted = 0
        GROUP BY c.ID, c.CustomerName, c.CustomerCode, c.Importance, c.TicketCount
        ORDER BY TotalTickets DESC
    ";
            IEnumerable<dynamic> customerStats = await connection.QueryAsync(customerStatsSql);

            // ── 4. EN ÇOK TİCKET AÇAN KULLANICILAR (Top 10) ────────────────────
            const string topUsersSql = @"
        SELECT TOP 10
            u.ID                                            AS UserID,
            ISNULL(u.FullName, u.Username)                 AS UserName,
            u.Picture                                       AS UserPicture,
            c.CustomerName,

            COUNT(t.ID)                                     AS TotalTickets,
            SUM(CASE WHEN MONTH(t.OpenedDate) = MONTH(GETDATE())
                      AND YEAR(t.OpenedDate) = YEAR(GETDATE()) THEN 1 ELSE 0 END) AS ThisMonthTickets,
            SUM(CASE WHEN CAST(t.OpenedDate AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS TodayTickets
        FROM Users u WITH (NOLOCK)
        INNER JOIN Tickets t WITH (NOLOCK) ON t.CreatedByUserID = u.ID AND t.IsDeleted = 0
        INNER JOIN Customers c WITH (NOLOCK) ON u.CompanyID = c.ID
        WHERE u.ISAdmin = 0
        GROUP BY u.ID, u.FullName, u.Username, u.Picture, c.CustomerName
        ORDER BY TotalTickets DESC
    ";
            IEnumerable<dynamic> topUsers = await connection.QueryAsync(topUsersSql);

            // ── 5. GÜNLÜK TRENDİ (Son 30 gün) ───────────────────────────────────
            const string dailyTrendSql = @"
        SELECT
            CAST(OpenedDate AS DATE)                        AS Date,
            COUNT(*)                                        AS Opened,
            SUM(CASE WHEN Status IN (2,3) AND CAST(ClosedDate AS DATE) = CAST(OpenedDate AS DATE) THEN 1 ELSE 0 END) AS ClosedSameDay
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND OpenedDate >= DATEADD(DAY, -29, CAST(GETDATE() AS DATE))
        GROUP BY CAST(OpenedDate AS DATE)
        ORDER BY CAST(OpenedDate AS DATE) ASC
    ";
            IEnumerable<dynamic> dailyTrend = await connection.QueryAsync(dailyTrendSql);

            // ── 6. AYLIK TRENDİ (Son 12 ay) ─────────────────────────────────────
            const string monthlyTrendSql = @"
        SELECT
            YEAR(OpenedDate)                                AS Year,
            MONTH(OpenedDate)                               AS Month,
            COUNT(*)                                        AS Opened,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)    AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)    AS Failed,
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND OpenedDate >= DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
        GROUP BY YEAR(OpenedDate), MONTH(OpenedDate)
        ORDER BY YEAR(OpenedDate) ASC, MONTH(OpenedDate) ASC
    ";
            IEnumerable<dynamic> monthlyTrend = await connection.QueryAsync(monthlyTrendSql);

            // ── 7. YILLIK TRENDİ (Son 5 yıl) ────────────────────────────────────
            const string yearlyTrendSql = @"
        SELECT
            YEAR(OpenedDate)                                AS Year,
            COUNT(*)                                        AS Opened,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)    AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)    AS Failed,
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND YEAR(OpenedDate) >= YEAR(GETDATE()) - 4
        GROUP BY YEAR(OpenedDate)
        ORDER BY YEAR(OpenedDate) ASC
    ";
            IEnumerable<dynamic> yearlyTrend = await connection.QueryAsync(yearlyTrendSql);

            // ── 8. LOGO ÜRÜN BAZLI DAĞILIM ──────────────────────────────────────
            const string productDistSql = @"
        SELECT
            lp.ID                                           AS LogoProductID,
            lp.LogoProductName,
            COUNT(t.ID)                                     AS TotalTickets,
            SUM(CASE WHEN t.Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS ActiveTickets,
            SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END)  AS ResolvedTickets,
            ISNULL(AVG(CASE WHEN t.Status IN (2,3)
                            THEN CAST(t.WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgMinute
        FROM LogoProducts lp WITH (NOLOCK)
        LEFT JOIN Tickets t WITH (NOLOCK) ON t.LogoProductID = lp.ID AND t.IsDeleted = 0
        GROUP BY lp.ID, lp.LogoProductName
        ORDER BY TotalTickets DESC
    ";
            IEnumerable<dynamic> productDist = await connection.QueryAsync(productDistSql);

            // ── 9. ÖNCELİK BAZLI DAĞILIM ────────────────────────────────────────
            const string priorityDistSql = @"
        SELECT
            Priority,
            COUNT(*)                                        AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS ActiveCount,
            SUM(CASE WHEN Status IN (2,3) THEN 1 ELSE 0 END) AS Closed,
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
        GROUP BY Priority
        ORDER BY Priority DESC
    ";
            IEnumerable<dynamic> priorityDist = await connection.QueryAsync(priorityDistSql);

            // ── 10. DURUM DAĞILIMI ───────────────────────────────────────────────
            const string statusDistSql = @"
        SELECT
            Status,
            COUNT(*) AS Count
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
        GROUP BY Status
        ORDER BY Status ASC
    ";
            IEnumerable<dynamic> statusDist = await connection.QueryAsync(statusDistSql);

            return Ok(ApiResponse<object>.Ok(new
            {
                GeneralStats = generalStats,
                AdminPerformance = adminPerformance,
                CustomerStats = customerStats,
                TopUsers = topUsers,
                DailyTrend = dailyTrend,
                MonthlyTrend = monthlyTrend,
                YearlyTrend = yearlyTrend,
                ProductDist = productDist,
                PriorityDist = priorityDist,
                StatusDist = statusDist
            }));
        }/// <summary>
         /// Admin kişisel performans raporu — sadece kendi istatistikleri
         /// </summary>
        [HttpGet("admin-report")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetAdminReport()
        {
            int userId = GetUserId();
            using var connection = _context.CreateConnection();

            // ── 1. GENEL KİŞİSEL İSTATİSTİKLER ─────────────────────────────────
            const string generalSql = @"
        SELECT
            COUNT(*)                                                                          AS TotalAssigned,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                                     AS TotalResolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)                                     AS TotalFailed,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END)                            AS ActiveCount,

            -- Bugün
            SUM(CASE WHEN CAST(ClosedDate AS DATE) = CAST(GETDATE() AS DATE)
                      AND Status IN (2,3) THEN 1 ELSE 0 END)                                AS TodayResolved,
            SUM(CASE WHEN CAST(AssignedDate AS DATE) = CAST(GETDATE() AS DATE)
                      THEN 1 ELSE 0 END)                                                    AS TodayAssigned,

            -- Bu hafta
            SUM(CASE WHEN ClosedDate >= DATEADD(DAY, -(DATEPART(WEEKDAY, GETDATE())-2), CAST(GETDATE() AS DATE))
                      AND Status IN (2,3) THEN 1 ELSE 0 END)                                AS ThisWeekResolved,
            SUM(CASE WHEN AssignedDate >= DATEADD(DAY, -(DATEPART(WEEKDAY, GETDATE())-2), CAST(GETDATE() AS DATE))
                      THEN 1 ELSE 0 END)                                                    AS ThisWeekAssigned,

            -- Bu ay
            SUM(CASE WHEN MONTH(ClosedDate) = MONTH(GETDATE()) AND YEAR(ClosedDate) = YEAR(GETDATE())
                      AND Status IN (2,3) THEN 1 ELSE 0 END)                                AS ThisMonthResolved,
            SUM(CASE WHEN MONTH(AssignedDate) = MONTH(GETDATE()) AND YEAR(AssignedDate) = YEAR(GETDATE())
                      THEN 1 ELSE 0 END)                                                    AS ThisMonthAssigned,

            -- Bu yıl
            SUM(CASE WHEN YEAR(ClosedDate) = YEAR(GETDATE())
                      AND Status IN (2,3) THEN 1 ELSE 0 END)                                AS ThisYearResolved,
            SUM(CASE WHEN YEAR(AssignedDate) = YEAR(GETDATE())
                      THEN 1 ELSE 0 END)                                                    AS ThisYearAssigned,

            -- Ortalama süreler
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0)            AS AvgWorkingMinute,
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            AND MONTH(ClosedDate) = MONTH(GETDATE())
                            AND YEAR(ClosedDate) = YEAR(GETDATE())
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0)            AS ThisMonthAvgMinute,
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            AND CAST(ClosedDate AS DATE) = CAST(GETDATE() AS DATE)
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0)            AS TodayAvgMinute,

            -- Başarı oranı
            CASE WHEN SUM(CASE WHEN Status IN (2,3) THEN 1 ELSE 0 END) = 0 THEN 0
                 ELSE CAST(SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS FLOAT) /
                      SUM(CASE WHEN Status IN (2,3) THEN 1 ELSE 0 END) * 100
            END AS SuccessRate,

            -- En hızlı / en yavaş çözüm
            MIN(CASE WHEN Status IN (2,3) AND WorkingMinute > 0
                     THEN WorkingMinute ELSE NULL END)                                      AS MinWorkingMinute,
            MAX(CASE WHEN Status IN (2,3)
                     THEN WorkingMinute ELSE NULL END)                                      AS MaxWorkingMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND AssignedToUserID = @UserId
    ";
            dynamic? generalStats = await connection.QueryFirstOrDefaultAsync(generalSql, new { UserId = userId });

            // ── 2. ÜZERİMDEKİ AKTİF TİCKETLAR ─────────────────────────────────
            const string activeTicketsSql = @"
        SELECT
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.AssignedDate, t.WorkingMinute,
            c.CustomerName, c.CustomerCode,
            lp.LogoProductName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName,
            cu.PhoneNumber AS CreatedByPhone,
            cu.EMailAddress AS CreatedByEmail,
            DATEDIFF(HOUR, t.OpenedDate, GETDATE()) AS WaitingHours
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers    c  WITH (NOLOCK) ON t.CustomerID      = c.ID
        LEFT  JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID   = lp.ID
        LEFT  JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID = cu.ID
        WHERE t.IsDeleted = 0
          AND t.AssignedToUserID = @UserId
          AND t.Status NOT IN (2, 3, 6)
        ORDER BY t.Priority DESC, t.OpenedDate ASC
    ";
            IEnumerable<dynamic> activeTickets = await connection.QueryAsync(activeTicketsSql, new { UserId = userId });

            // ── 3. SON ÇÖZÜLEN TİCKETLAR (Son 20) ──────────────────────────────
            const string recentResolvedSql = @"
        SELECT TOP 20
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.ClosedDate, t.WorkingMinute,
            t.SolutionNote,
            c.CustomerName, c.CustomerCode,
            lp.LogoProductName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers    c  WITH (NOLOCK) ON t.CustomerID      = c.ID
        LEFT  JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID   = lp.ID
        LEFT  JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID = cu.ID
        WHERE t.IsDeleted = 0
          AND t.AssignedToUserID = @UserId
          AND t.Status IN (2, 3)
        ORDER BY t.ClosedDate DESC
    ";
            IEnumerable<dynamic> recentResolved = await connection.QueryAsync(recentResolvedSql, new { UserId = userId });

            // ── 4. AYLIK TREND (Son 12 ay) ───────────────────────────────────────
            const string monthlyTrendSql = @"
        SELECT
            YEAR(ClosedDate)                                    AS Year,
            MONTH(ClosedDate)                                   AS Month,
            COUNT(*)                                            AS Total,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)        AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)        AS Failed,
            ISNULL(AVG(CAST(WorkingMinute AS FLOAT)), 0)        AS AvgMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND AssignedToUserID = @UserId
          AND Status IN (2, 3)
          AND ClosedDate >= DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
        GROUP BY YEAR(ClosedDate), MONTH(ClosedDate)
        ORDER BY YEAR(ClosedDate) ASC, MONTH(ClosedDate) ASC
    ";
            IEnumerable<dynamic> monthlyTrend = await connection.QueryAsync(monthlyTrendSql, new { UserId = userId });

            // ── 5. GÜNLÜK TREND (Son 30 gün) ────────────────────────────────────
            const string dailyTrendSql = @"
        SELECT
            CAST(ClosedDate AS DATE)                            AS Date,
            COUNT(*)                                            AS Total,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)        AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)        AS Failed,
            ISNULL(AVG(CAST(WorkingMinute AS FLOAT)), 0)        AS AvgMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND AssignedToUserID = @UserId
          AND Status IN (2, 3)
          AND ClosedDate >= DATEADD(DAY, -29, CAST(GETDATE() AS DATE))
        GROUP BY CAST(ClosedDate AS DATE)
        ORDER BY CAST(ClosedDate AS DATE) ASC
    ";
            IEnumerable<dynamic> dailyTrend = await connection.QueryAsync(dailyTrendSql, new { UserId = userId });

            // ── 6. ÖNCELİK BAZLI DAĞILIM ────────────────────────────────────────
            const string priorityDistSql = @"
        SELECT
            Priority,
            COUNT(*)                                            AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS ActiveCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)        AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)        AS Failed,
            ISNULL(AVG(CASE WHEN Status IN (2,3)
                            THEN CAST(WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgMinute
        FROM Tickets WITH (NOLOCK)
        WHERE IsDeleted = 0
          AND AssignedToUserID = @UserId
        GROUP BY Priority
        ORDER BY Priority DESC
    ";
            IEnumerable<dynamic> priorityDist = await connection.QueryAsync(priorityDistSql, new { UserId = userId });

            // ── 7. LOGO ÜRÜN BAZLI DAĞILIM ──────────────────────────────────────
            const string productDistSql = @"
        SELECT
            lp.LogoProductName,
            COUNT(t.ID)                                         AS Total,
            SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END)      AS Resolved,
            SUM(CASE WHEN t.Status = 3 THEN 1 ELSE 0 END)      AS Failed,
            SUM(CASE WHEN t.Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS ActiveCount,
            ISNULL(AVG(CASE WHEN t.Status IN (2,3)
                            THEN CAST(t.WorkingMinute AS FLOAT) ELSE NULL END), 0) AS AvgMinute
        FROM Tickets t WITH (NOLOCK)
        LEFT JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID = lp.ID
        WHERE t.IsDeleted = 0
          AND t.AssignedToUserID = @UserId
        GROUP BY lp.LogoProductName
        ORDER BY Total DESC
    ";
            IEnumerable<dynamic> productDist = await connection.QueryAsync(productDistSql, new { UserId = userId });

            // ── 8. EN UZUN SÜREN TİCKETLAR (Top 5) ─────────────────────────────
            const string longestTicketsSql = @"
        SELECT TOP 5
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.WorkingMinute, t.OpenedDate, t.ClosedDate,
            c.CustomerName
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers c WITH (NOLOCK) ON t.CustomerID = c.ID
        WHERE t.IsDeleted = 0
          AND t.AssignedToUserID = @UserId
          AND t.Status IN (2, 3)
          AND t.WorkingMinute > 0
        ORDER BY t.WorkingMinute DESC
    ";
            IEnumerable<dynamic> longestTickets = await connection.QueryAsync(longestTicketsSql, new { UserId = userId });

            return Ok(ApiResponse<object>.Ok(new
            {
                GeneralStats = generalStats,
                ActiveTickets = activeTickets,
                RecentResolved = recentResolved,
                MonthlyTrend = monthlyTrend,
                DailyTrend = dailyTrend,
                PriorityDist = priorityDist,
                ProductDist = productDist,
                LongestTickets = longestTickets
            }));
        }
        // ────────────────────────────────────────────────────────────────────
        #region Private Helpers

        private async Task SendTicketClosedMailAsync(
        System.Data.IDbConnection connection,
        int ticketId,
        int customerId,
        byte status)
        {
            var ticket = await connection.QueryFirstOrDefaultAsync(@"
        SELECT
            t.TicketNo, t.Title, t.WorkingMinute,
            t.OpenedDate, t.ResolvedDate, t.SolutionNote, t.CancelReason,
            ISNULL(au.FullName, au.Username) AS AssignedToName,
            c.CustomerName
        FROM Tickets t WITH (NOLOCK)
        LEFT JOIN Users     au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
        LEFT JOIN Customers c  WITH (NOLOCK) ON t.CustomerID       = c.ID
        WHERE t.ID = @ID",
                new { ID = ticketId });

            if (ticket == null) return;

            const string usersSql = @"
    SELECT u.EMailAddress, u.FullName
    FROM Users u WITH (NOLOCK)
    INNER JOIN Customers c WITH (NOLOCK) ON u.CompanyID = c.ID
    WHERE u.CompanyID = @CompanyID
      AND u.Status    = 1
      AND u.SendEmail = 1
      AND c.Status    = 1
";
            IEnumerable<dynamic> recipients =
                await connection.QueryAsync(usersSql, new { CompanyID = customerId });

            if (!recipients.Any()) return;

            int totalMin = (int)(ticket.WorkingMinute ?? 0);
            string workingDisplay = totalMin == 0 ? "Belirtilmemiş"
                : totalMin < 60 ? $"{totalMin} dakika"
                : $"{totalMin / 60} saat {totalMin % 60} dakika";

            string openedDate = ticket.OpenedDate != null ? ((DateTime)ticket.OpenedDate).ToString("dd.MM.yyyy HH:mm") : "-";
            string resolvedDate = ticket.ResolvedDate != null ? ((DateTime)ticket.ResolvedDate).ToString("dd.MM.yyyy HH:mm") : "-";

            string statusText, statusColor, headerBg, borderColor, statusEmoji;
            switch (status)
            {
                case 2:
                    statusText = "Başarıyla Çözüldü"; statusEmoji = "✅";
                    statusColor = "#16a34a"; headerBg = "#f0fdf4"; borderColor = "#bbf7d0";
                    break;
                case 3:
                    statusText = "Çözülemedi"; statusEmoji = "❌";
                    statusColor = "#dc2626"; headerBg = "#fef2f2"; borderColor = "#fecaca";
                    break;
                case 6:
                    statusText = "İptal Edildi"; statusEmoji = "🚫";
                    statusColor = "#6b7280"; headerBg = "#f1f5f9"; borderColor = "#e2e8f0";
                    break;
                default:
                    statusText = "Güncellendi"; statusEmoji = "ℹ️";
                    statusColor = "#2563eb"; headerBg = "#eff6ff"; borderColor = "#bfdbfe";
                    break;
            }

            string subject = $"Destek Talebi {statusText} — {ticket.TicketNo}";
            string assignedTo = string.IsNullOrEmpty((string?)ticket.AssignedToName)
                ? "Belirtilmemiş" : (string)ticket.AssignedToName;

            string solutionBlock = !string.IsNullOrEmpty((string?)ticket.SolutionNote)
                ? $@"<tr>
               <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;width:40%;'>Çözüm Notu</td>
               <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{ticket.SolutionNote}</td>
             </tr>"
                : string.Empty;

            string cancelBlock = status == 6 && !string.IsNullOrEmpty((string?)ticket.CancelReason)
                ? $@"<tr>
               <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;width:40%;'>İptal Nedeni</td>
               <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;color:#dc2626;font-weight:600;'>{ticket.CancelReason}</td>
             </tr>"
                : string.Empty;

            string body = $@"
        <div style='background:{headerBg};border:1px solid {borderColor};border-radius:12px;padding:20px 24px;margin-bottom:20px;text-align:center;'>
            <div style='font-size:2rem;margin-bottom:8px;'>{statusEmoji}</div>
            <h2 style='margin:0;color:{statusColor};font-size:1.2rem;'>Destek Talebi {statusText}</h2>
            <p style='margin:6px 0 0;color:#6b7280;font-size:.9rem;'>
                <strong style='color:#0f1117;'>{ticket.TicketNo}</strong> numaralı talep güncellendi.
            </p>
        </div>
        <table style='width:100%;border-collapse:collapse;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;margin-bottom:20px;'>
            <tr style='background:#f7f8fc;'>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;width:40%;'>Talep No</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-family:monospace;font-weight:700;color:#2563eb;'>{ticket.TicketNo}</td>
            </tr>
            <tr>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Firma</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{ticket.CustomerName}</td>
            </tr>
            <tr style='background:#f7f8fc;'>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Konu</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{ticket.Title}</td>
            </tr>
            <tr>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>İşlemi Alan</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{assignedTo}</td>
            </tr>
            <tr style='background:#f7f8fc;'>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Açılış Tarihi</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{openedDate}</td>
            </tr>
            <tr>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Kapanış Tarihi</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{resolvedDate}</td>
            </tr>
            <tr style='background:#f7f8fc;'>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Toplam Süre</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#0f1117;'>⏱ {workingDisplay}</td>
            </tr>
            <tr>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Sonuç</td>
                <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;color:{statusColor};font-weight:700;'>{statusEmoji} {statusText}</td>
            </tr>
            {solutionBlock}
            {cancelBlock}
        </table>
        <p style='color:#9ca3af;font-size:.78rem;text-align:center;margin:0;'>
            Bu mail otomatik olarak gönderilmiştir. Lütfen yanıtlamayınız.
        </p>
    ";

            foreach (dynamic recipient in recipients)
            {
                try
                {
                    await _mailService.SendAsync(
                        (string)recipient.EMailAddress,
                        (string)recipient.FullName,
                        subject,
                        body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mail gönderilemedi: {Email}, TicketID: {TicketID}",
                        (string)recipient.EMailAddress, ticketId);
                }
            }
        }
        #endregion

        /// <summary>
        /// Gelişmiş ticket listesi — tüm statuslar dahil, filtreli
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? search = null,
            [FromQuery] int? status = null,
            [FromQuery] int? priority = null,
            [FromQuery] int? customerId = null,
            [FromQuery] int? logoProductId = null,
            [FromQuery] int? assignedToUserId = null,
            [FromQuery] int? createdByUserId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            using var connection = _context.CreateConnection();

            var conditions = new List<string> { "t.IsDeleted = 0" };
            var parameters = new DynamicParameters();

            // Rol bazlı temel kısıt
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                conditions.Add("t.CustomerID = @CompanyId");
                parameters.Add("CompanyId", GetCompanyId());
            }

            // Filtreler
            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add("(t.TicketNo LIKE @Search OR t.Title LIKE @Search OR c.CustomerName LIKE @Search)");
                parameters.Add("Search", $"%{search}%");
            }
            if (status.HasValue)
            {
                conditions.Add("t.Status = @Status");
                parameters.Add("Status", status.Value);
            }
            if (priority.HasValue)
            {
                conditions.Add("t.Priority = @Priority");
                parameters.Add("Priority", priority.Value);
            }
            if (customerId.HasValue && (IsAdmin() || IsSuperAdmin()))
            {
                conditions.Add("t.CustomerID = @CustomerIdFilter");
                parameters.Add("CustomerIdFilter", customerId.Value);
            }
            if (logoProductId.HasValue)
            {
                conditions.Add("t.LogoProductID = @LogoProductId");
                parameters.Add("LogoProductId", logoProductId.Value);
            }
            if (assignedToUserId.HasValue && (IsAdmin() || IsSuperAdmin()))
            {
                conditions.Add("t.AssignedToUserID = @AssignedToUserId");
                parameters.Add("AssignedToUserId", assignedToUserId.Value);
            }
            if (createdByUserId.HasValue)
            {
                // User sadece kendi oluşturduklarını filtreleyebilir
                if (IsUser() && !IsAdmin() && !IsSuperAdmin())
                    parameters.Add("CreatedByUserId", GetUserId());
                else
                    parameters.Add("CreatedByUserId", createdByUserId.Value);
                conditions.Add("t.CreatedByUserID = @CreatedByUserId");
            }
            if (startDate.HasValue)
            {
                conditions.Add("t.OpenedDate >= @StartDate");
                parameters.Add("StartDate", startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                conditions.Add("t.OpenedDate < @EndDate");
                parameters.Add("EndDate", endDate.Value.Date.AddDays(1));
            }

            string whereClause = "WHERE " + string.Join(" AND ", conditions);

            string sql = $@"
        SELECT
            t.ID, t.TicketNo, t.Description, t.CustomerID, t.LogoProductID,
            t.CreatedByUserID, t.AssignedToUserID,
            t.Title, t.Priority, t.Status,
            t.OpenedDate, t.AssignedDate, t.ResolvedDate, t.ClosedDate,
            t.WorkingMinute,
            c.CustomerName, c.CustomerCode,
            c.Importance        AS CustomerImportance,
            lp.LogoProductName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName,
            ISNULL(au.FullName, au.Username) AS AssignedToName,
            cu.Picture          AS CreatedByPicture,
            au.Picture          AS AssignedToPicture,
            cu.PhoneNumber      AS CreatedByPhone,
            cu.EMailAddress     AS CreatedByEmail,
            t.AssignedDate      AS TakenInProgressDate,
            t.SolutionNote,
            t.CancelReason
        FROM Tickets t WITH (NOLOCK)
        INNER JOIN Customers    c  WITH (NOLOCK) ON t.CustomerID       = c.ID
        LEFT  JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID    = lp.ID
        LEFT  JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID  = cu.ID
        LEFT  JOIN Users        au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
        {whereClause}
        ORDER BY t.OpenedDate DESC
    ";

            IEnumerable<TicketListDto> tickets = await connection.QueryAsync<TicketListDto>(sql, parameters);
            return Ok(ApiResponse<IEnumerable<TicketListDto>>.Ok(tickets));
        }
    }
}