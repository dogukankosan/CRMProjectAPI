using System.Net.Http.Headers;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class LogApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<LogApiService> _logger;

        public LogApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<LogApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                          ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                          ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }

        public async Task LogUiErrorAsync(
            string? message,
            string? stack,
            string? page,
            string? token)
        {
            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Post, $"{_baseUrl}/api/log/ui-error");

                request.Headers.Add("X-API-Key", _apiKey);

                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);

                request.Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        message,
                        stackTrace = stack,
                        page,
                        occurredAt = DateTime.UtcNow
                    }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UI hata logu gönderilemedi");
            }
        }
    }
}