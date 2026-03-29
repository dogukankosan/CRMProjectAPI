using CRMProjectUI.APIService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    [Authorize]
    [Route("Notification")]
    public class NotificationController : Controller
    {
        private readonly TicketApiService _ticketService;
        private string? Token => User.FindFirst("JwtToken")?.Value;

        public NotificationController(TicketApiService ticketService)
        {
            _ticketService = ticketService;
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
    }
}