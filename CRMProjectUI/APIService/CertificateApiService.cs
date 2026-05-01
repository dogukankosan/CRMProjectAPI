using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class CertificateApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<CertificateApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CertificateApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CertificateApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }
        private HttpRequestMessage CreateRequest(
            HttpMethod method, string endpoint,
            object? body = null, string? token = null)
        {
            HttpRequestMessage request = new(method, $"{_baseUrl}{endpoint}");
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
        public async Task<List<CertificateDto>> GetListAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/certificate", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CertificateDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<CertificateDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CertificateDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetListAsync hatası");
                return new List<CertificateDto>();
            }
        }
        public async Task<List<CertificateDto>> GetByUserAsync(int userId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/certificate/user/{userId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CertificateDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<CertificateDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CertificateDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetByUserAsync hatası. UserID: {UserID}", userId);
                return new List<CertificateDto>();
            }
        }
        public async Task<CertificateDto?> GetByIdAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/certificate/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<CertificateDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<CertificateDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetByIdAsync hatası. ID: {ID}", id);
                return null;
            }
        }
        public async Task<(bool Success, string Message, List<string>? Errors, int? NewId)> CreateAsync(
            CertificateCreateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/certificate", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<int>? result = JsonSerializer.Deserialize<ApiResponse<int>>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Sertifika eklendi", null, result?.Data);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateAsync hatası");
                return (false, "API'ye bağlanılamadı", null, null);
            }
        }
        public async Task<(bool Success, string Message, List<string>? Errors)> UpdateAsync(
            int id, CertificateUpdateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Put,
                    $"/api/certificate/{id}", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Sertifika güncellendi", null);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }
        public async Task<(bool Success, string Message)> DeleteAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Delete,
                    $"/api/certificate/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Sertifika silindi");
                return (false, result?.Message ?? "Bir hata oluştu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı");
            }
        }
        public async Task<(bool Success, byte[]? Bytes, string? FileName)> DownloadAsync(
            int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/certificate/{id}/download", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return (false, null, null);
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                string fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? "sertifika.pdf";
                return (true, bytes, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DownloadAsync hatası. ID: {ID}", id);
                return (false, null, null);
            }
        }
    }
}