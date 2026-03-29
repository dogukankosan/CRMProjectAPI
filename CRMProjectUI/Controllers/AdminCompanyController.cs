using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("AdminSirket")]
    public class AdminCompanyController : Controller
    {
        private readonly CompanyApiService _companyApiService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AdminCompanyController> _logger;

        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
        private static readonly string[] AllowedFaviconExtensions = { ".ico", ".png" };
        private const long MaxFileSize = 5 * 1024 * 1024;

        private string? Token => User.FindFirst("JwtToken")?.Value;

        public AdminCompanyController(
            CompanyApiService companyApiService,
            IWebHostEnvironment env,
            ILogger<AdminCompanyController> logger)
        {
            _companyApiService = companyApiService;
            _env = env;
            _logger = logger;
        }

        [HttpGet("Liste")]
        public async Task<IActionResult> Index()
        {
            try
            {
                CompanyDto? company = await _companyApiService.GetCompanyAsync(Token);
                return View(company);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firma bilgisi alınamadı");
                TempData["Type"] = "error";
                TempData["Message"] = "Firma bilgileri alınamadı";
                return View((CompanyDto?)null);
            }
        }

        [HttpGet("Guncelle")]
        public async Task<IActionResult> Update()
        {
            try
            {
                CompanyDto? company = await _companyApiService.GetCompanyAsync(Token);
                if (company == null)
                {
                    TempData["Type"] = "error";
                    TempData["Message"] = "Firma bilgisi bulunamadı";
                    return RedirectToAction("Index");
                }
                return View(company);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firma bilgisi alınamadı");
                TempData["Type"] = "error";
                TempData["Message"] = "Bir hata oluştu";
                return RedirectToAction("Index");
            }
        }
        [HttpPost("Guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(CompanyDto dto, IFormFile? LogoFile, IFormFile? FaviconFile)
        {
            try
            {
                ModelState.Remove("LogoPath");
                ModelState.Remove("FaviconPath");

                if (!ModelState.IsValid)
                {
                    ViewBag.MsgType = "error";
                    ViewBag.MsgText = string.Join("<br>", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    return View(dto);
                }

                // Önce firma bilgilerini güncelle
                (bool updateSuccess, string message, List<string>? apiErrors) =
                    await _companyApiService.UpdateCompanyAsync(dto, Token);

                if (!updateSuccess)
                {
                    ViewBag.MsgType = "error";
                    ViewBag.MsgText = apiErrors?.Any() == true
                        ? string.Join("<br>", apiErrors)
                        : message;
                    return View(dto);
                }

                // Logo API'ye yükle
                if (LogoFile != null && LogoFile.Length > 0)
                {
                    await using Stream stream = LogoFile.OpenReadStream();
                    var logoResult = await _companyApiService.UploadLogoAsync(stream, LogoFile.FileName, Token);
                    if (!logoResult.Success)
                    {
                        TempData["Warning"] = "Firma güncellendi fakat logo yüklenemedi: " + logoResult.Message;
                        return RedirectToAction("Index");
                    }
                }

                // Favicon API'ye yükle
                if (FaviconFile != null && FaviconFile.Length > 0)
                {
                    await using Stream stream = FaviconFile.OpenReadStream();
                    var faviconResult = await _companyApiService.UploadFaviconAsync(stream, FaviconFile.FileName, Token);
                    if (!faviconResult.Success)
                    {
                        TempData["Warning"] = "Firma güncellendi fakat favicon yüklenemedi: " + faviconResult.Message;
                        return RedirectToAction("Index");
                    }
                }

                TempData["Type"] = "success";
                TempData["Message"] = message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firma güncelleme hatası");
                ViewBag.MsgType = "error";
                ViewBag.MsgText = "Beklenmeyen bir hata oluştu";
                return View(dto);
            }
        }

        #region Private Helpers

        private async Task<(bool Success, string? Path, string? Error)> SaveFileAsync(
            IFormFile file,
            string folderName,
            string[] allowedExtensions)
        {
            try
            {
                string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return (false, null, $"Geçersiz format. İzin verilen: {string.Join(", ", allowedExtensions)}");
                if (file.Length > MaxFileSize)
                    return (false, null, $"Dosya {MaxFileSize / 1024 / 1024}MB'dan büyük olamaz");
                if (!IsValidImageContentType(file.ContentType))
                    return (false, null, "Geçersiz dosya tipi");

                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", folderName);
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = $"{Guid.NewGuid()}{extension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using FileStream stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
                return (true, $"/uploads/{folderName}/{uniqueFileName}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya kaydetme hatası: {FileName}", file.FileName);
                return (false, null, "Dosya kaydedilemedi");
            }
        }

        private void DeleteOldFile(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            try
            {
                string fileName = Path.GetFileName(relativePath);
                string? folder = Path.GetDirectoryName(relativePath)
                    ?.TrimStart('/')
                    .Replace("/", Path.DirectorySeparatorChar.ToString());
                if (string.IsNullOrEmpty(folder)) return;
                string fullPath = Path.Combine(_env.WebRootPath, folder, fileName);
                if (!fullPath.StartsWith(_env.WebRootPath)) return;
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation("Eski dosya silindi: {Path}", relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Eski dosya silinemedi: {Path}", relativePath);
            }
        }

        private static bool IsValidImageContentType(string contentType)
        {
            HashSet<string> valid = new HashSet<string>
            {
                "image/jpeg", "image/png", "image/gif",
                "image/webp", "image/svg+xml",
                "image/x-icon", "image/vnd.microsoft.icon"
            };
            return valid.Contains(contentType.ToLowerInvariant());
        }

        #endregion
    }
}