using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class AuthApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<AuthApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AuthApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AuthApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }

        /// <summary>
        /// Login — username + password → token + kullanıcı bilgisi
        /// </summary>
        public async Task<(bool Success, string Message, LoginResponseDto? Data)> LoginAsync(string username, string password)
        {
            try
            {
                // Login endpoint API key gerektirmez
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/auth/login");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string json = JsonSerializer.Serialize(new { username, password }, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseJson = await response.Content.ReadAsStringAsync();

                ApiResponse<LoginResponseDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<LoginResponseDto>>(responseJson, JsonOptions);

                if (response.IsSuccessStatusCode && result?.Data != null)
                    return (true, result.Message ?? "Giriş başarılı", result.Data);

                return (false, result?.Message ?? "Kullanıcı adı veya şifre hatalı", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API bağlantı hatası: LoginAsync");
                return (false, "Sunucuya bağlanılamadı. Lütfen daha sonra tekrar deneyin.", null);
            }
        }

        /// <summary>
        /// Mevcut kullanıcı bilgisi — token ile
        /// </summary>
        public async Task<UserDto?> GetMeAsync(string token)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/auth/me");
                request.Headers.Add("X-API-Key", _apiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<UserDto>? result = JsonSerializer.Deserialize<ApiResponse<UserDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API bağlantı hatası: GetMeAsync");
                return null;
            }
        }

        /// <summary>
        /// Şifre değiştir
        /// </summary>
        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            string token, string currentPassword, string newPassword, string confirmPassword)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/auth/change-password");
                request.Headers.Add("X-API-Key", _apiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string json = JsonSerializer.Serialize(new
                {
                    currentPassword,
                    newPassword,
                    confirmPassword
                }, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseJson = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(responseJson, JsonOptions);


                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Şifre değiştirildi");

                return (false, result?.Message ?? "Şifre değiştirilemedi");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API bağlantı hatası: ChangePasswordAsync");
                return (false, "Sunucuya bağlanılamadı");
            }
        }
    }

    // ==================== AUTH MODEL'LERİ ====================

    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public byte IsAdmin { get; set; }
        public int CompanyId { get; set; }
        public string? Picture { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}