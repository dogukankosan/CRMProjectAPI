using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("AdminMail")]
    public class AdminMailSettingsController : Controller
    {
        private readonly MailSettingsApiService _mailService;
        private readonly ILogger<AdminMailSettingsController> _logger;

        private string? Token => User.FindFirst("JwtToken")?.Value;

        public AdminMailSettingsController(
            MailSettingsApiService mailService,
            ILogger<AdminMailSettingsController> logger)
        {
            _mailService = mailService;
            _logger = logger;
        }

        // ==================== GET ====================
        [HttpGet("Ayarlar")]
        public async Task<IActionResult> Index()
        {
            try
            {
                MailSettingsDto? settings = await _mailService.GetAsync(Token);
                return View(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mail ayarları yüklenirken hata");
                TempData["Error"] = "Mail ayarları yüklenirken bir hata oluştu";
                return View((MailSettingsDto?)null);
            }
        }

        // ==================== UPSERT ====================
        [HttpPost("Kaydet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kaydet(MailSettingsUpdateDto dto)
        {
            try
            {
                (bool Success, string Message, List<string>? Errors) result =
                    await _mailService.UpsertAsync(dto, Token);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                if (result.Errors?.Any() == true)
                    foreach (string error in result.Errors)
                        ModelState.AddModelError(string.Empty, error);
                else
                    ModelState.AddModelError(string.Empty, result.Message);

                // Mevcut ayarları yeniden yükle
                MailSettingsDto? settings = await _mailService.GetAsync(Token);
                if (settings != null)
                {
                    settings.MailFrom = dto.MailFrom;
                    settings.DisplayName = dto.DisplayName;
                    settings.SmtpHost = dto.SmtpHost;
                    settings.SmtpPort = dto.SmtpPort;
                    settings.EnableSsl = dto.EnableSsl;
                    settings.Username = dto.Username;
                    settings.TimeoutSeconds = dto.TimeoutSeconds;
                    settings.Signature = dto.Signature;
                    // Password kasıtlı boş bırak
                }
                return View("Index", settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mail ayarları kaydedilirken hata");
                TempData["Error"] = "Mail ayarları kaydedilirken bir hata oluştu";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("TestGonder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestGonder([FromBody] MailTestRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.MailTo))
                    return Json(new { success = false, message = "Alıcı mail adresi zorunludur" });

                // Form değerlerini MailSettingsUpdateDto'ya çevir
                MailSettingsUpdateDto? formValues = null;
                if (!string.IsNullOrEmpty(request.SmtpHost))
                {
                    formValues = new MailSettingsUpdateDto
                    {
                        MailFrom = request.MailFrom ?? string.Empty,
                        DisplayName = request.DisplayName ?? string.Empty,
                        SmtpHost = request.SmtpHost,
                        SmtpPort = request.SmtpPort ?? 587,
                        EnableSsl = request.EnableSsl ?? true,
                        Username = request.Username ?? string.Empty,
                        Password = request.Password,
                        TimeoutSeconds = request.TimeoutSeconds ?? 30,
                        Signature = request.Signature
                    };
                }

                (bool Success, string Message) result =
                    await _mailService.TestAsync(request.MailTo, request.Subject ?? "Test Maili", formValues, Token);

                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test maili gönderilirken hata");
                return Json(new { success = false, message = "Test maili gönderilirken bir hata oluştu" });
            }
        }
    }


}