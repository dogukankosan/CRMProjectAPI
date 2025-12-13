namespace CRMProjectAPI
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private const string API_KEY_HEADER = "X-API-Key"; // Header adı
        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            // Swagger için bypass (development)
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }
            // Header'dan API Key al
            if (!context.Request.Headers.TryGetValue(API_KEY_HEADER, out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "API Key eksik" });
                return;
            }
            // appsettings.json'dan doğru key'i al
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            string? apiKey = configuration["ApiKey"];
            // Key kontrolü
            if (!apiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "API Key geçersiz" });
                return;
            }
            await _next(context);
        }
    }
}