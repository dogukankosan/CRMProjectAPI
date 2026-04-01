using System.Net.Http.Headers;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class ErrorLogApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly ILogger<ErrorLogApiService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ErrorLogApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ErrorLogApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ApiSettings:BaseUrl"]!;
            _apiKey = configuration["ApiSettings:ApiKey"]!;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, string? token)
        {
            var req = new HttpRequestMessage(method, $"{_baseUrl}{endpoint}");
            req.Headers.Add("X-API-Key", _apiKey);
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return req;
        }

        public async Task<(int Total, int Pages, List<dynamic> Data)> GetLogsAsync(
            string? type, bool? onlyErrors, string? startDate, string? endDate,
            int page, int pageSize, string? token)
        {
            try
            {
                var qs = new List<string>();
                if (!string.IsNullOrEmpty(type)) qs.Add($"type={type}");
                if (onlyErrors == true) qs.Add("onlyErrors=true");
                if (!string.IsNullOrEmpty(startDate)) qs.Add($"startDate={startDate}");
                if (!string.IsNullOrEmpty(endDate)) qs.Add($"endDate={endDate}");
                qs.Add($"page={page}");
                qs.Add($"pageSize={pageSize}");

                string endpoint = "/api/log/list?" + string.Join("&", qs);
                var req = CreateRequest(HttpMethod.Get, endpoint, token);
                var res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode) return (0, 0, new());

                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                int total = data.GetProperty("total").GetInt32();
                int pages = data.GetProperty("pages").GetInt32();
                var rows = JsonSerializer.Deserialize<List<dynamic>>(
                    data.GetProperty("data").GetRawText(), JsonOptions) ?? new();

                return (total, pages, rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLogsAsync hatası");
                return (0, 0, new());
            }
        }

        public async Task<string?> GetLogDetailAsync(long id, string? token)
        {
            try
            {
                var req = CreateRequest(HttpMethod.Get, $"/api/log/detail/{id}", token);

                var res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode) return null;
                return await res.Content.ReadAsStringAsync();
            }
            catch { return null; }
        }

        public async Task<bool> DeleteLogAsync(long id, string? token)
        {
            try
            {
                var req = CreateRequest(HttpMethod.Delete, $"/api/log/delete/{id}", token);
                var res = await _httpClient.SendAsync(req);

                // Geçici debug
                var body = await res.Content.ReadAsStringAsync();
                _logger.LogWarning("DeleteLog ID:{id} StatusCode:{code} Body:{body}", id, res.StatusCode, body);

                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteLogAsync exception");
                return false;
            }
        }

        public async Task<(bool Success, string Message)> ClearLogsAsync(
            string? type, string? beforeDate, string? token)
        {
            try
            {
                var qs = new List<string>();
                if (!string.IsNullOrEmpty(type)) qs.Add($"type={type}");
                if (!string.IsNullOrEmpty(beforeDate)) qs.Add($"beforeDate={beforeDate}");

                string endpoint = "/api/log/clear" + (qs.Any() ? "?" + string.Join("&", qs) : "");
                var req = CreateRequest(HttpMethod.Delete, endpoint, token);
                var res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                string msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                return (res.IsSuccessStatusCode, msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClearLogsAsync hatası");
                return (false, "Bağlantı hatası");
            }
        }
    }
}