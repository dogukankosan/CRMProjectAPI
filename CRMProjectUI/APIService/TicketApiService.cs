using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class TicketApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<TicketApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public TicketApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<TicketApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl tanımlı değil");
            _apiKey = configuration["ApiSettings:ApiKey"]
                ?? throw new InvalidOperationException("ApiSettings:ApiKey tanımlı değil");
        }
        public async Task<object?> GetAdminReportAsync(string? token = null)
        {
            try
            {
                var request = CreateRequest(HttpMethod.Get, "/api/ticket/admin-report", token: token);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(json, JsonOptions);
            }
            catch { return null; }
        }
        public async Task<List<TicketListDto>> SearchTicketsAsync(
    string? search = null,
    int? status = null,
    int? priority = null,
    int? customerId = null,
    int? logoProductId = null,
    int? assignedToUserId = null,
    int? createdByUserId = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    string? token = null)
        {
            try
            {
                var query = new List<string>();
                if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
                if (status.HasValue) query.Add($"status={status}");
                if (priority.HasValue) query.Add($"priority={priority}");
                if (customerId.HasValue) query.Add($"customerId={customerId}");
                if (logoProductId.HasValue) query.Add($"logoProductId={logoProductId}");
                if (assignedToUserId.HasValue) query.Add($"assignedToUserId={assignedToUserId}");
                if (createdByUserId.HasValue) query.Add($"createdByUserId={createdByUserId}");
                if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");

                string url = "/api/ticket/search";
                if (query.Any()) url += "?" + string.Join("&", query);

                HttpRequestMessage request = CreateRequest(HttpMethod.Get, url, token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new List<TicketListDto>();
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<TicketListDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<TicketListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<TicketListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "SearchTicketsAsync hatası");
                return new List<TicketListDto>();
            }
        }
        public async Task<MyNotificationsDto?> GetMyNotificationsAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/ticket/my-notifications", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<MyNotificationsDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<MyNotificationsDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetMyNotificationsAsync hatası");
                return null;
            }
        }
        public async Task<string?> GetSuperAdminReportAsync(string? token = null)
        {
            try
            {
                var request = CreateRequest(HttpMethod.Get, "/api/ticket/superadmin-report", token: token);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStringAsync();
            }
            catch { return null; }
        }
        public async Task<CompanyNotificationsDto?> GetCompanyNotificationsAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/ticket/company-notifications", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<CompanyNotificationsDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<CompanyNotificationsDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetCompanyNotificationsAsync hatası");
                return null;
            }
        }
        // ── Yardımcı ────────────────────────────────────────────────────────
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
        #region Ticket CRUD

        /// <summary>
        /// Tüm ticket listesi (Admin/SuperAdmin) veya firma bazlı (User)
        /// </summary>
        public async Task<List<TicketListDto>> GetTicketsAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/ticket", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new List<TicketListDto>();

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<TicketListDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<TicketListDto>>>(json, JsonOptions);
                return result?.Data ?? new List<TicketListDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetTicketsAsync hatası");
                return new List<TicketListDto>();
            }
        }

        /// <summary>
        /// Ticket detay
        /// </summary>
        public async Task<TicketDto?> GetTicketByIdAsync(int id, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"/api/ticket/{id}", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<TicketDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<TicketDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetTicketByIdAsync hatası. ID: {ID}", id);
                return null;
            }
        }

        /// <summary>
        /// Ticket oluştur
        /// </summary>
        public async Task<(bool Success, string Message, List<string>? Errors, int? NewId)> CreateTicketAsync(
            TicketCreateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/ticket", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<int>? result = JsonSerializer.Deserialize<ApiResponse<int>>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Ticket oluşturuldu", null, result?.Data);

                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "CreateTicketAsync hatası");
                return (false, "API'ye bağlanılamadı", null, null);
            }
        }

        /// <summary>
        /// Ticket durum güncelle — Admin/SuperAdmin
        /// </summary>
        public async Task<(bool Success, string Message, List<string>? Errors)> UpdateStatusAsync(
            int id, TicketStatusUpdateDto dto, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Patch, $"/api/ticket/{id}/status", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Durum güncellendi", null);

                return (false, result?.Message ?? "Bir hata oluştu", result?.Errors);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "UpdateStatusAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        /// <summary>
        /// Ticket devret — Admin/SuperAdmin
        /// </summary>
        public async Task<(bool Success, string Message)> AssignTicketAsync(
         int id, int assignedToUserId, string? token = null)
        {
            try
            {
                var dto = new { AssignedToUserID = assignedToUserId };
                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Patch, $"/api/ticket/{id}/assign", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                // Errors string veya List<string> gelebilir, güvenli parse
                string message = "Bir hata oluştu";
                try
                {
                    ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);
                    message = result?.Message ?? message;
                    if (response.IsSuccessStatusCode)
                        return (true, message);
                }
                catch { }

                return (false, message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AssignTicketAsync hatası. ID: {ID}", id);
                return (false, "API'ye bağlanılamadı");
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ticket Files

        /// <summary>
        /// Ticket dosya yükle
        /// </summary>
        public async Task<(bool Success, string Message, int? FileId)> UploadFileAsync(
            int ticketId, Stream fileStream, string fileName, string? token = null)
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
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".txt" => "text/plain",
                    _ => "application/octet-stream"
                };

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                content.Add(streamContent, "file", fileName);

                HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post, $"{_baseUrl}/api/ticket/{ticketId}/files");
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
                _logger.LogError(ex, "UploadFileAsync hatası. TicketID: {ID}", ticketId);
                return (false, "API'ye bağlanılamadı", null);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ticket Comments

        /// <summary>
        /// Yorum ekle
        /// </summary>
        public async Task<(bool Success, string Message)> AddCommentAsync(
            int ticketId, string comment, int userId, string? token = null)
        {
            try
            {
                var dto = new TicketCommentCreateDto
                {
                    TicketID = ticketId,
                    UserID = userId,
                    Comment = comment
                };

                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Post, $"/api/ticket/{ticketId}/comments", dto, token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                ApiResponse? result = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode)
                    return (true, result?.Message ?? "Yorum eklendi");

                return (false, result?.Message ?? "Yorum eklenemedi");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AddCommentAsync hatası. TicketID: {ID}", ticketId);
                return (false, "API'ye bağlanılamadı");
            }
        }

        /// <summary>
        /// Yorumları getir
        /// </summary>
        public async Task<List<TicketCommentDto>> GetCommentsAsync(int ticketId, string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(
                    HttpMethod.Get, $"/api/ticket/{ticketId}/comments", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new List<TicketCommentDto>();

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<List<TicketCommentDto>>? result =
                    JsonSerializer.Deserialize<ApiResponse<List<TicketCommentDto>>>(json, JsonOptions);
                return result?.Data ?? new List<TicketCommentDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetCommentsAsync hatası. TicketID: {ID}", ticketId);
                return new List<TicketCommentDto>();
            }
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// SuperAdmin dashboard — genel istatistikler
        /// </summary>
        public async Task<SuperAdminDashboardDto?> GetSuperAdminDashboardAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/ticket/superadmin-dashboard", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<SuperAdminDashboardDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<SuperAdminDashboardDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetSuperAdminDashboardAsync hatası");
                return null;
            }
        }

        /// <summary>
        /// Admin dashboard — kişisel performans
        /// </summary>
        public async Task<AdminPersonalDashboardDto?> GetAdminDashboardAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/ticket/admin-dashboard", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<AdminPersonalDashboardDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<AdminPersonalDashboardDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetAdminDashboardAsync hatası");
                return null;
            }
        }

        /// <summary>
        /// User dashboard — firma + kişisel
        /// </summary>
        public async Task<UserPersonalDashboardDto?> GetUserDashboardAsync(string? token = null)
        {
            try
            {
                HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/ticket/user-dashboard", token: token);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                ApiResponse<UserPersonalDashboardDto>? result =
                    JsonSerializer.Deserialize<ApiResponse<UserPersonalDashboardDto>>(json, JsonOptions);
                return result?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GetUserDashboardAsync hatası");
                return null;
            }
        }

        #endregion
    }
}