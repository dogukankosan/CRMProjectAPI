// ApiExceptionHandlerMiddleware.cs
using CRMProjectAPI.Services;
using System.Diagnostics;
using System.Text.Json;

namespace CRMProjectAPI.Middleware
{
    public class ApiExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionHandlerMiddleware> _logger;
        // Static readonly — her exception'da new oluşturulmasın
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        public ApiExceptionHandlerMiddleware(RequestDelegate next, ILogger<ApiExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context, ILogService logService)
        {
            ArgumentNullException.ThrowIfNull(logService);
            Stopwatch stopwatch = Stopwatch.StartNew(); // Stopwatch? değil, null olamaz
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Hata oluştu: {Message}", ex.Message);
                await logService.LogErrorAsync(context, ex, (int)stopwatch.ElapsedMilliseconds);
                await HandleExceptionAsync(context, ex);
            }
        }
        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Response zaten başladıysa yazma
            if (context.Response.HasStarted) return;
            context.Response.ContentType = "application/json";
            var response = exception switch
            {
                UnauthorizedAccessException => new ExceptionResponse(401, "Yetkisiz erişim"),
                KeyNotFoundException => new ExceptionResponse(404, "Kayıt bulunamadı"),
                ArgumentException => new ExceptionResponse(400, "Geçersiz istek"),  // iç mesaj değil
                InvalidOperationException => new ExceptionResponse(400, "Geçersiz işlem"),  // iç mesaj değil
                _ => new ExceptionResponse(500, "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.")
            };
            context.Response.StatusCode = response.StatusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = response.StatusCode,
                message = response.Message,
                timestamp = DateTime.UtcNow
            }, JsonOptions));
        }
       private record ExceptionResponse(int StatusCode, string Message);
    }
}