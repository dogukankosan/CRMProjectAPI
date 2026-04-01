using CRMProjectUI.APIService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    [Authorize]
    [Route("UiLog")]
    public class UiLogController : Controller
    {
        private readonly LogApiService _logService;
        private string? Token => User.FindFirst("JwtToken")?.Value;

        public UiLogController(LogApiService logService)
        {
            _logService = logService;
        }

        [HttpPost("Error")]
        [IgnoreAntiforgeryToken]  // ✅ fetch'ten token göndermiyoruz
        public async Task<IActionResult> Error([FromBody] UiErrorRequest dto)
        {
            await _logService.LogUiErrorAsync(dto.Message, dto.Stack, dto.Page, Token);
            return Ok();
        }
    }

    public class UiErrorRequest
    {
        public string? Message { get; set; }
        public string? Stack { get; set; }
        public string? Page { get; set; }
    }
}