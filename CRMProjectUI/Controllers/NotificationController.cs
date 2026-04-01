using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CRMProjectUI.Controllers
{
    [Authorize]
    [Route("Notification")]
    public class NotificationController : Controller
    {
        private readonly TicketApiService _ticketService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly string _apiBase;

        private string? Token => User.FindFirst("JwtToken")?.Value;

        public NotificationController(
            TicketApiService ticketService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _ticketService = ticketService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _apiBase = configuration["ApiSettings:BaseUrl"] ?? "";
        }

        [HttpGet("MyNotifications")]
        public async Task<IActionResult> MyNotifications()
        {
            var data = await _ticketService.GetMyNotificationsAsync(Token);
            return Json(data);
        }

        [HttpGet("CompanyNotifications")]
        public async Task<IActionResult> CompanyNotifications()
        {
            var data = await _ticketService.GetCompanyNotificationsAsync(Token);
            return Json(data);
        }

        [HttpGet("Announcements")]
        public async Task<IActionResult> Announcements()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Token);
                client.DefaultRequestHeaders.Add("X-API-Key",
                    _configuration["ApiSettings:ApiKey"]);

                var response = await client.GetAsync($"{_apiBase}/api/announcement");
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<List<AnnouncementDto>>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return Json(result?.Data ?? new List<AnnouncementDto>());
            }
            catch
            {
                return Json(new List<AnnouncementDto>());
            }
        }

        [HttpPost("DuyuruKapat/{id:int}")]
        public async Task<IActionResult> DuyuruKapat(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Token);
                client.DefaultRequestHeaders.Add("X-API-Key",
                    _configuration["ApiSettings:ApiKey"]);

                await client.PostAsync(
                    $"{_apiBase}/api/announcement/{id}/dismiss", null);

                return Ok();
            }
            catch
            {
                return Ok();
            }
        }
    }
}