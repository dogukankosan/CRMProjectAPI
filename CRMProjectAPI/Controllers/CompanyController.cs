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
    [Route("api/company")]
    public class CompanyController : ControllerBase
    {
        private readonly DapperContext _context;

        private readonly IWebHostEnvironment _env;

        public CompanyController(DapperContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        [HttpPost("logo")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UploadLogo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            string[] allowed = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension))
                return BadRequest(ApiResponse.Fail("Geçersiz format"));
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya 5MB'dan büyük olamaz"));

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string folderPath = Path.Combine(webRoot, "uploads", "logos");
            Directory.CreateDirectory(folderPath);
            string fileName = $"{Guid.NewGuid():N}{extension}";
            string fullPath = Path.Combine(folderPath, fileName);
            string relativePath = $"/uploads/logos/{fileName}";

            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            // Eski logoyu sil
            const string selectSql = "SELECT LogoPath FROM Company WHERE ID = 1";
            using var connection = _context.CreateConnection();
            string? oldPath = await connection.QueryFirstOrDefaultAsync<string>(selectSql);
            if (!string.IsNullOrEmpty(oldPath))
            {
                string oldFullPath = Path.Combine(webRoot, oldPath.TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath)) System.IO.File.Delete(oldFullPath);
            }

            await connection.ExecuteAsync(
                "UPDATE Company SET LogoPath = @Path, UpdatedDate = GETDATE() WHERE ID = 1",
                new { Path = relativePath });

            return Ok(ApiResponse<string>.Ok(relativePath, "Logo güncellendi"));
        }

        [HttpPost("favicon")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UploadFavicon(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            string[] allowed = { ".ico", ".png" };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension))
                return BadRequest(ApiResponse.Fail("Sadece ICO ve PNG desteklenir"));
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya 2MB'dan büyük olamaz"));

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string folderPath = Path.Combine(webRoot, "uploads", "favicons");
            Directory.CreateDirectory(folderPath);
            string fileName = $"{Guid.NewGuid():N}{extension}";
            string fullPath = Path.Combine(folderPath, fileName);
            string relativePath = $"/uploads/favicons/{fileName}";

            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            // Eski favicon'u sil
            const string selectSql = "SELECT FaviconPath FROM Company WHERE ID = 1";
            using var connection = _context.CreateConnection();
            string? oldPath = await connection.QueryFirstOrDefaultAsync<string>(selectSql);
            if (!string.IsNullOrEmpty(oldPath))
            {
                string oldFullPath = Path.Combine(webRoot, oldPath.TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath)) System.IO.File.Delete(oldFullPath);
            }

            await connection.ExecuteAsync(
                "UPDATE Company SET FaviconPath = @Path, UpdatedDate = GETDATE() WHERE ID = 1",
                new { Path = relativePath });

            return Ok(ApiResponse<string>.Ok(relativePath, "Favicon güncellendi"));
        }
        /// <summary>
        /// Firma bilgilerini getir — login sayfasında da kullanılıyor, herkese açık
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get()
        {
            const string sql = @"
                SELECT 
                    ID, CompanyName, ShortCompanyName, Slogan,
                    Email, Phone, Phone2, Address, GoogleMapsEmbed, WorkingHours,
                    WebSiteLink, WebSiteTitle, LogoPath, FaviconPath, CanonicalUrl,
                    InstagramLink, LinkedinLink, YoutubeLink, ExternalWebLink,
                    MetaTitle, MetaDescription, MetaKeywords,
                    SectorDescription, AboutUsShort, AboutUs,
                    Vision, Mission, FoundedYear,
                    CreatedDate, UpdatedDate
                FROM Company WITH (NOLOCK)
                WHERE ID = 1
            ";
            using var connection = _context.CreateConnection();
            var company = await connection.QueryFirstOrDefaultAsync<CompanyDto>(sql);
            if (company == null)
                return NotFound(ApiResponse.NotFound("Firma bilgisi bulunamadı"));
            return Ok(ApiResponse<CompanyDto>.Ok(company));
        }

        /// <summary>
        /// Firma bilgilerini güncelle — sadece admin
        /// </summary>
        [HttpPut]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Update([FromBody] CompanyDto dto)
        {
            var validationErrors = CompanyValidation.Validate(dto);
            if (validationErrors.Any())
                return BadRequest(ApiResponse.Fail(validationErrors));

            const string sql = @"
    UPDATE Company SET
        CompanyName       = @CompanyName,
        ShortCompanyName  = @ShortCompanyName,
        Slogan            = @Slogan,
        Email             = @Email,
        Phone             = @Phone,
        Phone2            = @Phone2,
        Address           = @Address,
        GoogleMapsEmbed   = @GoogleMapsEmbed,
        WebSiteLink       = @WebSiteLink,
        WebSiteTitle      = @WebSiteTitle,
        MetaTitle         = @MetaTitle,
        MetaDescription   = @MetaDescription,
        MetaKeywords      = @MetaKeywords,
        SectorDescription = @SectorDescription,
        CanonicalUrl      = @CanonicalUrl,
        InstagramLink     = @InstagramLink,
        LinkedinLink      = @LinkedinLink,
        YoutubeLink       = @YoutubeLink,
        ExternalWebLink   = @ExternalWebLink,
        AboutUsShort      = @AboutUsShort,
        AboutUs           = @AboutUs,
        Vision            = @Vision,
        Mission           = @Mission,
        WorkingHours      = @WorkingHours,
        FoundedYear       = @FoundedYear,
        UpdatedDate       = GETDATE()
    WHERE ID = 1
";
            using var connection = _context.CreateConnection();
            int affectedRows = await connection.ExecuteAsync(sql, dto);
            if (affectedRows == 0)
                return NotFound(ApiResponse.NotFound("Firma kaydı bulunamadı"));
            return Ok(ApiResponse.Ok("Firma bilgileri başarıyla güncellendi"));
        }
    }
}