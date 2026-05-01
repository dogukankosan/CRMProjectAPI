using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Route("AdminSertifikalar")]
    public class AdminSertifikalarController : Controller
    {
        private readonly CertificateApiService _certService;
        private readonly UserApiService _userService;
        private readonly ILogger<AdminSertifikalarController> _logger;
        private string? Token => User.FindFirst("JwtToken")?.Value;
        private bool IsSuperAdmin => User.IsInRole("SuperAdmin");
        private int UserId => int.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;
        public AdminSertifikalarController(
            CertificateApiService certService,
            UserApiService userService,
            ILogger<AdminSertifikalarController> logger)
        {
            _certService = certService;
            _userService = userService;
            _logger = logger;
        }
        // ── Liste ────────────────────────────────────────────────────────
        [HttpGet("Liste")]
        public async Task<IActionResult> Liste()
        {
            try
            {
                List<CertificateDto> list = await _certService.GetListAsync(Token);
                return View(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sertifika listesi yüklenirken hata");
                TempData["Error"] = "Liste yüklenirken bir hata oluştu";
                return View(new List<CertificateDto>());
            }
       }
        // ── Ekle GET ─────────────────────────────────────────────────────
        [HttpGet("Ekle")]
        public async Task<IActionResult> Ekle()
        {
            try
            {
                // Sadece Admin ve SuperAdmin kullanıcıları getir
                List<UserListDto> adminUsers = await _userService.GetAdminUsersAsync(Token);
                ViewBag.AdminUsers = adminUsers;
                return View(new CertificateCreateDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sertifika ekle sayfası yüklenirken hata");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        // ── Ekle POST ────────────────────────────────────────────────────
        [HttpPost("Ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(CertificateCreateDto dto, IFormFile? pdfFile)
        {
            try
            {
                // Dosyayı Base64'e çevir
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    TempData["Error"] = "PDF dosyası seçilmedi";
                    ViewBag.AdminUsers = await _userService.GetAdminUsersAsync(Token);
                    return View(dto);
                }
                using MemoryStream ms = new();
                await pdfFile.CopyToAsync(ms);
                dto.FileBase64 = Convert.ToBase64String(ms.ToArray());
                dto.OriginalFileName = pdfFile.FileName;
                var (success, message, errors, _) = await _certService.CreateAsync(dto, Token);
                if (!success)
                {
                    TempData["Error"] = errors?.Any() == true
                        ? string.Join(", ", errors) : message;
                    ViewBag.AdminUsers = await _userService.GetAdminUsersAsync(Token);
                    return View(dto);
                }
                TempData["Success"] = "Sertifika başarıyla eklendi";
                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sertifika eklenirken hata");
                TempData["Error"] = "Sertifika eklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        // ── Düzenle GET ──────────────────────────────────────────────────
        [HttpGet("Duzenle/{id:int}")]
        public async Task<IActionResult> Duzenle(int id)
        {
            try
            {
                CertificateDto? cert = await _certService.GetByIdAsync(id, Token);
                if (cert == null)
                {
                    TempData["Error"] = "Sertifika bulunamadı";
                    return RedirectToAction(nameof(Liste));
                }
                // Admin kısıtlaması
                if (!IsSuperAdmin && cert.UploadedByUserID != UserId)
                {
                    TempData["Error"] = "Bu sertifikayı düzenleme yetkiniz yok";
                    return RedirectToAction(nameof(Liste));
                }
                ViewBag.AdminUsers = await _userService.GetAdminUsersAsync(Token);
                ViewBag.Cert = cert;
                CertificateUpdateDto dto = new()
                {
                    Title = cert.Title,
                    Notes = cert.Notes
                };
                return View(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sertifika düzenle sayfası yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        // ── Düzenle POST ─────────────────────────────────────────────────
        [HttpPost("Duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, CertificateUpdateDto dto, IFormFile? pdfFile)
        {
            try
            {
                // Yeni dosya geldiyse Base64'e çevir
                if (pdfFile != null && pdfFile.Length > 0)
                {
                    using MemoryStream ms = new();
                    await pdfFile.CopyToAsync(ms);
                    dto.FileBase64 = Convert.ToBase64String(ms.ToArray());
                    dto.OriginalFileName = pdfFile.FileName;
                }
                var (success, message, errors) = await _certService.UpdateAsync(id, dto, Token);
                if (!success)
                {
                    TempData["Error"] = errors?.Any() == true
                        ? string.Join(", ", errors) : message;
                    CertificateDto? cert = await _certService.GetByIdAsync(id, Token);
                    ViewBag.AdminUsers = await _userService.GetAdminUsersAsync(Token);
                    ViewBag.Cert = cert;
                    return View(dto);
                }
                TempData["Success"] = "Sertifika başarıyla güncellendi";
                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sertifika güncellenirken hata. ID: {ID}", id);
                TempData["Error"] = "Sertifika güncellenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        // ── Sil ──────────────────────────────────────────────────────────
        [HttpPost("Sil/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var (success, message) = await _certService.DeleteAsync(id, Token);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;
            return RedirectToAction(nameof(Liste));
        }
        // ── İndir ────────────────────────────────────────────────────────
        [HttpGet("Indir/{id:int}")]
        public async Task<IActionResult> Indir(int id)
        {
            var (success, bytes, fileName) = await _certService.DownloadAsync(id, Token);
            if (!success || bytes == null)
            {
                TempData["Error"] = "Dosya indirilemedi";
                return RedirectToAction(nameof(Liste));
            }
            return File(bytes, "application/pdf", fileName ?? "sertifika.pdf");
        }
        // ── Görüntüle (inline PDF) ────────────────────────────────────────────
        [HttpGet("Goruntule/{id:int}")]
        public async Task<IActionResult> Goruntule(int id)
        {
            var (success, bytes, fileName) = await _certService.DownloadAsync(id, Token);
            if (!success || bytes == null)
                return NotFound();
            // Content-Disposition: inline → tarayıcıda açar, indirmez
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(bytes, "application/pdf");
        }
    }
}