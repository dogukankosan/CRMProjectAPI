using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class CustomerApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<CustomerApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public CustomerApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CustomerApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }
        public async Task<bool> IsCustomerActiveAsync(int customerId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Get, $"/api/customer/{customerId}/is-active", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return true;
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<bool>? result = JsonSerializer.Deserialize<ApiResponse<bool>>(json, JsonOptions);
                return result?.Data ?? true;
            }
            catch { return true; }
        }
        public async Task<CustomerDetailDto?> GetCustomerDetailAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/customer/{id}/detail", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<CustomerDetailDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<CustomerDetailDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetCustomerDetailAsync hatası. ID: {ID}", id);
                return null;
            }
        }
        public async Task<(bool Success, string Message, string? Path)> UploadContractAsync(
            int customerId, Stream fileStream, string fileName, string? token = null)
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
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                content.Add(streamContent, "file", fileName);

                HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post, $"{_baseUrl}/api/customer/{customerId}/contract");
                request.Headers.Add("X-API-Key", _apiKey);
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<string>? result = JsonSerializer.Deserialize<ApiResponse<string>>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Sözleşme yüklendi", result?.Data);
                return (false, result?.Message ?? "Sözleşme yüklenemedi", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Contract upload hatası: {ID}", customerId);
                return (false, "API'ye bağlanılamadı", null);
            }
        }
        #region Helper

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

        #endregion

        #region Customer CRUD

        public async Task<bool> CheckCustomerCodeExistsAsync(string code, int excludeId = 0, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/customer/check-code?code={Uri.EscapeDataString(code)}&excludeId={excludeId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<bool>? result = JsonSerializer.Deserialize<ApiResponse<bool>>(json, JsonOptions);
                return result?.Data ?? false;
            }
            catch { return false; }
        }

        public async Task<List<CustomerListDto>> GetCustomersAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/customer", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CustomerListDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<CustomerListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CustomerListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCustomersAsync");
                throw;
            }
        }

        public async Task<List<CustomerListDto>> GetActiveCustomersAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/customer/active", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CustomerListDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<CustomerListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CustomerListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetActiveCustomersAsync");
                throw;
            }
        }

        public async Task<List<CustomerSelectDto>> GetCustomerSelectListAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/customer/select", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CustomerSelectDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<CustomerSelectDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CustomerSelectDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCustomerSelectListAsync");
                throw;
            }
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/customer/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetCustomerByIdAsync başarısız: {StatusCode}", response.StatusCode);
                    return null;
                }
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<CustomerDto>? result = JsonSerializer.Deserialize<ApiResponse<CustomerDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCustomerByIdAsync({ID})", id);
                return null;
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors, int? NewId)> CreateCustomerAsync(CustomerDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/customer", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<int>? result = JsonSerializer.Deserialize<ApiResponse<int>>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Müşteri başarıyla eklendi", null, result?.Data);
                _logger.LogWarning("CreateCustomer başarısız: {Errors}", result?.Errors);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: CreateCustomerAsync");
                return (false, "API'ye bağlanılamadı", null, null);
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors)> UpdateCustomerAsync(int id, CustomerDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Put, $"/api/customer/{id}", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
          ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Müşteri başarıyla güncellendi", null);
                _logger.LogWarning("UpdateCustomer başarısız: {Errors}", result?.Errors);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: UpdateCustomerAsync({ID})", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteCustomerAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Delete, $"/api/customer/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Müşteri başarıyla silindi");
                return (false, result?.Message ?? "Bir hata oluştu");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: DeleteCustomerAsync({ID})", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        public async Task<(bool Success, string Message, byte? NewStatus)> ToggleCustomerStatusAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Patch, $"/api/customer/{id}/status", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<byte>? result = JsonSerializer.Deserialize<ApiResponse<byte>>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Durum değiştirildi", result?.Data);
                return (false, result?.Message ?? "Bir hata oluştu", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: ToggleCustomerStatusAsync({ID})", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        #endregion

        #region Logo Products

        public async Task<List<LogoProductDto>> GetLogoProductsAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/customer/logo-products", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<LogoProductDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<LogoProductDto>>>(json, JsonOptions);
                return result?.Data ?? new List<LogoProductDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetLogoProductsAsync");
                throw;
            }
        }
        public async Task<List<LogoProductDto>> GetCustomerLogoProductsAsync(int customerId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/customer/{customerId}/logo-products", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CustomerLogoProductDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<CustomerLogoProductDto>>>(json, JsonOptions);

                return result?.Data?.Select(x => new LogoProductDto
                {
                    ID = x.LogoProductID,
                    LogoProductName = x.LogoProductName ?? string.Empty
                }).ToList() ?? new List<LogoProductDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCustomerLogoProductsAsync({ID})", customerId);
                return new List<LogoProductDto>();
            }
        }

        #endregion

        #region Customer Files

        public async Task<List<CustomerFileDto>> GetCustomerFilesAsync(int customerId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/customer/{customerId}/files", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CustomerFileDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<CustomerFileDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CustomerFileDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCustomerFilesAsync({ID})", customerId);
                throw;
            }
        }

        public async Task<(bool Success, string Message, int? FileId)> UploadFileAsync(
            int customerId,
            Stream fileStream,
            string fileName,
            string? description = null,
            string category = "Genel",
            string? tags = null,
            string? token = null)
        {
            try
            {
                MultipartFormDataContent content = new MultipartFormDataContent();
                StreamContent streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));
                content.Add(streamContent, "file", fileName);
                if (!string.IsNullOrEmpty(description))
                    content.Add(new StringContent(description), "description");
                content.Add(new StringContent(category), "category");
                if (!string.IsNullOrEmpty(tags))
                    content.Add(new StringContent(tags), "tags");

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/customer/{customerId}/files");
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
                _logger.LogError(ex, "API isteği başarısız: UploadFileAsync({ID})", customerId);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteFileAsync(int fileId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Delete, $"/api/customer/files/{fileId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
          ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Dosya silindi");
                return (false, result?.Message ?? "Dosya silinemedi");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: DeleteFileAsync({ID})", fileId);
                return (false, "API'ye bağlanılamadı");
            }
        }

        private static string GetMimeType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }

        #endregion

        #region Location

        public async Task<List<CitySelectDto>> GetCitiesAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/customer/cities", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<CitySelectDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<CitySelectDto>>>(json, JsonOptions);
                return result?.Data ?? new List<CitySelectDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetCitiesAsync");
                throw;
            }
        }
        public async Task<System.Text.Json.JsonElement?> GetTicketEligibilityAsync(int customerId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Get, $"/api/customer/{customerId}/ticket-eligibility", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("data", out JsonElement data))
                    return data.Clone(); // Clone — using bloğu kapanınca dispose olmasın
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTicketEligibilityAsync hatası");
                return null;
            }
        }
        public async Task<List<DistrictSelectDto>> GetDistrictsAsync(string il, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/customer/districts/{Uri.EscapeDataString(il)}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<DistrictSelectDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<DistrictSelectDto>>>(json, JsonOptions);
                return result?.Data ?? new List<DistrictSelectDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetDistrictsAsync({Il})", il);
                throw;
            }
        }

        #endregion
    }
}