// AnnouncementController.cs
using Azure;
using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Hubs;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[ApiController]
[Route("api/announcement")]
[Authorize]
public class AnnouncementController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<TicketHub> _hubContext;
    private readonly ILogger<AnnouncementController> _logger;

    public AnnouncementController(
        DapperContext context,
        IWebHostEnvironment env,
        IHubContext<TicketHub> hubContext,
        ILogger<AnnouncementController> logger)
    {
        _context = context;
        _env = env;
        _hubContext = hubContext;
        _logger = logger;
    }

    private int GetUserId() =>
        int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : 0;
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool IsAdmin() => User.IsInRole("Admin");

    // ── Kullanıcıya gösterilecek duyurular (dismiss edilmemişler)
    [HttpGet]
    public async Task<IActionResult> List()
    {
        int userId = GetUserId();
        using var connection = _context.CreateConnection();

        const string sql = @"
            SELECT
                a.ID, a.Title, a.Content, a.Priority,
                a.CreatedByUserID, a.CreatedDate, a.UpdatedDate, a.IsActive,
                ISNULL(u.FullName, u.Username) AS CreatedByName
            FROM Announcements a WITH (NOLOCK)
            LEFT JOIN Users u WITH (NOLOCK) ON a.CreatedByUserID = u.ID
            WHERE a.IsActive  = 1
              AND a.IsDeleted = 0
              AND a.ID NOT IN (
                  SELECT AnnouncementID
                  FROM AnnouncementDismissals WITH (NOLOCK)
                  WHERE UserID = @UserID
              )
            ORDER BY a.Priority DESC, a.CreatedDate DESC
        ";

        var announcements = (await connection.QueryAsync<AnnouncementDto>(
            sql, new { UserID = userId })).ToList();

        // Dosyaları yükle
        if (announcements.Any())
        {
            var ids = announcements.Select(a => a.ID).ToList();
            const string filesSql = @"
                SELECT
                    af.ID, af.AnnouncementID, af.OriginalFileName,
                    af.RelativePath, af.FileExtension, af.MimeType,
                    af.FileSizeBytes, af.UploadedDate,
                    ISNULL(u.FullName, u.Username) AS UploadedByName
                FROM AnnouncementFiles af WITH (NOLOCK)
                LEFT JOIN Users u WITH (NOLOCK) ON af.UploadedByUserID = u.ID
                WHERE af.AnnouncementID IN @IDs AND af.IsDeleted = 0
                ORDER BY af.UploadedDate ASC
            ";
            var files = (await connection.QueryAsync<AnnouncementFileDto>(
                filesSql, new { IDs = ids })).ToList();

            foreach (var ann in announcements)
                ann.Files = files.Where(f => f.AnnouncementID == ann.ID).ToList();
        }

        return Ok(ApiResponse<IEnumerable<AnnouncementDto>>.Ok(announcements));
    }

    // ── Admin: tüm duyurular
    [HttpGet("all")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ListAll()
    {
        using var connection = _context.CreateConnection();

        const string sql = @"
            SELECT
                a.ID, a.Title, a.Content, a.Priority,
                a.CreatedByUserID, a.CreatedDate, a.UpdatedDate, a.IsActive,
                ISNULL(u.FullName, u.Username) AS CreatedByName
            FROM Announcements a WITH (NOLOCK)
            LEFT JOIN Users u WITH (NOLOCK) ON a.CreatedByUserID = u.ID
            WHERE a.IsDeleted = 0
            ORDER BY a.CreatedDate DESC
        ";

        var announcements = (await connection.QueryAsync<AnnouncementDto>(sql)).ToList();

        if (announcements.Any())
        {
            var ids = announcements.Select(a => a.ID).ToList();
            const string filesSql = @"
                SELECT
                    af.ID, af.AnnouncementID, af.OriginalFileName,
                    af.RelativePath, af.FileExtension, af.MimeType,
                    af.FileSizeBytes, af.UploadedDate,
                    ISNULL(u.FullName, u.Username) AS UploadedByName
                FROM AnnouncementFiles af WITH (NOLOCK)
                LEFT JOIN Users u WITH (NOLOCK) ON af.UploadedByUserID = u.ID
                WHERE af.AnnouncementID IN @IDs AND af.IsDeleted = 0
                ORDER BY af.UploadedDate ASC
            ";
            var files = (await connection.QueryAsync<AnnouncementFileDto>(
                filesSql, new { IDs = ids })).ToList();

            foreach (var ann in announcements)
                ann.Files = files.Where(f => f.AnnouncementID == ann.ID).ToList();
        }

        return Ok(ApiResponse<IEnumerable<AnnouncementDto>>.Ok(announcements));
    }

    // ── Ekle
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create(
        [FromForm] AnnouncementCreateDto dto,
        [FromForm] List<IFormFile>? files)
    {
        var errors = AnnouncementValidation.ValidateCreate(dto);
        if (errors.Any())
            return BadRequest(ApiResponse.Fail(errors));

        if (files != null)
        {
            foreach (var f in files)
            {
                var fileErrors = AnnouncementValidation.ValidateFile(f);
                errors.AddRange(fileErrors);
            }
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));
        }

        using var connection = _context.CreateConnection();

        const string sql = @"
            INSERT INTO Announcements
                (Title, Content, Priority, CreatedByUserID, CreatedDate, IsActive, IsDeleted)
            VALUES
                (@Title, @Content, @Priority, @CreatedByUserID, GETDATE(), 1, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

        int newId = await connection.QuerySingleAsync<int>(sql, new
        {
            dto.Title,
            dto.Content,
            dto.Priority,
            CreatedByUserID = GetUserId()
        });

        // Dosyaları kaydet
        if (files != null && files.Any())
            await SaveFilesAsync(connection, newId, files);

        // SignalR — tüm kullanıcılara anlık bildir
        await _hubContext.Clients.All.SendAsync("AnnouncementCreated", new
        {
            id = newId,
            title = dto.Title,
            priority = dto.Priority
        });

        return Ok(ApiResponse<int>.Ok(newId, "Duyuru eklendi"));
    }

    // ── Güncelle
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] AnnouncementCreateDto dto,
        [FromForm] List<IFormFile>? files)
    {
        var errors = AnnouncementValidation.ValidateCreate(dto);
        if (errors.Any())
            return BadRequest(ApiResponse.Fail(errors));

        using var connection = _context.CreateConnection();

        var existing = await connection.QueryFirstOrDefaultAsync(
            "SELECT ID FROM Announcements WHERE ID = @ID AND IsDeleted = 0",
            new { ID = id });

        if (existing == null)
            return NotFound(ApiResponse.NotFound("Duyuru bulunamadı"));

        const string sql = @"
            UPDATE Announcements SET
                Title       = @Title,
                Content     = @Content,
                Priority    = @Priority,
                UpdatedDate = GETDATE()
            WHERE ID = @ID AND IsDeleted = 0
        ";

        await connection.ExecuteAsync(sql, new
        {
            dto.Title,
            dto.Content,
            dto.Priority,
            ID = id
        });

        // Yeni dosya varsa ekle
        if (files != null && files.Any())
        {
            foreach (var f in files)
            {
                var fileErrors = AnnouncementValidation.ValidateFile(f);
                errors.AddRange(fileErrors);
            }
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            await SaveFilesAsync(connection, id, files);
        }

        return Ok(ApiResponse.Ok("Duyuru güncellendi"));
    }

    // ── Dosya sil
    [HttpDelete("file/{fileId:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteFile(int fileId)
    {
        using var connection = _context.CreateConnection();

        var file = await connection.QueryFirstOrDefaultAsync(
            "SELECT RelativePath FROM AnnouncementFiles WHERE ID = @ID AND IsDeleted = 0",
            new { ID = fileId });

        if (file == null)
            return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

        await connection.ExecuteAsync(
            "UPDATE AnnouncementFiles SET IsDeleted = 1 WHERE ID = @ID",
            new { ID = fileId });

        // Fiziksel dosyayı sil
        try
        {
            string fullPath = Path.Combine(
                _env.WebRootPath, ((string)file.RelativePath).TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dosya fiziksel olarak silinemedi: {FileId}", fileId);
        }

        return Ok(ApiResponse.Ok("Dosya silindi"));
    }

    // ── Aktif/Pasif toggle
    [HttpPatch("{id:int}/toggle")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Toggle(int id)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Announcements SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE ID = @ID",
            new { ID = id });
        return Ok(ApiResponse.Ok("Durum güncellendi"));
    }

    // ── Sil
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Announcements SET IsDeleted = 1 WHERE ID = @ID",
            new { ID = id });
        return Ok(ApiResponse.Ok("Duyuru silindi"));
    }

    // ── Kullanıcı "Tekrar gösterme"
    [HttpPost("{id:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        int userId = GetUserId();
        using var connection = _context.CreateConnection();
        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO AnnouncementDismissals (AnnouncementID, UserID, DismissedDate)
                VALUES (@AID, @UID, GETDATE())",
                new { AID = id, UID = userId });
        }
        catch { } // UNIQUE constraint — zaten kapatılmış
        return Ok(ApiResponse.Ok("Kapatıldı"));
    }

    // ── Dosya indir
    [HttpGet("file/{fileId:int}/download")]
    public async Task<IActionResult> Download(int fileId)
    {
        using var connection = _context.CreateConnection();

        var file = await connection.QueryFirstOrDefaultAsync(
            "SELECT RelativePath, OriginalFileName, MimeType FROM AnnouncementFiles WHERE ID = @ID AND IsDeleted = 0",
            new { ID = fileId });

        if (file == null)
            return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

        string fullPath = Path.Combine(
            _env.WebRootPath, ((string)file.RelativePath).TrimStart('/'));

        if (!System.IO.File.Exists(fullPath))
            return NotFound(ApiResponse.NotFound("Dosya fiziksel olarak bulunamadı"));

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        string encodedName = Uri.EscapeDataString((string)file.OriginalFileName);

        Response.Headers.Append("Content-Disposition",
            $"attachment; filename=\"{encodedName}\"; filename*=UTF-8''{encodedName}");

        return File(bytes, (string)file.MimeType, (string)file.OriginalFileName);
    }

    // ── Private: dosya kaydet
    private async Task SaveFilesAsync(
        System.Data.IDbConnection connection,
        int announcementId,
        List<IFormFile> files)
    {
        string folder = Path.Combine(_env.WebRootPath, "uploads", "announcements");
        Directory.CreateDirectory(folder);

        const string sql = @"
            INSERT INTO AnnouncementFiles
                (AnnouncementID, OriginalFileName, StoredFileName, RelativePath,
                 FileExtension, MimeType, FileSizeBytes, UploadedByUserID, UploadedDate, IsDeleted)
            VALUES
                (@AnnouncementID, @OriginalFileName, @StoredFileName, @RelativePath,
                 @FileExtension, @MimeType, @FileSizeBytes, @UploadedByUserID, GETDATE(), 0)
        ";

        foreach (var file in files)
        {
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string stored = $"{Guid.NewGuid():N}{ext}";
            string fullPath = Path.Combine(folder, stored);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            await connection.ExecuteAsync(sql, new
            {
                AnnouncementID = announcementId,
                OriginalFileName = file.FileName,
                StoredFileName = stored,
                RelativePath = $"/uploads/announcements/{stored}",
                FileExtension = ext,
                MimeType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedByUserID = GetUserId()
            });
        }
    }
}