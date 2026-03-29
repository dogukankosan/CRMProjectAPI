using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class KnowledgeBaseApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<KnowledgeBaseApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public KnowledgeBaseApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<KnowledgeBaseApiService> logger)
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

        // ────────────────────────────────────────────────────────────────────
        #region Liste

        public async Task<List<KnowledgeBaseListDto>> GetListAsync(
            string? search = null,
            short? logoProduct = null,
            string? category = null,
            string? token = null)
        {
            try
            {
                string endpoint = "/api/knowledgebase";
                List<string> queryParams = new();
                if (!string.IsNullOrEmpty(search))
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                if (logoProduct.HasValue)
                    queryParams.Add($"logoProduct={logoProduct}");
                if (!string.IsNullOrEmpty(category))
                    queryParams.Add($"category={Uri.EscapeDataString(category)}");
                if (queryParams.Any())
                    endpoint += "?" + string.Join("&", queryParams);

                HttpRequestMessage request = CreateRequest(HttpMethod.Get, endpoint, token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new List<KnowledgeBaseListDto>();

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<KnowledgeBaseListDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<KnowledgeBaseListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<KnowledgeBaseListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetListAsync hatası");
                return new List<KnowledgeBaseListDto>();
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Detay

        public async Task<KnowledgeBaseDto?> GetByIdAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/knowledgebase/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<KnowledgeBaseDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<KnowledgeBaseDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetByIdAsync hatası. ID: {ID}", id);
                return null;
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region CRUD

        public async Task<(bool Success, string Message, List<string>? Errors, int? NewId)> CreateAsync(
            KnowledgeBaseCreateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/knowledgebase", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<int>? result = JsonSerializer.Deserialize<ApiResponse<int>>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Makale oluşturuldu", null, result?.Data);

                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "CreateAsync hatası");
                return (false, "API'ye bağlanılamadı", null, null);
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors)> UpdateAsync(
            int id, KnowledgeBaseCreateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Put, $"/api/knowledgebase/{id}", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Makale güncellendi", null);

                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "UpdateAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Delete, $"/api/knowledgebase/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Makale silindi");

                return (false, result?.Message ?? "Bir hata oluştu");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "DeleteAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        public async Task<(bool Success, string Message)> ToggleActiveAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Patch, $"/api/knowledgebase/{id}/toggle", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Durum değiştirildi");

                return (false, result?.Message ?? "Bir hata oluştu");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "ToggleActiveAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        public async Task<(bool Success, string Message)> TogglePublicAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Patch, $"/api/knowledgebase/{id}/toggle-public", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Görünürlük değiştirildi");

                return (false, result?.Message ?? "Bir hata oluştu");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "TogglePublicAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Dosyalar

        public async Task<(bool Success, string Message, int? FileId)> UploadFileAsync(
            int kbId, Stream fileStream, string fileName, string? token = null)
        {
            try
            {
                MultipartFormDataContent content = new MultipartFormDataContent();
                StreamContent streamContent = new StreamContent(fileStream);
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                string mime = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".doc" => "application/msword",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".xls" => "application/vnd.ms-excel",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".txt" => "text/plain",
                    ".zip" => "application/zip",
                    _ => "application/octet-stream"
                };
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                content.Add(streamContent, "file", fileName);

                HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post, $"{_baseUrl}/api/knowledgebase/{kbId}/files");
                request.Headers.Add("X-API-Key", _apiKey);
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<int>? result = JsonSerializer.Deserialize<ApiResponse<int>>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Dosya yüklendi", result?.Data);

                return (false, result?.Message ?? "Dosya yüklenemedi", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "UploadFileAsync hatası. KbID: {ID}", kbId);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteFileAsync(int fileId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Delete, $"/api/knowledgebase/files/{fileId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Dosya silindi");

                return (false, result?.Message ?? "Dosya silinemedi");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "DeleteFileAsync hatası. FileID: {ID}", fileId);
                return (false, "API'ye bağlanılamadı");
            }
        }

        #endregion
    }
}