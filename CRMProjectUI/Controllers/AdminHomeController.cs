    using CRMProjectUI.APIService;
    using CRMProjectUI.Models;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;

    namespace CRMProjectUI.Controllers
    {
        [Authorize]
        public class AdminHomeController : Controller
        {
            private readonly TicketApiService _ticketService;
            private readonly ILogger<AdminHomeController> _logger;

            private string? Token => User.FindFirst("JwtToken")?.Value;
            private bool IsAdmin => int.TryParse(User.FindFirst("IsAdmin")?.Value, out int v) && v >= 1;
            private bool IsSuperAdmin => User.FindFirst("IsAdmin")?.Value == "2";
            private int CompanyId => int.TryParse(User.FindFirst("CompanyId")?.Value, out int id) ? id : 0;
            private int UserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;

            public AdminHomeController(
                TicketApiService ticketService,
                ILogger<AdminHomeController> logger)
            {
                _ticketService = ticketService;
                _logger = logger;
            }

        public async Task<IActionResult> Index()
        {
            if (IsSuperAdmin)
            {
                try
                {
                    SuperAdminDashboardDto? dashboard = await _ticketService.GetSuperAdminDashboardAsync(Token);
                    return View("SuperAdminDashboard", dashboard);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SuperAdmin dashboard yüklenirken hata");
                    return View("SuperAdminDashboard", (SuperAdminDashboardDto?)null);
                }
            }
            else if (IsAdmin)
            {
                try
                {
                    AdminPersonalDashboardDto? dashboard = await _ticketService.GetAdminDashboardAsync(Token);
                    return View("AdminDashboard", dashboard);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Admin dashboard yüklenirken hata");
                    return View("AdminDashboard", (AdminPersonalDashboardDto?)null);
                }
            }
            else
            {
                try
                {
                    UserPersonalDashboardDto? dashboard = await _ticketService.GetUserDashboardAsync(Token);
                    return View("UserDashboard", dashboard);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "User dashboard yüklenirken hata");
                    return View("UserDashboard", (UserPersonalDashboardDto?)null);
                }
            }
        }
    }
    }