using CRMProjectAPI.Helpers;
using CRMProjectAPI.Services;
using System.Diagnostics;
using System.Text.Json;

namespace CRMProjectAPI.Middleware
{
    public class ApiExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionHandlerMiddleware> _logger;
        public ApiExceptionHandlerMiddleware(RequestDelegate next, ILogger<ApiExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context, ILogService logService)
        {
            Stopwatch? stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Hata oluştu: {Message}", ex.Message);
                // Hatayı DB'ye logla
                await logService.LogErrorAsync(context, ex, (int)stopwatch.ElapsedMilliseconds);
                // Kullanıcıya güzel yanıt dön
                await HandleExceptionAsync(context, ex);
            }
        }
        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = exception switch
            {
                // Özel exception tipleri
                UnauthorizedAccessException => new ExceptionResponse(401, "Yetkisiz erişim"),
                KeyNotFoundException => new ExceptionResponse(404, "Kayıt bulunamadı"),
                ArgumentException => new ExceptionResponse(400, exception.Message),
                InvalidOperationException => new ExceptionResponse(400, exception.Message),
                // Genel hata
                _ => new ExceptionResponse(500, "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.")
            };
            context.Response.StatusCode = response.StatusCode;
            string? jsonResponse = JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = response.StatusCode,
                message = response.Message,
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await context.Response.WriteAsync(jsonResponse);
        }
        private record ExceptionResponse(int StatusCode, string Message);
    }
}