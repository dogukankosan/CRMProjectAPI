using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRMProjectUI.Controllers
{
    [Authorize] // Admin, SuperAdmin ve User erişebilsin
    [Route("AdminBilgiBankasi")]
    public class AdminBilgiBankasiController : Controller
    {
        private readonly KnowledgeBaseApiService _kbService;
        private readonly CustomerApiService _customerService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminBilgiBankasiController> _logger;

        private string? Token => User.FindFirst("JwtToken")?.Value;
        private bool IsSuperAdmin => User.IsInRole("SuperAdmin");
        private int UserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;

        public AdminBilgiBankasiController(
            KnowledgeBaseApiService kbService,
            CustomerApiService customerService,
            IConfiguration configuration,
            ILogger<AdminBilgiBankasiController> logger)
        {
            _kbService = kbService;
            _customerService = customerService;
            _configuration = configuration;
            _logger = logger;
        }

        // ── Liste ────────────────────────────────────────────────────────────
        [HttpGet("Liste")]
        public async Task<IActionResult> Liste(
            string? search = null,
            short? logoProduct = null,
            string? category = null,
            string? successMsg = null,
            string? errorMsg = null)
        {
            ViewBag.SuccessMsg = successMsg;
            ViewBag.ErrorMsg = errorMsg;
            try
            {
                List<KnowledgeBaseListDto> items = await _kbService.GetListAsync(search, logoProduct, category, Token);

                // ── Ürün listesi — User ise kendi firmasının ürünleri, Admin/SuperAdmin tümü ──
                List<LogoProductDto> products;
                if (User.IsInRole("User"))
                {
                    int companyId = int.TryParse(User.FindFirst("CompanyId")?.Value, out int cid) ? cid : 0;
                    var customerProducts = await _customerService.GetCustomerLogoProductsAsync(companyId, Token);
                    products = customerProducts
                        .Select(p => new LogoProductDto { ID = p.ID, LogoProductName = p.LogoProductName ?? "" })
                        .ToList();
                }
                else
                {
                    products = await _customerService.GetLogoProductsAsync(Token);
                }

                ViewBag.Search = search;
                ViewBag.LogoProduct = logoProduct;
                ViewBag.Category = category;
                ViewBag.Products = products;
                ViewBag.Categories = new List<string>
        {
            "Kurulum", "Hata Çözümü", "İpucu / Kısa Yol", "Güncelleme",
            "Entegrasyon", "Raporlama", "Yedekleme & Kurtarma",
            "Performans", "Güvenlik", "Veritabanı", "Genel"
        };

                return View(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bilgi bankası listesi yüklenirken hata");
                TempData["Error"] = "Liste yüklenirken bir hata oluştu";
                return View(new List<KnowledgeBaseListDto>());
            }
        }
        // ── Detay ────────────────────────────────────────────────────────────
        [HttpGet("Detay/{id:int}")]
        public async Task<IActionResult> Detay(int id)
        {
            try
            {
                KnowledgeBaseDto? kb = await _kbService.GetByIdAsync(id, Token);
                if (kb == null)
                {
                    TempData["Error"] = "Makale bulunamadı";
                    return RedirectToAction(nameof(Liste));
                }

                ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"];
                return View(kb);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bilgi bankası detayı yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Makale yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpGet("Ekle")]
        public async Task<IActionResult> Ekle()
        {
            try
            {
                List<LogoProductDto> products = await _customerService.GetLogoProductsAsync(Token);
                ViewBag.Products = products;
                ViewBag.Categories = new List<string>
{
    "Kurulum", "Hata Çözümü", "İpucu / Kısa Yol", "Güncelleme",
    "Entegrasyon", "Raporlama", "Yedekleme & Kurtarma",
    "Performans", "Güvenlik", "Veritabanı", "Genel"
};
                return View(new KnowledgeBaseCreateDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bilgi bankası ekle sayfası yüklenirken hata");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("Ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(KnowledgeBaseCreateDto dto, List<IFormFile>? files)
        {
            try
            {
                var (success, message, errors, newId) = await _kbService.CreateAsync(dto, Token);
                if (!success)
                {
                    TempData["Error"] = errors?.Any() == true
                        ? string.Join(", ", errors) : message;

                    List<LogoProductDto> products = await _customerService.GetLogoProductsAsync(Token);
                    ViewBag.Products = products;
                    ViewBag.Categories = new List<string>
{
    "Kurulum", "Hata Çözümü", "İpucu / Kısa Yol", "Güncelleme",
    "Entegrasyon", "Raporlama", "Yedekleme & Kurtarma",
    "Performans", "Güvenlik", "Veritabanı", "Genel"
};
                    return View(dto);
                }

                // Dosyaları yükle
                if (files?.Any() == true && newId.HasValue)
                {
                    foreach (var file in files.Where(f => f.Length > 0))
                    {
                        using Stream stream = file.OpenReadStream();
                        await _kbService.UploadFileAsync(newId.Value, stream, file.FileName, Token);
                    }
                }

                TempData["Success"] = "Makale başarıyla oluşturuldu";
                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bilgi bankası eklenirken hata");
                TempData["Error"] = "Makale eklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpGet("Duzenle/{id:int}")]
        public async Task<IActionResult> Duzenle(int id)
        {
            try
            {
                KnowledgeBaseDto? kb = await _kbService.GetByIdAsync(id, Token);
                if (kb == null)
                {
                    TempData["Error"] = "Makale bulunamadı";
                    return RedirectToAction(nameof(Liste));
                }

                // Admin sadece kendi makalesini düzenleyebilir
                if (!IsSuperAdmin && kb.CreatedBy != UserId)
                {
                    TempData["Error"] = "Bu makaleyi düzenleme yetkiniz yok";
                    return RedirectToAction(nameof(Liste));
                }

                List<LogoProductDto> products = await _customerService.GetLogoProductsAsync(Token);
                ViewBag.Products = products;
                ViewBag.Categories = new List<string>
{
    "Kurulum", "Hata Çözümü", "İpucu / Kısa Yol", "Güncelleme",
    "Entegrasyon", "Raporlama", "Yedekleme & Kurtarma",
    "Performans", "Güvenlik", "Veritabanı", "Genel"
};
                ViewBag.KbId = id;
                ViewBag.ExistingFiles = kb.Files;

                var dto = new KnowledgeBaseCreateDto
                {
                    Title = kb.Title,
                    Description = kb.Description,
                    CodeBlock = kb.CodeBlock,
                    CodeLanguage = kb.CodeLanguage,
                    LogoProductIDs = kb.Products.Select(p => p.LogoProductID).ToList(),
                    Category = kb.Category,
                    IsPublic = kb.IsPublic,
                    IsActive = kb.IsActive
                };

                return View(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bilgi bankası düzenle sayfası yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("Duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, KnowledgeBaseCreateDto dto, List<IFormFile>? files)
        {
            try
            {
                var (success, message, errors) = await _kbService.UpdateAsync(id, dto, Token);
                if (!success)
                {
                    TempData["Error"] = errors?.Any() == true
                        ? string.Join(", ", errors) : message;

                    List<LogoProductDto> products = await _customerService.GetLogoProductsAsync(Token);
                    ViewBag.Products = products;
                    ViewBag.Categories = new List<string>
{
    "Kurulum", "Hata Çözümü", "İpucu / Kısa Yol", "Güncelleme",
    "Entegrasyon", "Raporlama", "Yedekleme & Kurtarma",
    "Performans", "Güvenlik", "Veritabanı", "Genel"
};
                    ViewBag.KbId = id;

                    KnowledgeBaseDto? kb = await _kbService.GetByIdAsync(id, Token);
                    ViewBag.ExistingFiles = kb?.Files ?? new List<KnowledgeBaseFileDto>();
                    return View(dto);
                }

                // Yeni dosyaları yükle
                if (files?.Any() == true)
                {
                    foreach (var file in files.Where(f => f.Length > 0))
                    {
                        using Stream stream = file.OpenReadStream();
                        await _kbService.UploadFileAsync(id, stream, file.FileName, Token);
                    }
                }

                TempData["Success"] = "Makale başarıyla güncellendi";
                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bilgi bankası güncellenirken hata. ID: {ID}", id);
                TempData["Error"] = "Makale güncellenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("Sil/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var (success, message) = await _kbService.DeleteAsync(id, Token);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Liste));
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("ToggleAktif/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAktif(int id)
        {
            var (success, message) = await _kbService.ToggleActiveAsync(id, Token);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Liste));
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("TogglePublic/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublic(int id)
        {
            var (success, message) = await _kbService.TogglePublicAsync(id, Token);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Detay), new { id });
        }
        [HttpGet("DosyaIndir/{fileId:int}")]
        public async Task<IActionResult> DosyaIndir(int fileId)
        {
            var apiBase = _configuration["ApiSettings:BaseUrl"];
            var apiKey = _configuration["ApiSettings:ApiKey"];

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

            var response = await client.GetAsync($"{apiBase}/api/knowledgebase/files/{fileId}/download");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName
                           ?? "dosya";

            return File(bytes, contentType, fileName);
        }
        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("DosyaSil/{fileId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DosyaSil(int fileId, int kbId)
        {
            var (success, message) = await _kbService.DeleteFileAsync(fileId, Token);
            return Json(new { success, message });
        }
    }
}