using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Route("AdminDuyuru")]
    public class AdminAnnouncementController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBase;

        private string? Token => User.FindFirst("JwtToken")?.Value;

        public AdminAnnouncementController(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _apiBase = configuration["ApiSettings:BaseUrl"] ?? "";
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Token);
            client.DefaultRequestHeaders.Add("X-API-Key",
                _configuration["ApiSettings:ApiKey"]);
            return client;
        }

        [HttpGet("Liste")]
        public async Task<IActionResult> Liste()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{_apiBase}/api/announcement/all");
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<List<AnnouncementDto>>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return View(result?.Data ?? new List<AnnouncementDto>());
            }
            catch
            {
                TempData["Error"] = "Duyurular yüklenirken hata oluştu";
                return View(new List<AnnouncementDto>());
            }
        }

        [HttpGet("Ekle")]
        public IActionResult Ekle() => View(new AnnouncementCreateDto());

        [HttpPost("Ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(AnnouncementCreateDto dto, List<IFormFile>? files)
        {
            try
            {
                var client = CreateClient();
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(dto.Title ?? ""), "Title");
                form.Add(new StringContent(dto.Content ?? ""), "Content");
                form.Add(new StringContent(dto.Priority.ToString()), "Priority");

                if (files != null)
                    foreach (var file in files.Where(f => f.Length > 0))
                    {
                        var fc = new StreamContent(file.OpenReadStream());
                        fc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                        form.Add(fc, "files", file.FileName);
                    }

                var response = await client.PostAsync($"{_apiBase}/api/announcement", form);
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<int>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Success == true)
                {
                    TempData["Success"] = "Duyuru başarıyla eklendi";
                    return RedirectToAction("Liste");
                }
                TempData["Error"] = result?.Message ?? "Bir hata oluştu";
                return View(dto);
            }
            catch
            {
                TempData["Error"] = "Duyuru eklenirken hata oluştu";
                return View(dto);
            }
        }

        [HttpGet("Duzenle/{id:int}")]
        public async Task<IActionResult> Duzenle(int id)
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{_apiBase}/api/announcement/all");
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<List<AnnouncementDto>>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var ann = result?.Data?.FirstOrDefault(x => x.ID == id);
                if (ann == null)
                {
                    TempData["Error"] = "Duyuru bulunamadı";
                    return RedirectToAction("Liste");
                }
                return View(ann);
            }
            catch
            {
                TempData["Error"] = "Duyuru yüklenirken hata oluştu";
                return RedirectToAction("Liste");
            }
        }

        [HttpPost("Duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, AnnouncementCreateDto dto, List<IFormFile>? files)
        {
            try
            {
                var client = CreateClient();
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(dto.Title ?? ""), "Title");
                form.Add(new StringContent(dto.Content ?? ""), "Content");
                form.Add(new StringContent(dto.Priority.ToString()), "Priority");

                if (files != null)
                    foreach (var file in files.Where(f => f.Length > 0))
                    {
                        var fc = new StreamContent(file.OpenReadStream());
                        fc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                        form.Add(fc, "files", file.FileName);
                    }

                var response = await client.PutAsync($"{_apiBase}/api/announcement/{id}", form);
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<object>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Success == true)
                {
                    TempData["Success"] = "Duyuru güncellendi";
                    return RedirectToAction("Liste");
                }
                TempData["Error"] = result?.Message ?? "Bir hata oluştu";
                return View(dto);
            }
            catch
            {
                TempData["Error"] = "Duyuru güncellenirken hata oluştu";
                return View(dto);
            }
        }

        [HttpPost("Sil/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            try
            {
                var client = CreateClient();
                await client.DeleteAsync($"{_apiBase}/api/announcement/{id}");
                TempData["Success"] = "Duyuru silindi";
            }
            catch
            {
                TempData["Error"] = "Duyuru silinirken hata oluştu";
            }
            return RedirectToAction("Liste");
        }

        [HttpPost("DosyaSil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DosyaSil(int fileId, int announcementId)
        {
            try
            {
                var client = CreateClient();
                await client.DeleteAsync($"{_apiBase}/api/announcement/file/{fileId}");
                TempData["Success"] = "Dosya silindi";
            }
            catch
            {
                TempData["Error"] = "Dosya silinirken hata oluştu";
            }
            return RedirectToAction("Duzenle", new { id = announcementId });
        }
        [HttpGet("Dosya/{fileId:int}")]
        public async Task<IActionResult> DosyaIndir(int fileId)
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync(
                    $"{_apiBase}/api/announcement/file/{fileId}/download");

                if (!response.IsSuccessStatusCode) return NotFound();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString()
                                  ?? "application/octet-stream";
                var disposition = response.Content.Headers.ContentDisposition;
                var fileName = disposition?.FileNameStar
                                  ?? disposition?.FileName
                                  ?? "dosya";

                return File(bytes, contentType, fileName);
            }
            catch
            {
                return NotFound();
            }
        }
        [HttpPost("Toggle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            try
            {
                var client = CreateClient();
                await client.PatchAsync($"{_apiBase}/api/announcement/{id}/toggle", null);
                TempData["Success"] = "Durum güncellendi";
            }
            catch
            {
                TempData["Error"] = "Durum güncellenirken hata oluştu";
            }
            return RedirectToAction("Liste");
        }
    }
}