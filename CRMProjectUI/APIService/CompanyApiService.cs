using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class CompanyApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<CompanyApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        public async Task<(bool Success, string Message, string? Path)> UploadLogoAsync(
            Stream fileStream, string fileName, string? token = null)
        {
            return await UploadFileAsync("/api/company/logo", fileStream, fileName, token);
        }

        public async Task<(bool Success, string Message, string? Path)> UploadFaviconAsync(
            Stream fileStream, string fileName, string? token = null)
        {
            return await UploadFileAsync("/api/company/favicon", fileStream, fileName, token);
        }

        private async Task<(bool Success, string Message, string? Path)> UploadFileAsync(
            string endpoint, Stream fileStream, string fileName, string? token = null)
        {
            try
            {
                MultipartFormDataContent content = new MultipartFormDataContent();
                StreamContent streamContent = new StreamContent(fileStream);
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                string mime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    ".ico" => "image/x-icon",
                    _ => "application/octet-stream"
                };
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                content.Add(streamContent, "file", fileName);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{endpoint}");
                request.Headers.Add("X-API-Key", _apiKey);
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<string>? result = JsonSerializer.Deserialize<ApiResponse<string>>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Yüklendi", result?.Data);
                return (false, result?.Message ?? "Yüklenemedi", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Upload hatası: {Endpoint}", endpoint);
                return (false, "API'ye bağlanılamadı", null);
            }
        }
        public CompanyApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CompanyApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }

        // token opsiyonel — login sayfasında token yok ama company çekiyoruz
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

        public async Task<CompanyDto?> GetCompanyAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/company", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetCompanyAsync başarısız: {StatusCode}", response.StatusCode);
                    return null;
                }
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<CompanyDto>? result = JsonSerializer.Deserialize<ApiResponse<CompanyDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCompanyAsync");
                return null;
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors)> UpdateCompanyAsync(CompanyDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Put, "/api/company", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Başarıyla güncellendi", null);
                _logger.LogWarning("UpdateCompany başarısız: {Errors}", result?.Errors);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: UpdateCompanyAsync");
                return (false, "API'ye bağlanılamadı", null);
            }
        }
    }
}