using CRMProjectUI.APIService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("AdminHataLog")]
    public class AdminHataLogController : Controller
    {
        private readonly ErrorLogApiService _logService;
        private string? Token => User.FindFirst("JwtToken")?.Value;

        public AdminHataLogController(ErrorLogApiService logService)
        {
            _logService = logService;
        }

        [HttpGet("Liste")]
        public IActionResult Liste() => View();

        [HttpGet("Data")]
        public async Task<IActionResult> Data(
            string? type = null,
            bool? onlyErrors = null,
            string? startDate = null,
            string? endDate = null,
            int page = 1,
            int pageSize = 50)
        {
            var (total, pages, data) = await _logService.GetLogsAsync(
                type, onlyErrors, startDate, endDate, page, pageSize, Token);
            return Json(new { total, pages, data });
        }

        [HttpGet("Detay/{id:long}")]
        public async Task<IActionResult> Detay(long id)
        {
            var json = await _logService.GetLogDetailAsync(id, Token);
            if (json == null) return NotFound();
            return Content(json, "application/json");
        }


        [HttpPost("Sil/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(long id)
        {
            bool ok = await _logService.DeleteLogAsync(id, Token);
            return Json(new { success = ok, message = ok ? "Log silindi" : "Silinemedi" });
        }

        [HttpPost("Temizle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Temizle(string? type, string? beforeDate)
        {
            var (success, message) = await _logService.ClearLogsAsync(type, beforeDate, Token);
            return Json(new { success, message });
        }
    }
}