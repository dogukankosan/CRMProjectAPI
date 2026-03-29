using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRMProjectUI.Controllers
{
    [Authorize]
    [Route("AdminTicket")]
    public class AdminTicketController : Controller
    {
        private readonly TicketApiService _ticketService;
        private readonly CustomerApiService _customerService;
        private readonly UserApiService _userService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminTicketController> _logger;
        private string? Token => User.FindFirst("JwtToken")?.Value;
        private int CallerUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int v) ? v : 0;
        private int CallerCompanyId => int.TryParse(User.FindFirst("CompanyId")?.Value, out int v) ? v : 0;
        private bool IsSuperAdmin => User.IsInRole("SuperAdmin");
        private bool IsAdmin => User.IsInRole("Admin");
        private bool IsUser => User.IsInRole("User");

        public AdminTicketController(
            TicketApiService ticketService,
            CustomerApiService customerService,
            UserApiService userService,
            IConfiguration configuration,
            ILogger<AdminTicketController> logger)
        {
            _ticketService = ticketService;
            _customerService = customerService;
            _userService = userService;
            _configuration = configuration;
            _logger = logger;
        }
        [HttpGet("CheckEligibility")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CheckEligibility(int customerId)
        {
            var eligibility = await _customerService.GetTicketEligibilityAsync(customerId, Token);
            if (!eligibility.HasValue) return Json(new { isContractExpired = false, isTicketExhausted = false, ticketCount = 0, daysLeft = 999 });

            var el = eligibility.Value;
            return Json(new
            {
                isContractExpired = el.TryGetProperty("IsContractExpired", out var p1) ? p1.GetInt32() == 1 : false,
                isTicketExhausted = el.TryGetProperty("IsTicketExhausted", out var p2) ? p2.GetInt32() == 1 : false,
                ticketCount = el.TryGetProperty("TicketCount", out var p3) ? p3.GetInt32() : 0,
                daysLeft = el.TryGetProperty("DaysLeft", out var p4) ? p4.GetInt32() : 999
            });
        }
        [HttpGet("UserRapor")]
        [Authorize(Roles = "User")]
        public IActionResult UserRapor()
        {
            ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"] ?? "";
            return View("UserReport");
        }

        [HttpGet("UserReportData")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UserReportData()
        {
            var result = await _ticketService.GetUserDashboardAsync(Token);
            return Json(result);
        }
        [HttpGet("SuperAdminReport")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SuperAdminReportData()
        {
            var result = await _ticketService.GetSuperAdminReportAsync(Token);
            if (result == null) return Json(new { data = (object?)null });
            return Content(result, "application/json");
        }
        [HttpPost("DosyaYukle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DosyaYukle(int id, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                await using Stream stream = file.OpenReadStream();
                var result = await _ticketService.UploadFileAsync(id, stream, file.FileName, Token);
                if (result.Success)
                    TempData["Success"] = "Dosya yüklendi";
                else
                    TempData["Error"] = result.Message;
            }
            return RedirectToAction(nameof(Detay), new { id });
        }
        [HttpGet("TumTalepler")]
        public async Task<IActionResult> TumTalepler(
    string? search = null,
    int? status = null,
    int? priority = null,
    int? customerId = null,
    int? logoProductId = null,
    int? assignedToUserId = null,
    int? createdByUserId = null,
    DateTime? startDate = null,
    DateTime? endDate = null)
        {
            try
            {
                var tickets = await _ticketService.SearchTicketsAsync(
                    search, status, priority, customerId, logoProductId,
                    assignedToUserId, createdByUserId, startDate, endDate, Token);

                // Filtre dropdownları için
                if (IsSuperAdmin || IsAdmin)
                {
                    ViewBag.AdminUsers = await _userService.GetAdminUsersAsync(Token);
                    ViewBag.Customers = await _customerService.GetActiveCustomersAsync(Token);
                }
                var logoProducts = await _customerService.GetLogoProductsAsync(Token);
                ViewBag.LogoProducts = logoProducts;

                // Mevcut filtre değerlerini view'a taşı
                ViewBag.Search = search;
                ViewBag.Status = status;
                ViewBag.Priority = priority;
                ViewBag.CustomerId = customerId;
                ViewBag.LogoProductId = logoProductId;
                ViewBag.AssignedToUserId = assignedToUserId;
                ViewBag.CreatedByUserId = createdByUserId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"] ?? "";

                return View(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TumTalepler yüklenirken hata");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return View(new List<TicketListDto>());
            }
        }
        [HttpGet("AdminReport")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AdminReportData()
        {
            var result = await _ticketService.GetAdminReportAsync(Token);
            return Json(result);
        }

        [HttpGet("BenimRaporom")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult BenimRaporom()
        {
            ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"] ?? "";
            return View("AdminReport");
        }
        [HttpGet("Rapor")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Rapor()
        {
            ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"] ?? "";
            return View("SuperAdminReport");
        }
        // ────────────────────────────────────────────────────────────────────
        #region Liste
        [HttpGet("Liste")]
        public async Task<IActionResult> Liste()
        {
            try
            {
                List<TicketListDto> tickets = await _ticketService.GetTicketsAsync(Token);

                // Admin/SuperAdmin için devir modalı için kullanıcıları yükle
                if (IsSuperAdmin || IsAdmin)
                {
                    List<UserListDto> adminUsers = await _userService.GetAdminUsersAsync(Token);
                    ViewBag.AdminUsers = adminUsers;
                }

                return View(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ticket listesi yüklenirken hata");
                TempData["Error"] = "Ticket listesi yüklenirken bir hata oluştu";
                return View(new List<TicketListDto>());
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Detay
        [HttpGet("GetLogoProducts")]
        public async Task<IActionResult> GetLogoProducts(int customerId)
        {
            var products = await _customerService.GetCustomerLogoProductsAsync(customerId, Token);
            return Json(products);
        }

        [HttpGet("Detay/{id:int}")]
        public async Task<IActionResult> Detay(int id)
        {
            try
            {
                TicketDto? ticket = await _ticketService.GetTicketByIdAsync(id, Token);
                if (ticket == null)
                {
                    TempData["Error"] = "Ticket bulunamadı";
                    return RedirectToAction(nameof(Liste));
                }

                // User kendi firmasının ticketını görebilir
                if (IsUser && !IsSuperAdmin && !IsAdmin)
                {
                    if (ticket.CustomerID != CallerCompanyId)
                    {
                        TempData["Error"] = "Bu ticketa erişim yetkiniz yok";
                        return RedirectToAction(nameof(Liste));
                    }
                }

                // Admin/SuperAdmin için atanabilecek kullanıcıları yükle
                if (IsSuperAdmin || IsAdmin)
                {
                    List<UserListDto> adminUsers = await _userService.GetAdminUsersAsync(Token);
                    if (IsAdmin && !IsSuperAdmin)
                        adminUsers = adminUsers.Where(u => u.ISAdmin == 1).ToList();
                    ViewBag.AdminUsers = adminUsers;
                }

                ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"];

                return View(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ticket detayı yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Ticket detayı yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ekle — User ve SuperAdmin
        [HttpGet("Ekle")]
        public async Task<IActionResult> Ekle()
        {
            try
            {
                // Sadece User için kendi firmasının kontrolü — Admin JS tarafında yapıyor
                if (IsUser && CallerCompanyId > 0)
                {
                    System.Text.Json.JsonElement? eligibility =
                        await _customerService.GetTicketEligibilityAsync(CallerCompanyId, Token);

                    if (eligibility.HasValue)
                    {
                        var el = eligibility.Value;
                        bool isContractExpired = el.TryGetProperty("IsContractExpired", out var p1) ? p1.GetInt32() == 1 : false;
                        bool isTicketExhausted = el.TryGetProperty("IsTicketExhausted", out var p2) ? p2.GetInt32() == 1 : false;
                        int ticketCount = el.TryGetProperty("TicketCount", out var p3) ? p3.GetInt32() : 0;
                        int daysLeft = el.TryGetProperty("DaysLeft", out var p4) ? p4.GetInt32() : 999;

                        if (isContractExpired)
                            TempData["Error"] = "Sözleşme süreniz dolmuştur. Yeni ticket oluşturamazsınız.";
                        else if (isTicketExhausted)
                            TempData["Error"] = "Ticket hakkınız kalmamıştır. Yeni ticket oluşturamazsınız.";
                        else if (ticketCount > 0 && ticketCount <= 5)
                            TempData["Warning"] = $"Dikkat! Yalnızca {ticketCount} ticket hakkınız kalmıştır.";

                        if (daysLeft is > 0 and <= 10)
                            TempData["Warning"] = $"Dikkat! Sözleşmeniz {daysLeft} gün içinde sona erecek.";

                        ViewBag.RemainingTickets = ticketCount;
                        ViewBag.ContractEndDate = el.TryGetProperty("ContractEndDate", out var ced)
                                                   && ced.ValueKind != System.Text.Json.JsonValueKind.Null
                                                   ? DateTime.Parse(ced.GetString()!) : (DateTime?)null;
                    }
                }

                await LoadTicketFormDropdownsAsync();
                return View("TicketForm", new TicketCreateDto
                {
                    CustomerID = CallerCompanyId,
                    CreatedByUserID = CallerUserId,
                    Priority = 2
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ticket ekleme sayfası yüklenirken hata");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        [HttpPost("Ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(TicketCreateDto dto, List<IFormFile>? Files)
        {


            try
            {
                dto.CreatedByUserID = CallerUserId;

                // User kendi firması için ticket açabilir
                if (IsUser)
                    dto.CustomerID = CallerCompanyId;

                (bool Success, string Message, List<string>? Errors, int? NewId) result =
                    await _ticketService.CreateTicketAsync(dto, Token);

                if (!result.Success)
                {
                    AddErrors(result.Errors, result.Message);
                    await LoadTicketFormDropdownsAsync();
                    return View("TicketForm", dto);
                }

                // Dosyalar varsa yükle
                if (result.NewId.HasValue && Files != null && Files.Any())
                {
                    foreach (IFormFile file in Files.Where(f => f.Length > 0))
                    {
                        await using Stream stream = file.OpenReadStream();
                        var uploadResult = await _ticketService.UploadFileAsync(
                            result.NewId.Value, stream, file.FileName, Token);

                        if (!uploadResult.Success)
                            _logger.LogWarning("Dosya yüklenemedi: {FileName}", file.FileName);
                    }
                }

                TempData["Success"] = result.Message ?? "Ticket başarıyla oluşturuldu";
                return RedirectToAction(nameof(Liste)); // ← Detay yerine Liste
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ticket eklenirken hata");
                ModelState.AddModelError(string.Empty, "Ticket eklenirken bir hata oluştu");
                await LoadTicketFormDropdownsAsync();
                return View("TicketForm", dto);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Durum Güncelle — Admin ve SuperAdmin
        [HttpPost("DurumGuncelle/{id:int}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DurumGuncelle(int id, TicketStatusUpdateDto dto, string? returnUrl = null)
        {
            try
            {
                // Status 1 (İşlemde) yapılınca otomatik üzerine al
                if (dto.Status == 1)
                {
                    await _ticketService.AssignTicketAsync(id, CallerUserId, Token);
                }

                (bool Success, string Message, List<string>? Errors) result =
                    await _ticketService.UpdateStatusAsync(id, dto, Token);
                if (result.Success)
                    TempData["Success"] = result.Message ?? "Durum başarıyla güncellendi";
                else
                    TempData["Error"] = result.Message ?? "Bir hata oluştu";

                // Nereden gelindiyse oraya dön
                if (!string.IsNullOrEmpty(returnUrl) && returnUrl == "liste")
                    return RedirectToAction(nameof(Liste));

                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durum güncellenirken hata. ID: {ID}", id);
                TempData["Error"] = "Durum güncellenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Devir — Admin ve SuperAdmin

        [HttpPost("Devret/{id:int}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Devret(int id, int assignedToUserId)
        {
            try
            {
                (bool Success, string Message) result =
                    await _ticketService.AssignTicketAsync(id, assignedToUserId, Token);

                if (result.Success)
                    TempData["Success"] = result.Message;
                else
                    TempData["Error"] = result.Message;

                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ticket devredilirken hata. ID: {ID}", id);
                TempData["Error"] = "Ticket devredilirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Yorum — Tüm roller

        [HttpPost("YorumEkle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YorumEkle(int id, string comment)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(comment))
                {
                    TempData["Error"] = "Yorum boş olamaz";
                    return RedirectToAction(nameof(Detay), new { id });
                }

                (bool Success, string Message) result =
                    await _ticketService.AddCommentAsync(id, comment, CallerUserId, Token);

                if (result.Success)
                    TempData["Success"] = result.Message;
                else
                    TempData["Error"] = result.Message;

                return RedirectToAction(nameof(Detay), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yorum eklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Yorum eklenirken bir hata oluştu";
                return RedirectToAction(nameof(Detay), new { id });
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Dosya İndir

        [HttpGet("Dosya/{fileId:int}")]
        public async Task<IActionResult> Dosya(int fileId)
        {
            try
            {
                string apiBase = HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["ApiSettings:BaseUrl"]!;
                string apiKey = HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["ApiSettings:ApiKey"]!;

                using HttpClient httpClient = HttpContext.RequestServices
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient();

                HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Get, $"{apiBase}/api/ticket/files/{fileId}/download");
                request.Headers.Add("X-API-Key", apiKey);
                if (!string.IsNullOrEmpty(Token))
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

                HttpResponseMessage response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return NotFound();

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                string contentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/octet-stream";
                string fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? "dosya";

                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya indirme hatası. FileID: {ID}", fileId);
                return NotFound();
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Dashboard

        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                if (IsSuperAdmin)
                {
                    SuperAdminDashboardDto? dashboard = await _ticketService.GetSuperAdminDashboardAsync(Token);
                    return View("SuperAdminDashboard", dashboard);
                }
                else if (IsAdmin)
                {
                    AdminPersonalDashboardDto? dashboard = await _ticketService.GetAdminDashboardAsync(Token);
                    return View("AdminDashboard", dashboard);
                }
                else
                {
                    UserPersonalDashboardDto? dashboard = await _ticketService.GetUserDashboardAsync(Token);
                    return View("UserDashboard", dashboard);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard yüklenirken hata");
                TempData["Error"] = "Dashboard yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Helpers

        private async Task LoadTicketFormDropdownsAsync()
        {
            int customerId = (IsSuperAdmin || IsAdmin) ? 0 : CallerCompanyId;

            if (IsSuperAdmin || IsAdmin)
            {
                List<UserListDto> adminUsers = await _userService.GetAdminUsersAsync(Token);
                if (IsAdmin && !IsSuperAdmin)
                    adminUsers = adminUsers.Where(u => u.ISAdmin == 1).ToList();
                ViewBag.AdminUsers = adminUsers;

                // Admin ve SuperAdmin için müşteri listesi ve tüm logo ürünleri
                var allLogoProducts = await _customerService.GetLogoProductsAsync(Token);
                ViewBag.LogoProducts = allLogoProducts;

                var customers = await _customerService.GetActiveCustomersAsync(Token);
                ViewBag.Customers = customers;
            }
            else if (customerId > 0)
            {
                // User — kendi firmasının logo ürünleri
                var logoProducts = await _customerService.GetCustomerLogoProductsAsync(customerId, Token);
                ViewBag.LogoProducts = logoProducts;
            }

            ViewBag.CallerUserId = CallerUserId;
            ViewBag.CallerCompanyId = CallerCompanyId;
            ViewBag.IsSuperAdmin = IsSuperAdmin;
        }

        private void AddErrors(List<string>? errors, string fallbackMessage)
        {
            if (errors != null && errors.Any())
                foreach (string error in errors)
                    ModelState.AddModelError(string.Empty, error);
            else
                ModelState.AddModelError(string.Empty, fallbackMessage);
        }

        #endregion

    }
}