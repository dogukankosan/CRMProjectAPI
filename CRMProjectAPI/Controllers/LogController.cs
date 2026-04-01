using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectAPI.Controllers
{
    [Route("api/log")]
    [ApiController]
    [Authorize]
    public class LogController : ControllerBase
    {
        private readonly ILogService _logService;
        private readonly DapperContext _context;
        private readonly ILogger<LogController> _logger;

        public LogController(ILogService logService, DapperContext context, ILogger<LogController> logger)
        {
            _logService = logService;
            _context = context;
            _logger = logger;
        }

        // ── UI Hata Logu Kaydet ──────────────────────────────────────────
        [HttpPost("ui-error")]
        public async Task<IActionResult> LogUiError([FromBody] UiErrorDto dto)
        {
            try
            {
                var log = new ApiLog
                {
                    HttpMethod = "UI",
                    Endpoint = dto.Page ?? "unknown",
                    QueryString = null,
                    RequestHeaders = null,
                    StatusCode = 0,
                    ResponseTime = null,
                    IsError = true,
                    ErrorMessage = dto.Message != null
                                        ? dto.Message[..Math.Min(dto.Message.Length, 1000)]
                                        : null,
                    ErrorStackTrace = dto.StackTrace != null
                                        ? dto.StackTrace[..Math.Min(dto.StackTrace.Length, 4000)]
                                        : null,
                    ErrorType = "UIError",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString(),
                    ApiKeyHash = null,
                    CreatedDate = DateTime.UtcNow
                };
                await _logService.LogAsync(log);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UI hata logu kaydedilemedi");
                return Ok();
            }
        }

        // ── Log Listesi ──────────────────────────────────────────────────
        [HttpGet("list")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetLogs(
            [FromQuery] string? type = null,  // "UI" | "API" | null=hepsi
            [FromQuery] bool? onlyErrors = null,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var conditions = new List<string>();
                var parameters = new DynamicParameters();

                if (type == "UI")
                {
                    conditions.Add("HttpMethod = 'UI'");
                }
                else if (type == "API")
                {
                    conditions.Add("HttpMethod != 'UI'");
                }

                if (onlyErrors == true)
                {
                    conditions.Add("IsError = 1");
                }

                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var sd))
                {
                    conditions.Add("CreatedDate >= @StartDate");
                    parameters.Add("StartDate", sd.Date);
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var ed))
                {
                    conditions.Add("CreatedDate < @EndDate");
                    parameters.Add("EndDate", ed.Date.AddDays(1));
                }

                string where = conditions.Any()
                    ? "WHERE " + string.Join(" AND ", conditions)
                    : "";

                // Toplam kayıt sayısı
                int total = await connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM ApiLog WITH (NOLOCK) {where}", parameters);

                // Sayfalı liste
                int offset = (page - 1) * pageSize;
                parameters.Add("Offset", offset);
                parameters.Add("PageSize", pageSize);

                string sql = $@"
                    SELECT
                        ID, HttpMethod, Endpoint, QueryString,
                        StatusCode, ResponseTime, IsError,
                        ErrorMessage, ErrorStackTrace, ErrorType,
                        IpAddress, UserAgent, CreatedDate
                    FROM ApiLog WITH (NOLOCK)
                    {where}
                    ORDER BY CreatedDate DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                ";

                var logs = await connection.QueryAsync(sql, parameters);

                return Ok(ApiResponse<object>.Ok(new
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Pages = (int)Math.Ceiling((double)total / pageSize),
                    Data = logs
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log listesi alınamadı");
                return StatusCode(500, ApiResponse.Fail("Log listesi alınamadı"));
            }
        }

        // ── Log Detay ────────────────────────────────────────────────────
        [HttpGet("detail/{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetLogDetail(long id)
        {
            try
            {
                using var connection = _context.CreateConnection();
                var log = await connection.QueryFirstOrDefaultAsync(
                    "SELECT * FROM ApiLog WITH (NOLOCK) WHERE ID = @ID",
                    new { ID = id });

                if (log == null)
                    return NotFound(ApiResponse.NotFound("Log kaydı bulunamadı"));

                return Ok(ApiResponse<object>.Ok(log));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log detayı alınamadı. ID: {ID}", id);
                return StatusCode(500, ApiResponse.Fail("Log detayı alınamadı"));
            }
        }

        // ── Log Sil (tekil) ──────────────────────────────────────────────
        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteLog(long id)
        {
            try
            {
                using var connection = _context.CreateConnection();
                int affected = await connection.ExecuteAsync(
                    "DELETE FROM ApiLog WHERE ID = @ID", new { ID = (int)id });

                if (affected == 0)
                    return NotFound(ApiResponse.NotFound("Log kaydı bulunamadı"));

                return Ok(ApiResponse.Ok("Log silindi"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log silinemedi. ID: {ID}", id);
                return StatusCode(500, ApiResponse.Fail("Log silinemedi"));
            }
        }

        // ── Toplu Sil ────────────────────────────────────────────────────
        [HttpDelete("clear")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ClearLogs(
            [FromQuery] string? type = null,   // "UI" | "API" | null=hepsi
            [FromQuery] string? beforeDate = null)  // bu tarihten öncekiler
        {
            try
            {
                using var connection = _context.CreateConnection();

                var conditions = new List<string>();
                var parameters = new DynamicParameters();

                if (type == "UI")
                    conditions.Add("HttpMethod = 'UI'");
                else if (type == "API")
                    conditions.Add("HttpMethod != 'UI'");

                if (!string.IsNullOrEmpty(beforeDate) && DateTime.TryParse(beforeDate, out var bd))
                {
                    conditions.Add("CreatedDate < @BeforeDate");
                    parameters.Add("BeforeDate", bd.Date);
                }

                string where = conditions.Any()
                    ? "WHERE " + string.Join(" AND ", conditions)
                    : "";

                int affected = await connection.ExecuteAsync(
                    $"DELETE FROM ApiLog {where}", parameters);

                return Ok(ApiResponse<int>.Ok(affected, $"{affected} log kaydı silindi"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loglar temizlenemedi");
                return StatusCode(500, ApiResponse.Fail("Loglar temizlenemedi"));
            }
        }
    }
}