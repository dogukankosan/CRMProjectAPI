using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IJwtService _jwtService;

        public AuthController(DapperContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Login — herkese açık, API key whitelist'te
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(ApiResponse.Fail("Kullanıcı adı ve şifre zorunludur"));

            const string sql = @"
                SELECT
                    u.ID, u.Username, u.Password, u.EMailAddress,
                    u.FullName, u.ISAdmin, u.Status,
                    u.CompanyID, u.Picture, u.SendEmail
                FROM Users u WITH (NOLOCK)
                WHERE u.Username = @Username
            ";
            using var connection = _context.CreateConnection();
            UserLoginDto? user = await connection.QueryFirstOrDefaultAsync<UserLoginDto>(sql, new { dto.Username });

            if (user == null)
                return Unauthorized(ApiResponse.Fail("Kullanıcı adı veya şifre hatalı"));

            if (!user.Status)
                return Unauthorized(ApiResponse.Fail("Hesabınız pasif. Lütfen yönetici ile iletişime geçin."));

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
                return Unauthorized(ApiResponse.Fail("Kullanıcı adı veya şifre hatalı"));
            bool companyActive = await connection.ExecuteScalarAsync<bool>(
    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE ID = @ID AND Status = 1) THEN 1 ELSE 0 END",
    new { ID = user.CompanyID });
            if (!companyActive)
                return Unauthorized(ApiResponse.Fail("Firmanız pasif durumdadır. Lütfen yönetici ile iletişime geçin."));
            JwtUserClaims claims = new JwtUserClaims
            {
                UserId = user.ID,
                Username = user.Username,
                Email = user.EMailAddress,
                FullName = user.FullName ?? user.Username,
                ISAdmin = user.ISAdmin,   // IsAdmin → ISAdmin
                CompanyId = user.CompanyID,
                Picture = user.Picture
            };

            string token = _jwtService.GenerateToken(claims);

            return Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
            {
                Token = token,
                UserId = user.ID,
                Username = user.Username,
                FullName = user.FullName ?? user.Username,
                Email = user.EMailAddress,
                IsAdmin = user.ISAdmin,   // byte geliyor artık
                CompanyId = user.CompanyID,
                Picture = user.Picture,
                ExpiresAt = DateTime.UtcNow.AddHours(8)
            }, "Giriş başarılı"));
        }

        /// <summary>
        /// Mevcut kullanıcı bilgisi — giriş yapmış herkes
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            string? userIdStr = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized(ApiResponse.Fail("Token geçersiz"));

            const string sql = @"
                SELECT
                    ID, Username, EMailAddress, FullName,
                    ISAdmin, Status, CompanyID, Picture, SendEmail,
                    PhoneNumber, CreatedDate
                FROM Users WITH (NOLOCK)
                WHERE ID = @ID AND Status = 1
            ";
            using var connection = _context.CreateConnection();
            UserDto? user = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { ID = userId });

            if (user == null)
                return Unauthorized(ApiResponse.Fail("Kullanıcı bulunamadı veya pasif"));

            return Ok(ApiResponse<UserDto>.Ok(user));
        }

        /// <summary>
        /// Şifre değiştir — giriş yapmış herkes kendi şifresini değiştirebilir
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            string? userIdStr = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized(ApiResponse.Fail("Token geçersiz"));

            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(ApiResponse.Fail("Mevcut şifre zorunludur"));

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(ApiResponse.Fail("Yeni şifre en az 6 karakter olmalıdır"));

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(ApiResponse.Fail("Yeni şifreler eşleşmiyor"));

            using var connection = _context.CreateConnection();
            string? hashedPass = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT Password FROM Users WHERE ID = @ID AND Status = 1",
                new { ID = userId });

            if (hashedPass == null)
                return NotFound(ApiResponse.NotFound("Kullanıcı bulunamadı"));

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, hashedPass))
                return BadRequest(ApiResponse.Fail("Mevcut şifre hatalı"));

            string newHashed = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
            await connection.ExecuteAsync(
                "UPDATE Users SET Password = @Password, UpdatedDate = GETDATE() WHERE ID = @ID",
                new { Password = newHashed, ID = userId });

            return Ok(ApiResponse.Ok("Şifre başarıyla değiştirildi"));
        }

        /// <summary>
        /// Sadece development'ta — şifre hash üret
        /// </summary>
        [HttpGet("hash-test")]
        [AllowAnonymous]
        public IActionResult HashTest([FromQuery] string password)
        {
            if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                return NotFound();
            string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            return Ok(new { password, hash });
        }
    }

    // ==================== AUTH DTO'LARI ====================

    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public byte IsAdmin { get; set; }  // bool → byte
        public int CompanyId { get; set; }
        public string? Picture { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    internal class UserLoginDto
    {
        public int ID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string EMailAddress { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public byte ISAdmin { get; set; }  // bool → byte
        public bool Status { get; set; }
        public int CompanyID { get; set; }
        public string? Picture { get; set; }
        public bool SendEmail { get; set; }
    }
}