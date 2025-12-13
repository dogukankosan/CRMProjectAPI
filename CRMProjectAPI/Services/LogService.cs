using CRMProjectAPI.Data;
using CRMProjectAPI.Models;
using Dapper;

namespace CRMProjectAPI.Services
{
    public interface ILogService
    {
        Task LogAsync(ApiLog log);
        Task LogErrorAsync(HttpContext context, Exception ex, int responseTime);
        Task LogRequestAsync(HttpContext context, int statusCode, int responseTime, string? responseBody = null);
    }
    public class LogService : ILogService
    {
        private readonly DapperContext _context;
        public LogService(DapperContext context)
        {
            _context = context;
        }
        public async Task LogAsync(ApiLog log)
        {
            const string sql = @"
                INSERT INTO ApiLog 
                (HttpMethod, Endpoint, QueryString, RequestBody, RequestHeaders,
                 StatusCode, ResponseBody, ResponseTime,
                 IsError, ErrorMessage, ErrorStackTrace, ErrorType,
                 IpAddress, UserAgent, ApiKey)
                VALUES 
                (@HttpMethod, @Endpoint, @QueryString, @RequestBody, @RequestHeaders,
                 @StatusCode, @ResponseBody, @ResponseTime,
                 @IsError, @ErrorMessage, @ErrorStackTrace, @ErrorType,
                 @IpAddress, @UserAgent, @ApiKey)";
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sql, log);
        }
        public async Task LogErrorAsync(HttpContext context, Exception ex, int responseTime)
        {
            ApiLog log = new ApiLog
            {
                HttpMethod = context.Request.Method,
                Endpoint = context.Request.Path,
                QueryString = context.Request.QueryString.ToString(),
                StatusCode = 500,
                ResponseTime = responseTime,
                IsError = true,
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace,
                ErrorType = ex.GetType().Name,
                IpAddress = GetIpAddress(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                ApiKey = MaskApiKey(context.Request.Headers["X-API-Key"].ToString())
            };
            await LogAsync(log);
        }
        public async Task LogRequestAsync(HttpContext context, int statusCode, int responseTime, string? responseBody = null)
        {
            ApiLog log = new ApiLog
            {
                HttpMethod = context.Request.Method,
                Endpoint = context.Request.Path,
                QueryString = context.Request.QueryString.ToString(),
                StatusCode = statusCode,
                ResponseBody = responseBody,
                ResponseTime = responseTime,
                IsError = statusCode >= 400,
                IpAddress = GetIpAddress(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                ApiKey = MaskApiKey(context.Request.Headers["X-API-Key"].ToString())
            };
            await LogAsync(log);
        }
        private static string? GetIpAddress(HttpContext context)
        {
            // Proxy arkasındaysa gerçek IP'yi al
            string? forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
                return forwardedFor.Split(',').FirstOrDefault()?.Trim();
            return context.Connection.RemoteIpAddress?.ToString();
        }
        private static string? MaskApiKey(string? apiKey)
        {
            // API key'i loglarken maskele (güvenlik)
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
                return apiKey;
            return apiKey[..4] + "****" + apiKey[^4..];
        }
    }
}