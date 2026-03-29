using CRMProjectUI.APIService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRMProjectUI.Controllers
{
    [Route("Auth")]
    [AllowAnonymous]  // ← bunu ekle
    public class AuthController : Controller
    {
        private readonly AuthApiService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthApiService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // ==================== LOGIN ====================

        [HttpGet("Login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // Zaten giriş yaptıysa yönlendir
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRole();

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                (bool Success, string Message, LoginResponseDto? Data) result =
                    await _authService.LoginAsync(model.Username, model.Password);

                if (!result.Success || result.Data == null)
                {
                    ModelState.AddModelError(string.Empty, result.Message);
                    return View(model);
                }

                LoginResponseDto data = result.Data;

                // Claims oluştur
                List<Claim> claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, data.UserId.ToString()),
                    new Claim(ClaimTypes.Name,           data.Username),
                    new Claim(ClaimTypes.Email,          data.Email),
                    new Claim("FullName",   data.FullName),
              new Claim("IsAdmin", data.IsAdmin.ToString()),  // "0", "1", "2"
                    new Claim("CompanyId",  data.CompanyId.ToString()),
                    new Claim("Picture",    data.Picture ?? ""),
                    new Claim("JwtToken",   data.Token)   // API isteklerinde kullanmak için
                };
                // OLMASI GEREKEN
                claims.Add(new Claim(ClaimTypes.Role, data.IsAdmin == 2 ? "SuperAdmin"
                                                    : data.IsAdmin == 1 ? "Admin"
                                                    : "User"));

                ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                AuthenticationProperties authProps = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe
                        ? DateTimeOffset.UtcNow.AddDays(7)
                        : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    authProps);

                _logger.LogInformation("Kullanıcı giriş yaptı: {Username} | Admin: {IsAdmin}", data.Username, data.IsAdmin);

                // ReturnUrl varsa oraya git
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                // Role göre yönlendir
                return RedirectToRole(data.IsAdmin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login hatası: {Username}", model.Username);
                ModelState.AddModelError(string.Empty, "Giriş yapılırken bir hata oluştu. Lütfen tekrar deneyin.");
                return View(model);
            }
        }

        // ==================== LOGOUT ====================

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            string username = User.Identity?.Name ?? "?";
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Kullanıcı çıkış yaptı: {Username}", username);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet("Logout")]
        public async Task<IActionResult> LogoutGet()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ==================== ACCESS DENIED ====================

        [HttpGet("AccessDenied")]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ==================== HELPER ====================

        private IActionResult RedirectToRole(byte? isAdmin = null)
        {
            byte admin = isAdmin ?? byte.Parse(User.FindFirst("IsAdmin")?.Value ?? "0");
            return admin >= 1
                ? RedirectToAction("Index", "AdminHome")
                : RedirectToAction("Index", "AdminHome");
        }
    }

    // ==================== VIEW MODEL ====================

    public class LoginViewModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
    }
}