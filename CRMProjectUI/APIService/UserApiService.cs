using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class UserApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<UserApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public UserApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<UserApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }
        public async Task<bool> IsUserActiveAsync(int userId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Get, $"/api/user/{userId}/is-active", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return true; // hata varsa düşürme
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<bool>? result = JsonSerializer.Deserialize<ApiResponse<bool>>(json, JsonOptions);
                return result?.Data ?? true;
            }
            catch
            {
                return true; // API erişilemiyorsa düşürme
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

        #region User CRUD

        public async Task<List<UserListDto>> GetUsersAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/user", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<UserListDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<UserListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<UserListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetUsersAsync");
                throw;
            }
        }

        public async Task<List<UserListDto>> GetUsersByCustomerAsync(int customerId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/user/by-customer/{customerId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<UserListDto>>? result = JsonSerializer.Deserialize<ApiResponse<List<UserListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<UserListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetUsersByCustomerAsync({ID})", customerId);
                return new List<UserListDto>();
            }
        }

        /// <summary>
        /// Sadece Admin ve SuperAdmin rolündeki kullanıcıları getirir.
        /// Ticket devir modalında kullanılır.
        /// ISAdmin >= 1 olanlar → Admin (1) ve SuperAdmin (2)
        /// </summary>
        public async Task<List<UserListDto>> GetAdminUsersAsync(string? token = null)
        {
            try
            {
                // API'de admin kullanıcıları filtreleyen özel bir endpoint varsa onu kullan,
                // yoksa tüm kullanıcıları çekip client tarafında filtrele
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/user/admins", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    List<UserListDto> all = await GetUsersAsync(token);
                    return all.Where(u => u.ISAdmin >= 1 && u.Status).ToList();
                }
                if (!response.IsSuccessStatusCode)
                    return new List<UserListDto>();

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<UserListDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<UserListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<UserListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetAdminUsersAsync");
                return new List<UserListDto>();
            }
        }

        public async Task<UserDto?> GetUserByIdAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/user/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetUserByIdAsync başarısız: {StatusCode}", response.StatusCode);
                    return null;
                }
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<UserDto>? result = JsonSerializer.Deserialize<ApiResponse<UserDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: GetUserByIdAsync({ID})", id);
                return null;
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors, int? NewId)> CreateUserAsync(UserCreateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/user", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<int>? result = JsonSerializer.Deserialize<ApiResponse<int>>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Kullanıcı başarıyla oluşturuldu", null, result?.Data);
                _logger.LogWarning("CreateUser başarısız: {Errors}", result?.Errors);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: CreateUserAsync");
                return (false, "API'ye bağlanılamadı", null, null);
            }
        }

        public async Task<(bool Success, string Message, List<string>? Errors)> UpdateUserAsync(int id, UserUpdateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Put, $"/api/user/{id}", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Kullanıcı başarıyla güncellendi", null);
                _logger.LogWarning("UpdateUser başarısız: {Errors}", result?.Errors);
                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: UpdateUserAsync({ID})", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Delete, $"/api/user/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Kullanıcı başarıyla silindi");
                return (false, result?.Message ?? "Bir hata oluştu");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: DeleteUserAsync({ID})", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        public async Task<(bool Success, string Message, bool? NewStatus)> ToggleUserStatusAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Patch, $"/api/user/{id}/status", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<bool>? result = JsonSerializer.Deserialize<ApiResponse<bool>>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Durum değiştirildi", result?.Data);
                return (false, result?.Message ?? "Bir hata oluştu", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: ToggleUserStatusAsync({ID})", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        #endregion

        #region Resim

        public async Task<(bool Success, string Message, string? PicturePath)> UploadPictureAsync(int id, Stream fileStream, string fileName, string? token = null)
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
                    _ => "application/octet-stream"
                };
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                content.Add(streamContent, "file", fileName);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/user/{id}/picture");
                request.Headers.Add("X-API-Key", _apiKey);
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<string>? result = JsonSerializer.Deserialize<ApiResponse<string>>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Resim yüklendi", result?.Data);
                return (false, result?.Message ?? "Resim yüklenemedi", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: UploadPictureAsync({ID})", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        public async Task<(bool Success, string Message)> DeletePictureAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Delete, $"/api/user/{id}/picture", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Resim silindi");
                return (false, result?.Message ?? "Resim silinemedi");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API isteği başarısız: DeletePictureAsync({ID})", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        #endregion

        #region Kontrol

        public async Task<bool> CheckUsernameExistsAsync(string username, int excludeId = 0, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/user/check-username?username={Uri.EscapeDataString(username)}&excludeId={excludeId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<bool>? result = JsonSerializer.Deserialize<ApiResponse<bool>>(json, JsonOptions);
                return result?.Data ?? false;
            }
            catch { return false; }
        }

        public async Task<bool> CheckEmailExistsAsync(string email, int excludeId = 0, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get,
                    $"/api/user/check-email?email={Uri.EscapeDataString(email)}&excludeId={excludeId}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<bool>? result = JsonSerializer.Deserialize<ApiResponse<bool>>(json, JsonOptions);
                return result?.Data ?? false;
            }
            catch { return false; }
        }

        #endregion
    }
}