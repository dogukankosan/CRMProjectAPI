using System.Security.Cryptography;

namespace CRMProjectAPI
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyMiddleware> _logger;
        private readonly string _apiKey;
        private const string API_KEY_HEADER = "X-API-Key";
        private static readonly HashSet<string> _whitelist = new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/login"
        };
        private static readonly string[] BypassPaths = { "/swagger", "/health", "/uploads" }; // ← /uploads ekle
        public ApiKeyMiddleware(
            RequestDelegate next,
            ILogger<ApiKeyMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _apiKey = configuration["ApiKey"]
                ?? throw new InvalidOperationException("API Key konfigürasyonda tanımlı değil!");
        }
        public async Task InvokeAsync(HttpContext context)
        {
            // Whitelist — login API key gerektirmez
            if (_whitelist.Contains(context.Request.Path.Value ?? ""))
            {
                await _next(context);
                return;
            }
            // Swagger / health bypass
            if (BypassPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
            {
                await _next(context);
                return;
            }
            if (!context.Request.Headers.TryGetValue(API_KEY_HEADER, out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "API Key eksik" });
                return;
            }
            byte[] salt = System.Text.Encoding.UTF8.GetBytes("crm-api-key-salt");
            byte[] storedHash = HMACSHA256.HashData(salt, System.Text.Encoding.UTF8.GetBytes(_apiKey));
            byte[] extractedHash = HMACSHA256.HashData(salt, System.Text.Encoding.UTF8.GetBytes(extractedApiKey.ToString()));
            if (!CryptographicOperations.FixedTimeEquals(storedHash, extractedHash))
            {
                string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown";
                _logger.LogWarning("Geçersiz API Key denemesi: {IP}", ip);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "API Key geçersiz" });
                return;
            }
            await _next(context);
        }
    }
}