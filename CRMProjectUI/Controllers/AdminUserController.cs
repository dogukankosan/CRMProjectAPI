using CRMProjectUI.APIService;
using CRMProjectUI.Filters;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRMProjectUI.Controllers
{
    [Authorize(Roles = "Admin,User,SuperAdmin")]
    [CompanyAuthorize]
    [Route("AdminKullanici")]
    public class AdminUserController : Controller
    {
        private readonly UserApiService _userService;
        private readonly CustomerApiService _customerService;
        private readonly ILogger<AdminUserController> _logger;

        private string? Token => User.FindFirst("JwtToken")?.Value;
        private byte CallerIsAdmin => byte.TryParse(User.FindFirst("IsAdmin")?.Value, out byte v) ? v : (byte)0;
        private int CallerCompanyId => int.TryParse(User.FindFirst("CompanyId")?.Value, out int v) ? v : 0;
        private int CallerUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int v) ? v : 0;

        public AdminUserController(
            UserApiService userService,
            CustomerApiService customerService,
            ILogger<AdminUserController> logger)
        {
            _userService = userService;
            _customerService = customerService;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────────────────
        #region Liste

        [Authorize(Roles = "Admin,SuperAdmin,User")]
        [HttpGet("Liste/{customerId:int}")]
        public async Task<IActionResult> Liste(int customerId)
        {
            // User sadece kendi firmasını görebilir
            if (CallerIsAdmin == 0 && CallerCompanyId != customerId)
                return RedirectToAction("Liste", new { customerId = CallerCompanyId });

            try
            {
                CustomerDto? customer = await _customerService.GetCustomerByIdAsync(customerId, Token);
                if (customer == null)
                {
                    TempData["Error"] = "Firma bulunamadı";
                    return RedirectToAction("Liste", "AdminMusteri");
                }

                List<UserListDto> users = await _userService.GetUsersByCustomerAsync(customerId, Token);
                ViewBag.CustomerId = customerId;
                ViewBag.CustomerName = customer.CustomerName;
                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı listesi yüklenirken hata. CustomerID: {ID}", customerId);
                TempData["Error"] = "Kullanıcı listesi yüklenirken bir hata oluştu";
                return RedirectToAction("Liste", "AdminMusteri");
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Ekle

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpGet("Ekle")]
        public async Task<IActionResult> Ekle(int customerId = 0)
        {
            try
            {
                CustomerDto? customer = await _customerService.GetCustomerByIdAsync(customerId, Token);
                ViewBag.IsEdit = false;
                ViewBag.CustomerId = customerId;
                ViewBag.CustomerName = customer?.CustomerName ?? "";
                return View("UserForm", new UserCreateDto { CompanyID = customerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ekleme sayfası yüklenirken hata");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction("Liste", new { customerId });
            }
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("Ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(UserCreateDto dto, IFormFile? PictureFile)
        {
            try
            {
                (bool Success, string Message, List<string>? Errors, int? NewId) result =
                    await _userService.CreateUserAsync(dto, Token);

                if (!result.Success)
                {
                    AddErrors(result.Errors, result.Message);
                    CustomerDto? customer = await _customerService.GetCustomerByIdAsync(dto.CompanyID, Token);
                    ViewBag.IsEdit = false;
                    ViewBag.CustomerId = dto.CompanyID;
                    ViewBag.CustomerName = customer?.CustomerName ?? "";
                    ViewBag.CurrentPic = dto.Picture;
                    return View("UserForm", dto);
                }

                if (result.NewId.HasValue && PictureFile != null && PictureFile.Length > 0)
                {
                    await using Stream stream = PictureFile.OpenReadStream();
                    await _userService.UploadPictureAsync(result.NewId.Value, stream, PictureFile.FileName, Token);
                }

                TempData["Success"] = result.Message;
                return RedirectToAction("Liste", new { customerId = dto.CompanyID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı eklenirken hata");
                ModelState.AddModelError(string.Empty, "Kullanıcı eklenirken bir hata oluştu");
                CustomerDto? customer = await _customerService.GetCustomerByIdAsync(dto.CompanyID, Token);
                ViewBag.IsEdit = false;
                ViewBag.CustomerId = dto.CompanyID;
                ViewBag.CustomerName = customer?.CustomerName ?? "";
                ViewBag.CurrentPic = dto.Picture;
                return View("UserForm", dto);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Düzenle

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpGet("Duzenle/{id:int}")]
        public async Task<IActionResult> Duzenle(int id)
        {
            try
            {
                // Admin kendi profilini buradan düzenleyemez
                if (CallerIsAdmin == 1 && CallerUserId == id)
                    return RedirectToAction("Profil");

                UserDto? user = await _userService.GetUserByIdAsync(id, Token);
                if (user == null)
                {
                    TempData["Error"] = "Kullanıcı bulunamadı";
                    return RedirectToAction("Liste", new { customerId = 0 });
                }

                // Admin başka bir Admin veya SuperAdmin'i düzenleyemez
                if (CallerIsAdmin == 1 && user.ISAdmin >= 1)
                {
                    TempData["Error"] = "Bu kullanıcıyı düzenleme yetkiniz yok.";
                    return RedirectToAction("Liste", new { customerId = user.CompanyID });
                }

                CustomerDto? customer = await _customerService.GetCustomerByIdAsync(user.CompanyID, Token);
                ViewBag.IsEdit = true;
                ViewBag.UserId = id;
                ViewBag.CustomerId = user.CompanyID;
                ViewBag.CustomerName = customer?.CustomerName ?? "";
                ViewBag.CurrentPic = user.Picture;

                UserUpdateDto dto = new UserUpdateDto
                {
                    Username = user.Username,
                    EMailAddress = user.EMailAddress,
                    Picture = user.Picture,
                    CompanyID = user.CompanyID,
                    ISAdmin = user.ISAdmin,
                    Status = user.Status,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    SendEmail = user.SendEmail
                };

                return View("UserForm", dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı düzenleme sayfası yüklenirken hata. ID: {ID}", id);
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction("Liste", new { customerId = 0 });
            }
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("Duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, UserUpdateDto dto, IFormFile? PictureFile)
        {
            try
            {
                // Admin kendi profilini buradan düzenleyemez
                if (CallerIsAdmin == 1 && CallerUserId == id)
                {
                    TempData["Error"] = "Kendi profilinizi Profil sayfasından güncelleyiniz";
                    return RedirectToAction("Profil");
                }

                // Admin başka bir Admin veya SuperAdmin'i düzenleyemez
                if (CallerIsAdmin == 1)
                {
                    UserDto? targetUser = await _userService.GetUserByIdAsync(id, Token);
                    if (targetUser != null && targetUser.ISAdmin >= 1)
                    {
                        TempData["Error"] = "Bu kullanıcıyı düzenleme yetkiniz yok.";
                        return RedirectToAction("Liste", new { customerId = dto.CompanyID });
                    }
                }

                // Kendi statüsünü pasif yapamaz, rolünü değiştiremez
                if (CallerUserId == id)
                {
                    if (!dto.Status)
                    {
                        ModelState.AddModelError(string.Empty, "Kendi hesabınızı pasif yapamazsınız!");
                        ViewBag.IsEdit = true; ViewBag.UserId = id;
                        ViewBag.CustomerId = dto.CompanyID;
                        ViewBag.CurrentPic = dto.Picture;
                        return View("UserForm", dto);
                    }
                    if (dto.ISAdmin != CallerIsAdmin)
                    {
                        ModelState.AddModelError(string.Empty, "Kendi rolünüzü değiştiremezsiniz!");
                        ViewBag.IsEdit = true; ViewBag.UserId = id;
                        ViewBag.CustomerId = dto.CompanyID;
                        ViewBag.CurrentPic = dto.Picture;
                        return View("UserForm", dto);
                    }
                }

                (bool Success, string Message, List<string>? Errors) result =
                    await _userService.UpdateUserAsync(id, dto, Token);

                if (!result.Success)
                {
                    AddErrors(result.Errors, result.Message);
                    ViewBag.IsEdit = true;
                    ViewBag.UserId = id;
                    ViewBag.CustomerId = dto.CompanyID;
                    ViewBag.CurrentPic = dto.Picture;
                    return View("UserForm", dto);
                }

                string? newPicturePath = null;
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    await using Stream stream = PictureFile.OpenReadStream();
                    var uploadResult = await _userService.UploadPictureAsync(id, stream, PictureFile.FileName, Token);
                    if (uploadResult.Success)
                        newPicturePath = uploadResult.PicturePath;
                }

                // Kendi bilgilerini güncelliyorsa cookie'yi yenile
                if (CallerUserId == id)
                    await RefreshCookieAsync(dto.FullName, dto.Username, dto.EMailAddress, dto.ISAdmin,
                        newPicturePath ?? dto.Picture);

                TempData["Success"] = result.Message;
                return RedirectToAction("Liste", new { customerId = dto.CompanyID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı güncellenirken hata. ID: {ID}", id);
                ModelState.AddModelError(string.Empty, "Kullanıcı güncellenirken bir hata oluştu");
                ViewBag.IsEdit = true;
                ViewBag.UserId = id;
                ViewBag.CustomerId = dto.CompanyID;
                ViewBag.CurrentPic = dto.Picture;
                return View("UserForm", dto);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Sil & Durum — Sadece SuperAdmin

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("Sil/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            try
            {
                if (CallerUserId == id)
                    return Json(new { success = false, message = "Kendi hesabınızı silemezsiniz!" });

                (bool Success, string Message) result = await _userService.DeleteUserAsync(id, Token);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı silinirken hata. ID: {ID}", id);
                return Json(new { success = false, message = "Kullanıcı silinirken bir hata oluştu" });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("DurumDegistir/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DurumDegistir(int id)
        {
            try
            {
                if (CallerUserId == id)
                    return Json(new { success = false, message = "Kendi hesabınızın durumunu değiştiremezsiniz!" });

                (bool Success, string Message, bool? NewStatus) result =
                    await _userService.ToggleUserStatusAsync(id, Token);

                return Json(new
                {
                    success = result.Success,
                    message = result.Message,
                    newStatus = result.NewStatus,
                    isActive = result.NewStatus == true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı durumu değiştirilirken hata. ID: {ID}", id);
                return Json(new { success = false, message = "Durum değiştirilirken bir hata oluştu" });
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Resim İşlemleri

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("ResimSil/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResimSil(int id)
        {
            try
            {
                (bool Success, string Message) result = await _userService.DeletePictureAsync(id, Token);

                if (CallerUserId == id && result.Success)
                    await RefreshCookiePictureAsync("");

                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resim silinirken hata. ID: {ID}", id);
                return Json(new { success = false, message = "Resim silinirken bir hata oluştu" });
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Kullanıcı Kontrolleri — Tüm roller

        [HttpGet("KullaniciAdiKontrol")]
        public async Task<IActionResult> KullaniciAdiKontrol(string username, int excludeId = 0)
        {
            bool exists = await _userService.CheckUsernameExistsAsync(username, excludeId, Token);
            return Json(new { exists });
        }

        [HttpGet("EmailKontrol")]
        public async Task<IActionResult> EmailKontrol(string email, int excludeId = 0)
        {
            bool exists = await _userService.CheckEmailExistsAsync(email, excludeId, Token);
            return Json(new { exists });
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Profil — Tüm roller

        [HttpGet("Profil")]
        public async Task<IActionResult> Profil()
        {
            try
            {
                UserDto? user = await _userService.GetUserByIdAsync(CallerUserId, Token);
                if (user == null)
                {
                    TempData["Error"] = "Kullanıcı bulunamadı";
                    return RedirectToAction("Index", "AdminHome");
                }

                ProfilUpdateDto dto = new ProfilUpdateDto
                {
                    FullName = user.FullName,
                    Username = user.Username,
                    EMailAddress = user.EMailAddress,
                    PhoneNumber = user.PhoneNumber,
                    Picture = user.Picture
                };

                ViewBag.CurrentPic = user.Picture;
                return View(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil sayfası yüklenirken hata");
                TempData["Error"] = "Sayfa yüklenirken bir hata oluştu";
                return RedirectToAction("Index", "AdminHome");
            }
        }
        [HttpPost("Profil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profil(ProfilUpdateDto dto, IFormFile? PictureFile)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.CurrentPic = dto.Picture;
                    return View(dto);
                }

                // 1. Mevcut kullanıcı bilgilerini getir (Değişmemesi gereken alanlar için: IsAdmin, CompanyId vb.)
                UserDto? existing = await _userService.GetUserByIdAsync(CallerUserId, Token);
                if (existing == null)
                {
                    TempData["Error"] = "Kullanıcı oturumu bulunamadı.";
                    return RedirectToAction("Index", "AdminHome");
                }

                // 2. Güncelleme DTO'sunu hazırla
                UserUpdateDto updateDto = new UserUpdateDto
                {
                    FullName = dto.FullName,
                    Username = dto.Username,
                    EMailAddress = dto.EMailAddress,
                    PhoneNumber = dto.PhoneNumber,
                    // Şifre boşsa eski şifre kalsın (null gönderiyoruz), doluysa yenisi set edilsin
                    Password = string.IsNullOrWhiteSpace(dto.NewPassword) ? null : dto.NewPassword,
                    Picture = existing.Picture,
                    CompanyID = existing.CompanyID, // Admin kendi şirketini değiştiremesin
                    ISAdmin = existing.ISAdmin,     // Admin kendi yetkisini buradan yükseltemesin
                    Status = existing.Status,       // Kendi hesabını buradan pasif yapamasın
                    SendEmail = existing.SendEmail
                };

                // 3. API'ye gönder
                var result = await _userService.UpdateUserAsync(CallerUserId, updateDto, Token);

                if (!result.Success)
                {
                    AddErrors(result.Errors, result.Message);
                    ViewBag.CurrentPic = dto.Picture;
                    return View(dto);
                }

                // 4. Resim varsa yükle
                string? newPicturePath = existing.Picture;
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    await using Stream stream = PictureFile.OpenReadStream();
                    var uploadResult = await _userService.UploadPictureAsync(CallerUserId, stream, PictureFile.FileName, Token);
                    if (uploadResult.Success)
                        newPicturePath = uploadResult.PicturePath;
                }

                // 5. KRİTİK NOKTA: Kendi bilgilerini değiştirdiği için Cookie'deki 
                // isim, email ve resim bilgilerini hemen tazeliyoruz ki sayfa yenilenince yeni hali gelsin.
                await RefreshCookieAsync(dto.FullName, dto.Username, dto.EMailAddress,
                    existing.ISAdmin, newPicturePath);

                TempData["Success"] = "Profil bilgileriniz başarıyla güncellendi.";
                return RedirectToAction("Profil");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil güncellenirken hata oluştu.");
                ModelState.AddModelError(string.Empty, "Profil güncellenirken bir hata oluştu.");
                ViewBag.CurrentPic = dto.Picture;
                return View(dto);
            }
        }

        [HttpPost("ResimSilProfil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResimSilProfil()
        {
            try
            {
                (bool Success, string Message) result = await _userService.DeletePictureAsync(CallerUserId, Token);

                if (result.Success)
                    await RefreshCookiePictureAsync("");

                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil resmi silinirken hata");
                return Json(new { success = false, message = "Resim silinirken bir hata oluştu" });
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Helpers

        private void AddErrors(List<string>? errors, string fallbackMessage)
        {
            if (errors != null && errors.Any())
                foreach (string error in errors)
                    ModelState.AddModelError(string.Empty, error);
            else
                ModelState.AddModelError(string.Empty, fallbackMessage);
        }

        /// <summary>
        /// Cookie'yi yeniler — mevcut AuthenticationProperties korunur (RememberMe, ExpiresUtc)
        /// </summary>
        private async Task RefreshCookieAsync(
            string fullName, string username, string email, byte isAdmin, string? picture)
        {
            List<Claim> claims = User.Claims
                .Where(c => c.Type != "FullName" &&
                            c.Type != ClaimTypes.Name &&
                            c.Type != ClaimTypes.Email &&
                            c.Type != "Picture" &&
                            c.Type != "IsAdmin" &&
                            c.Type != ClaimTypes.Role)
                .ToList();

            claims.Add(new Claim("FullName", fullName ?? username));
            claims.Add(new Claim(ClaimTypes.Name, username));
            claims.Add(new Claim(ClaimTypes.Email, email));
            claims.Add(new Claim("IsAdmin", isAdmin.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, isAdmin == 2 ? "SuperAdmin"
                                                  : isAdmin == 1 ? "Admin"
                                                  : "User"));
            claims.Add(new Claim("Picture", picture ?? ""));

            ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties existingProps = (await HttpContext.AuthenticateAsync()).Properties!;

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                existingProps); // ← RememberMe ve ExpiresUtc korunur
        }

        /// <summary>
        /// Sadece Picture claim'ini günceller
        /// </summary>
        private async Task RefreshCookiePictureAsync(string picture)
        {
            List<Claim> claims = User.Claims
                .Where(c => c.Type != "Picture")
                .ToList();
            claims.Add(new Claim("Picture", picture));

            ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties existingProps = (await HttpContext.AuthenticateAsync()).Properties!;

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                existingProps);
        }

        #endregion
    }
}