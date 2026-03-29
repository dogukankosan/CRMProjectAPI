using CRMProjectAPI.Data;
using CRMProjectAPI.Models;
using Dapper;
using System.Text;

namespace CRMProjectAPI.Services
{
    public interface ILogService
    {
        Task LogAsync(ApiLog log);
        Task LogErrorAsync(HttpContext context, Exception ex, int? responseTime);
        Task LogRequestAsync(HttpContext context, int statusCode, int? responseTime, string? responseBody = null);
    }
    public class LogService : ILogService
    {
        private readonly DapperContext _context;
        private readonly ILogger<LogService> _logger;
        // Loglanmaması gereken endpoint'ler
        private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/health",
            "/swagger",
            "/favicon.ico"
        };
        // Hassas header'lar — maskelenir
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "X-API-Key",
            "Cookie"
        };
        // DB kolon sınırlarıyla eşleştirildi
        private const int MaxBodyLength = 4000;
        private const int MaxStackTraceLength = 4000;
        private const int MaxHeadersLength = 2000;
        private const int MaxQueryLength = 500;
        private const int MaxMessageLength = 1000;
        private const int MaxUserAgentLength = 500;
        public LogService(DapperContext context, ILogger<LogService> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task LogAsync(ApiLog log)
        {
            // Excluded path kontrolü — normalize et
            string path = log.Endpoint.ToLowerInvariant();
            if (ExcludedPaths.Any(p => path.StartsWith(p)))
                return;
            const string sql = @"
                INSERT INTO ApiLog 
                (HttpMethod, Endpoint, QueryString, RequestBody, RequestHeaders,
                 StatusCode, ResponseBody, ResponseTrimmed, ResponseTime,
                 IsError, ErrorMessage, ErrorStackTrace, ErrorType,
                 IpAddress, UserAgent, ApiKeyHash, CreatedDate)
                VALUES 
                (@HttpMethod, @Endpoint, @QueryString, @RequestBody, @RequestHeaders,
                 @StatusCode, @ResponseBody, @ResponseTrimmed, @ResponseTime,
                 @IsError, @ErrorMessage, @ErrorStackTrace, @ErrorType,
                 @IpAddress, @UserAgent, @ApiKeyHash, @CreatedDate)";
            try
            {
                using var connection = _context.CreateConnection();
                await connection.ExecuteAsync(sql, log);
            }
            catch (Exception ex)
            {
                // Log hatası ana işlemi etkilememeli
                _logger.LogError(ex, "API log kaydedilemedi: {Endpoint}", log.Endpoint);
            }
        }
        public async Task LogErrorAsync(HttpContext context, Exception ex, int? responseTime)
        {
            ApiLog log = new ApiLog
            {
                HttpMethod = context.Request.Method,
                Endpoint = context.Request.Path,
                QueryString = TruncateString(context.Request.QueryString.ToString(), MaxQueryLength),
                RequestHeaders = GetSafeHeaders(context),
                StatusCode = 500,
                ResponseTime = responseTime,
                IsError = true,
                ErrorMessage = TruncateString(ex.Message, MaxMessageLength),
                ErrorStackTrace = TruncateString(ex.StackTrace, MaxStackTraceLength),
                ErrorType = ex.GetType().Name,
                IpAddress = GetIpAddress(context),
                UserAgent = TruncateString(context.Request.Headers.UserAgent.ToString(), MaxUserAgentLength),
                ApiKeyHash = MaskApiKey(context.Request.Headers["X-API-Key"].ToString())
            };
           await LogAsync(log);
        }
        public async Task LogRequestAsync(HttpContext context, int statusCode, int? responseTime, string? responseBody = null)
        {
            bool isTrimmed = responseBody != null && responseBody.Length > MaxBodyLength;
            ApiLog log = new ApiLog
            {
                HttpMethod = context.Request.Method,
                Endpoint = context.Request.Path,
                QueryString = TruncateString(context.Request.QueryString.ToString(), MaxQueryLength),
                RequestHeaders = GetSafeHeaders(context),
                StatusCode = statusCode,
                ResponseBody = TruncateString(responseBody, MaxBodyLength),
                ResponseTrimmed = isTrimmed,
                ResponseTime = responseTime,
                IsError = statusCode >= 400,
                IpAddress = GetIpAddress(context),
                UserAgent = TruncateString(context.Request.Headers.UserAgent.ToString(), MaxUserAgentLength),
                ApiKeyHash = MaskApiKey(context.Request.Headers["X-API-Key"].ToString())
            };
            await LogAsync(log);
        }
        #region Private Helpers
        private static string? GetIpAddress(HttpContext context)
        {
            // Proxy arkasındaysa gerçek IP'yi al
            string? ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                      ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                      ?? context.Connection.RemoteIpAddress?.ToString();
            // Virgülle ayrılmış birden fazla IP varsa ilkini al
            return ip?.Split(',').FirstOrDefault()?.Trim();
        }
        private static string? MaskApiKey(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return null;
            if (apiKey.Length < 8) return "****";
            return $"{apiKey[..4]}****{apiKey[^4..]}";
        }
        private static string? TruncateString(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength
                ? value
                : value[..maxLength] + "...[truncated]";
        }
        private static string? GetSafeHeaders(HttpContext context)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var header in context.Request.Headers)
            {
                if (SensitiveHeaders.Contains(header.Key))
                    sb.AppendLine($"{header.Key}: ****");
                else
                    sb.AppendLine($"{header.Key}: {header.Value}");
            }
            return TruncateString(sb.ToString(), MaxHeadersLength);
        }
        #endregion
    }
}