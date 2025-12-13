using CRMProjectUI.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CRMProjectUI.APIService
{
    public class CompanyApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        public CompanyApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }
        /// <summary>
        /// API'den firma bilgilerini getir (tek kayıt)
        /// </summary>
        public async Task<List<CompanyDto>> GetCompanyAsync()
        {
            string baseUrl = _configuration["ApiSettings:BaseUrl"]!;
            string apiKey = _configuration["ApiSettings:ApiKey"]!;
            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/api/company"
            );
            request.Headers.Add("X-API-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string? json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<List<CompanyDto>>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            return result?.Data ?? new List<CompanyDto>();
        }
    }
}