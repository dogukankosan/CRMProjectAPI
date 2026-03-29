using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class MailSettingsApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<MailSettingsApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public MailSettingsApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<MailSettingsApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object? body = null, string? token = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, $"{_baseUrl}{endpoint}");
            request.Headers.Add("X-API-Key", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (body != null)
            {
                string json = JsonSerializer.Serialize(body, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<MailSettingsDto?> GetAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/mail-settings", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<MailSettingsDto>? result = JsonSerializer.Deserialize<ApiResponse<MailSettingsDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetMailSettingsAsync");
                return null;
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors)> UpsertAsync(MailSettingsUpdateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Put, "/api/mail-settings", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Kaydedildi", null);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: UpsertMailSettingsAsync");
                return (false, "API'ye bağlanılamadı", null);
            }
        }
        public async Task<(bool Success, string Message)> TestAsync(
            string mailTo, string subject,
            MailSettingsUpdateDto? formValues = null,
            string? token = null)
        {
            try
            {
                var body = new
                {
                    mailTo,
                    subject,
                    mailFrom = formValues?.MailFrom,
                    displayName = formValues?.DisplayName,
                    smtpHost = formValues?.SmtpHost,
                    smtpPort = formValues?.SmtpPort,
                    enableSsl = formValues?.EnableSsl,
                    username = formValues?.Username,
                    password = formValues?.Password,
                    timeoutSeconds = formValues?.TimeoutSeconds,
                    signature = formValues?.Signature
                };
                HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/mail-settings/test", body, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Mail gönderildi");
                return (false, result?.Message ?? "Mail gönderilemedi");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: TestMailAsync");
                return (false, "API'ye bağlanılamadı");
            }
        }
    }


}