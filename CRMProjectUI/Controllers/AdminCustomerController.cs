using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Route("AdminMusteri")]
    public class AdminCustomerController : Controller
    {
        private readonly CustomerApiService _customerService;
        private readonly ILogger<AdminCustomerController> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        private string? Token => User.FindFirst("JwtToken")?.Value;
        private bool IsSuperAdmin => User.IsInRole("SuperAdmin");
        private int CallerCompanyId => int.TryParse(User.FindFirst("CompanyId")?.Value, out int v) ? v : 0;

        public AdminCustomerController(
            CustomerApiService customerService,
            ILogger<AdminCustomerController> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _customerService = customerService;
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        // ────────────────────────────────────────────────────────────────────
        #region Dosya İndir — SuperAdmin

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("Dosya/{fileId:int}")]
        public async Task<IActionResult> Dosya(int fileId)
        {
            try
            {
                string apiBase = _configuration["ApiSettings:BaseUrl"]!;
                string apiKey = _configuration["ApiSettings:ApiKey"]!;

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                    $"{apiBase}/api/customer/files/{fileId}/download");
                request.Headers.Add("X-API-Key", apiKey);
                if (!string.IsNullOrEmpty(Token))
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return NotFound();

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
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
        #region Kod Kontrol

        [HttpGet("KodKontrol")]
        public async Task<IActionResult> KodKontrol(string code, int excludeId = 0)
        {
            bool exists = await _customerService.CheckCustomerCodeExistsAsync(code, excludeId, Token);
            return Json(new { exists });
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Liste & Detay

        [HttpGet("Liste")]
        public async Task<IActionResult> Liste()
        {
            try
            {
                List<CustomerListDto> customers = await _customerService.GetCustomersAsync(Token); 
                ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"] ?? "";
                return View(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri listesi yüklenirken hata oluştu");
                TempData["Error"] = "Müşteri listesi yüklenirken bir hata oluştu";
                return View(new List<CustomerListDto>());
            }
        }

        [HttpGet("Detay/{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Detay(int id)
        {
            try
            {
                CustomerDetailDto? detail = await _customerService.GetCustomerDetailAsync(id, Token);
                if (detail == null)
                {
                    TempData["Error"] = "Müşteri bulunamadı";
                    return RedirectToAction(nameof(Liste));
                }

                ViewBag.ApiBase = _configuration["ApiSettings:BaseUrl"];
                return View(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri detayı yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Müşteri detayı yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ekle

        [HttpGet("Ekle")]
        public async Task<IActionResult> Ekle()
        {
            try
            {
                await LoadDropdownsAsync();
                return View("CustomerForm", new CustomerDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri ekleme sayfası yüklenirken hata oluştu");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }

        [HttpPost("Ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(CustomerDto dto, IFormFile? ContractFile)
        {
            try
            {       // Admin yeni kayıtta her zaman aktif kaydetsin
                if (!IsSuperAdmin)
                    dto.Status = 1;
                (bool Success, string Message, List<string>? Errors, int? NewId) result =
                  await _customerService.CreateCustomerAsync(dto, Token);

                if (!result.Success)
                {
                    AddErrors(result.Errors, result.Message);
                    await LoadDropdownsAsync(dto.Il);
                    return View("CustomerForm", dto);
                }

                // Sözleşme yükleme — sadece SuperAdmin
                if (result.NewId.HasValue && ContractFile != null && ContractFile.Length > 0)
                {
                    if (IsSuperAdmin)
                    {
                        await using Stream stream = ContractFile.OpenReadStream();
                        var uploadResult = await _customerService.UploadContractAsync(
                            result.NewId.Value, stream, ContractFile.FileName, Token);
                        if (!uploadResult.Success)
                            TempData["Warning"] = "Müşteri eklendi fakat sözleşme yüklenemedi: " + uploadResult.Message;
                    }
                    else
                    {
                        TempData["Warning"] = "Müşteri eklendi fakat sözleşme yükleme yetkiniz yok";
                    }
                }

                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri eklenirken hata oluştu");
                ModelState.AddModelError(string.Empty, "Müşteri eklenirken bir hata oluştu");
                await LoadDropdownsAsync(dto.Il);
                return View("CustomerForm", dto);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Düzenle

        [HttpGet("Duzenle/{id:int}")]
        public async Task<IActionResult> Duzenle(int id)
        {
            try
            {
                CustomerDto? customer = await _customerService.GetCustomerByIdAsync(id, Token);
                if (customer == null)
                {
                    TempData["Error"] = "Müşteri bulunamadı";
                    return RedirectToAction(nameof(Liste));
                }
                await LoadDropdownsAsync(customer.Il);
                return View("CustomerForm", customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri düzenleme sayfası yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction(nameof(Liste));
            }
        }
        [HttpPost("Duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, CustomerDto dto, IFormFile? ContractFile)
        {
            try
            {
  
                // Admin düzenlemede Status, Importance, TicketCount değiştiremesin
                if (!IsSuperAdmin)
                {
                    CustomerDto? existing = await _customerService.GetCustomerByIdAsync(id, Token);
                    if (existing != null)
                    {
                        dto.Status = existing.Status;
                        dto.Importance = existing.Importance;
                        dto.TicketCount = existing.TicketCount;
                    }
                }

                // Kendi firmasını pasife alamaz — Status override edildikten sonra kontrol et
                if (CallerCompanyId == id && dto.Status == 0)
                {
                    ModelState.AddModelError(string.Empty, "Kendi firmanızı pasife alamazsınız!");
                    await LoadDropdownsAsync(dto.Il);
                    return View("CustomerForm", dto);
                }

                (bool Success, string Message, List<string>? Errors) result =
                    await _customerService.UpdateCustomerAsync(id, dto, Token);

                if (!result.Success)
                {
                    AddErrors(result.Errors, result.Message);
                    await LoadDropdownsAsync(dto.Il);
                    return View("CustomerForm", dto);
                }

                // Sözleşme yükleme — sadece SuperAdmin
                if (ContractFile != null && ContractFile.Length > 0)
                {
                    if (IsSuperAdmin)
                    {
                        await using Stream stream = ContractFile.OpenReadStream();
                        var uploadResult = await _customerService.UploadContractAsync(
                            id, stream, ContractFile.FileName, Token);
                        if (!uploadResult.Success)
                            TempData["Warning"] = "Müşteri güncellendi fakat sözleşme yüklenemedi: " + uploadResult.Message;
                    }
                    else
                    {
                        TempData["Warning"] = "Müşteri güncellendi fakat sözleşme yükleme yetkiniz yok";
                    }
                }

                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Liste));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri güncellenirken hata. ID: {ID}", id);
                ModelState.AddModelError(string.Empty, "Müşteri güncellenirken bir hata oluştu");
                await LoadDropdownsAsync(dto.Il);
                return View("CustomerForm", dto);
            }
        }
        [HttpGet("KullaniciBakis")]
        public async Task<IActionResult> KullaniciBakis()
        {
            try
            {
                var data = await _customerService.GetCustomersWithUsersAsync(Token);
                return View(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KullaniciBakis yüklenirken hata");
                TempData["Error"] = "Veriler yüklenirken bir hata oluştu";
                return View(new List<CustomerUsersOverviewDto>());
            }
        }
        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Sil & Durum — SuperAdmin

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("Sil/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            try
            {
                (bool Success, string Message) result = await _customerService.DeleteCustomerAsync(id, Token);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri silinirken hata. ID: {ID}", id);
                return Json(new { success = false, message = "Müşteri silinirken bir hata oluştu" });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("DurumDegistir/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DurumDegistir(int id)
        {
            try
            {
                if (CallerCompanyId == id)
                    return Json(new { success = false, message = "Kendi firmanızın durumunu değiştiremezsiniz!" });

                (bool Success, string Message, byte? NewStatus) result =
                    await _customerService.ToggleCustomerStatusAsync(id, Token);

                return Json(new
                {
                    success = result.Success,
                    message = result.Message,
                    newStatus = result.NewStatus,
                    isActive = result.NewStatus == 1
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri durumu değiştirilirken hata. ID: {ID}", id);
                return Json(new { success = false, message = "Durum değiştirilirken bir hata oluştu" });
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Dosya İşlemleri — SuperAdmin

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("DosyaListesi/{customerId:int}")]
        public async Task<IActionResult> DosyaListesi(int customerId)
        {
            try
            {
                List<CustomerFileDto> files = await _customerService.GetCustomerFilesAsync(customerId, Token);
                return Json(new { files });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya listesi yüklenirken hata. CustomerID: {ID}", customerId);
                return Json(new { files = new List<CustomerFileDto>() });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("DosyaYukle/{customerId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DosyaYukle(
            int customerId,
            IFormFile file,
            [FromForm] string? description,
            [FromForm] string category = "Genel",
            [FromForm] string? tags = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return Json(new { success = false, message = "Dosya seçilmedi" });
                if (file.Length > 50 * 1024 * 1024)
                    return Json(new { success = false, message = "Dosya boyutu 50MB'dan büyük olamaz" });

                HashSet<string> allowedExtensions = new HashSet<string>
                    { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".txt", ".zip", ".rar" };
                string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return Json(new { success = false, message = "Bu dosya türü desteklenmiyor" });

                using Stream stream = file.OpenReadStream();
                (bool Success, string Message, int? FileId) result =
                    await _customerService.UploadFileAsync(customerId, stream, file.FileName, description, category, tags, Token);
                return Json(new { success = result.Success, message = result.Message, fileId = result.FileId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yüklenirken hata. CustomerID: {ID}", customerId);
                return Json(new { success = false, message = "Dosya yüklenirken bir hata oluştu" });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("DosyaSil/{fileId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DosyaSil(int fileId)
        {
            try
            {
                (bool Success, string Message) result = await _customerService.DeleteFileAsync(fileId, Token);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silinirken hata. FileID: {ID}", fileId);
                return Json(new { success = false, message = "Dosya silinirken bir hata oluştu" });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("Dosyalar/{customerId:int}")]
        public async Task<IActionResult> Dosyalar(int customerId)
        {
            try
            {
                List<CustomerFileDto> files = await _customerService.GetCustomerFilesAsync(customerId, Token);
                return PartialView("_CustomerFiles", files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosyalar yüklenirken hata. CustomerID: {ID}", customerId);
                return PartialView("_CustomerFiles", new List<CustomerFileDto>());
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region İl/İlçe (AJAX)

        [HttpGet("Iller")]
        public async Task<IActionResult> Iller()
        {
            try
            {
                List<CitySelectDto> cities = await _customerService.GetCitiesAsync(Token);
                return Json(cities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İl listesi yüklenirken hata oluştu");
                return Json(new List<CitySelectDto>());
            }
        }

        [HttpGet("Ilceler/{il}")]
        public async Task<IActionResult> Ilceler(string il)
        {
            try
            {
                List<DistrictSelectDto> districts = await _customerService.GetDistrictsAsync(il, Token);
                return Json(districts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İlçe listesi yüklenirken hata. İl: {Il}", il);
                return Json(new List<DistrictSelectDto>());
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Helpers

        private async Task LoadDropdownsAsync(string? selectedIl = null)
        {
            ViewBag.Cities = await _customerService.GetCitiesAsync(Token);
            ViewBag.Districts = !string.IsNullOrEmpty(selectedIl)
                ? await _customerService.GetDistrictsAsync(selectedIl, Token)
                : new List<DistrictSelectDto>();
            ViewBag.LogoProducts = await _customerService.GetLogoProductsAsync(Token);
            ViewBag.IsSuperAdmin = IsSuperAdmin;
            ViewBag.ImportanceLevels = new List<string> { "VIP", "Önemli", "Normal", "Düşük" };
            ViewBag.CustomerTypes = new List<string> { "Kurumsal", "Bireysel" };
            ViewBag.FileCategories = new List<string> { "Genel", "Sözleşme", "Fatura", "Teklif", "Teknik", "Hukuki", "Other" };
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