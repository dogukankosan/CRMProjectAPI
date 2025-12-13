using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Validations;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectAPI.Controllers
{
    [ApiController]
    [Route("api/company")]
    public class CompanyController : ControllerBase
    {
        private readonly DapperContext _context;
        public CompanyController(DapperContext context)
        {
            _context = context;
        }
        /// <summary>
        /// Firma bilgilerini getir (tek kayıt)
        /// </summary>
        [HttpGet("single")]
        public async Task<IActionResult> GetSingle()
        {
            const string sql = @"
                SELECT TOP 1 *
                FROM Company WITH (NOLOCK)
                ORDER BY ID ASC
            ";
            using var connection = _context.CreateConnection();
            var company = await connection.QueryFirstOrDefaultAsync(sql);
            if (company == null)
                return NotFound(ApiResponse.NotFound("Firma bilgisi bulunamadı"));
            return Ok(ApiResponse<object>.Ok(company));
        }

        /// <summary>
        /// Firma bilgilerini listele (tek kayıt ama liste döner)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List()
        {
            const string sql = @"
                SELECT TOP 1 *
                FROM Company WITH (NOLOCK)
                ORDER BY ID ASC
            ";
            using var connection = _context.CreateConnection();
            var companyList = await connection.QueryAsync(sql);
            return Ok(ApiResponse<object>.Ok(companyList));
        }
        /// <summary>
        /// Firma bilgilerini güncelle
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] CompanyDto dto)
        {
            var validationErrors = CompanyValidation.Validate(dto);
            if (validationErrors.Any())
                return BadRequest(ApiResponse.Fail(validationErrors));
            const string sql = @"
                UPDATE Company SET
                    CompanyName = @CompanyName,
                    ShortCompanyName = @ShortCompanyName,
                    Slogan = @Slogan,
                    Email = @Email,
                    Phone = @Phone,
                    Phone2 = @Phone2,
                    Address = @Address,
                    GoogleMapsEmbed = @GoogleMapsEmbed,
                    WebSiteLink = @WebSiteLink,
                    WebSiteTitle = @WebSiteTitle,
                    LogoPath = @LogoPath,
                    FaviconPath = @FaviconPath,
                    MetaTitle = @MetaTitle,
                    MetaDescription = @MetaDescription,
                    MetaKeywords = @MetaKeywords,
                    SectorDescription = @SectorDescription,
                    CanonicalUrl = @CanonicalUrl,
                    InstagramLink = @InstagramLink,
                    LinkedinLink = @LinkedinLink,
                    YoutubeLink = @YoutubeLink,
                    ExternalWebLink = @ExternalWebLink,
                    AboutUsShort = @AboutUsShort,
                    AboutUs = @AboutUs,
                    Vision = @Vision,
                    Mission = @Mission,
                    WorkingHours = @WorkingHours,
                    FoundedYear = @FoundedYear,
                    UpdatedDate = GETDATE()
                WHERE ID = 1
            ";
            using var connection = _context.CreateConnection();
            int affectedRows = await connection.ExecuteAsync(sql, dto);
            if (affectedRows == 0)
                throw new InvalidOperationException("Firma kaydı güncellenemedi.");
            return Ok(ApiResponse.Ok("Firma bilgileri başarıyla güncellendi"));
        }
    }
}