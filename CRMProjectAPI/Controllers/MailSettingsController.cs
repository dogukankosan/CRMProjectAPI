using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;

namespace CRMProjectAPI.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ApiController]
    [Route("api/mail-settings")]

    public class MailSettingsController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IEncryptionService _encryption;
        private readonly ILogger<MailSettingsController> _logger;

        public MailSettingsController(
            DapperContext context,
            IEncryptionService encryption,
            ILogger<MailSettingsController> logger)
        {
            _context = context;
            _encryption = encryption;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            const string sql = @"
                SELECT
                    ID, MailFrom, DisplayName, SmtpHost, SmtpPort,
                    EnableSsl, Username, TimeoutSeconds, Signature,
                    CreatedDate, UpdatedDate
                FROM MailSettings WITH (NOLOCK)
                WHERE ID = 1
            ";
            using var connection = _context.CreateConnection();
            MailSettingsDto? settings = await connection.QueryFirstOrDefaultAsync<MailSettingsDto>(sql);

            if (settings == null)
                return NotFound(ApiResponse.NotFound("Mail ayarları bulunamadı"));

            settings.Password = null; // Şifreyi GET'te gönderme
            return Ok(ApiResponse<MailSettingsDto>.Ok(settings));
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] MailSettingsUpdateDto dto)
        {
            List<string> errors = ValidateMailSettings(dto);
            if (errors.Any())
                return BadRequest(ApiResponse.Fail(errors));

            using var connection = _context.CreateConnection();

            int existingId = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT ID FROM MailSettings WHERE ID = 1");

            if (existingId == 0)
            {
                // ==================== INSERT ====================
                if (string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest(ApiResponse.Fail("İlk kayıtta şifre zorunludur"));

                string encryptedPass = _encryption.Encrypt(dto.Password);

                const string insertSql = @"
                    INSERT INTO MailSettings
                        (MailFrom, DisplayName, SmtpHost, SmtpPort, EnableSsl,
                         Username, Password, IsActive, TimeoutSeconds, Signature, CreatedDate)
                    VALUES
                        (@MailFrom, @DisplayName, @SmtpHost, @SmtpPort, @EnableSsl,
                         @Username, @Password, 1, @TimeoutSeconds, @Signature, GETDATE())
                ";
                await connection.ExecuteAsync(insertSql, new
                {
                    dto.MailFrom,
                    dto.DisplayName,
                    dto.SmtpHost,
                    dto.SmtpPort,
                    dto.EnableSsl,
                    dto.Username,
                    Password = encryptedPass,
                    dto.TimeoutSeconds,
                    dto.Signature
                });
                return Ok(ApiResponse.Ok("Mail ayarları kaydedildi"));
            }
            else
            {
                // ==================== UPDATE ====================
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    // Şifre değiştirilecek
                    string encryptedPass = _encryption.Encrypt(dto.Password);
                    const string updateWithPassSql = @"
                        UPDATE MailSettings SET
                            MailFrom       = @MailFrom,
                            DisplayName    = @DisplayName,
                            SmtpHost       = @SmtpHost,
                            SmtpPort       = @SmtpPort,
                            EnableSsl      = @EnableSsl,
                            Username       = @Username,
                            Password       = @Password,
                            TimeoutSeconds = @TimeoutSeconds,
                            Signature      = @Signature,
                            UpdatedDate    = GETDATE()
                        WHERE ID = 1
                    ";
                    await connection.ExecuteAsync(updateWithPassSql, new
                    {
                        dto.MailFrom,
                        dto.DisplayName,
                        dto.SmtpHost,
                        dto.SmtpPort,
                        dto.EnableSsl,
                        dto.Username,
                        Password = encryptedPass,
                        dto.TimeoutSeconds,
                        dto.Signature
                    });
                }
                else
                {
                    // Şifre değiştirilmeyecek
                    const string updateSql = @"
                        UPDATE MailSettings SET
                            MailFrom       = @MailFrom,
                            DisplayName    = @DisplayName,
                            SmtpHost       = @SmtpHost,
                            SmtpPort       = @SmtpPort,
                            EnableSsl      = @EnableSsl,
                            Username       = @Username,
                            TimeoutSeconds = @TimeoutSeconds,
                            Signature      = @Signature,
                            UpdatedDate    = GETDATE()
                        WHERE ID = 1
                    ";
                    await connection.ExecuteAsync(updateSql, new
                    {
                        dto.MailFrom,
                        dto.DisplayName,
                        dto.SmtpHost,
                        dto.SmtpPort,
                        dto.EnableSsl,
                        dto.Username,
                        dto.TimeoutSeconds,
                        dto.Signature
                    });
                }
                return Ok(ApiResponse.Ok("Mail ayarları güncellendi"));
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("test")]
        public async Task<IActionResult> Test([FromBody] MailTestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.MailTo))
                return BadRequest(ApiResponse.Fail("Alıcı mail adresi zorunludur"));

            using var connection = _context.CreateConnection();

            // DB'den mevcut ayarları çek
            const string sql = @"
        SELECT ID, MailFrom, DisplayName, SmtpHost, SmtpPort,
               EnableSsl, Username, Password, TimeoutSeconds, Signature
        FROM MailSettings WITH (NOLOCK) WHERE ID = 1
    ";
            MailSettingsInternal? db = await connection.QueryFirstOrDefaultAsync<MailSettingsInternal>(sql);

            // Form değerleri varsa öncelik onlarda, yoksa DB'den al
            string mailFrom = dto.MailFrom ?? db?.MailFrom ?? string.Empty;
            string displayName = dto.DisplayName ?? db?.DisplayName ?? string.Empty;
            string smtpHost = dto.SmtpHost ?? db?.SmtpHost ?? string.Empty;
            int smtpPort = dto.SmtpPort ?? db?.SmtpPort ?? 587;
            bool enableSsl = dto.EnableSsl ?? db?.EnableSsl ?? true;
            string username = dto.Username ?? db?.Username ?? string.Empty;
            int timeout = dto.TimeoutSeconds ?? db?.TimeoutSeconds ?? 30;
            string? signature = dto.Signature ?? db?.Signature;

            // Şifre — form'dan geldiyse kullan, yoksa DB'den çöz
            string smtpPassword;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                smtpPassword = dto.Password; // form'dan geldi, plain text
            }
            else if (db != null && !string.IsNullOrEmpty(db.Password))
            {
                smtpPassword = _encryption.Decrypt(db.Password); // DB'den çöz
                if (string.IsNullOrEmpty(smtpPassword))
                    return BadRequest(ApiResponse.Fail("Mail şifresi çözülemedi. Şifreyi girin."));
            }
            else
            {
                return BadRequest(ApiResponse.Fail("SMTP şifresi zorunludur."));
            }

            if (string.IsNullOrWhiteSpace(smtpHost))
                return BadRequest(ApiResponse.Fail("SMTP sunucu adresi zorunludur."));

            bool success = false;
            string errorMessage = string.Empty;

            try
            {
                using SmtpClient smtp = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(username, smtpPassword),
                    Timeout = timeout * 1000,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                string signatureHtml = !string.IsNullOrEmpty(signature)
                    ? $"<hr style='border:none;border-top:1px solid #e2e8f0;margin:24px 0;'/>{signature}"
                    : string.Empty;

                string body = $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
                <h3 style='color:#2563eb;'>Test Maili</h3>
                <p>Bu bir test mailidir. Mail ayarlarınız başarıyla çalışıyor.</p>
                <p style='color:#6b7280;font-size:13px;'>
                    Gönderim zamanı: {DateTime.Now:dd.MM.yyyy HH:mm:ss}
                </p>
                {signatureHtml}
            </div>
        ";

                MailMessage mail = new MailMessage
                {
                    From = new MailAddress(mailFrom, displayName),
                    Subject = dto.Subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mail.To.Add(dto.MailTo);
                await smtp.SendMailAsync(mail);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test maili gönderilemedi: {To}", dto.MailTo);
                errorMessage = ex.Message;
            }

            // Log kaydet
            string? userIdStr = User.FindFirst("userId")?.Value;
            int.TryParse(userIdStr, out int userId);
            await connection.ExecuteAsync(@"
        INSERT INTO MailLog (MailTo, Subject, IsSuccess, ErrorMessage, SentDate, SentByUserID)
        VALUES (@MailTo, @Subject, @IsSuccess, @ErrorMessage, GETDATE(), @SentByUserID)",
                new
                {
                    MailTo = dto.MailTo,
                    Subject = dto.Subject,
                    IsSuccess = success,
                    ErrorMessage = success ? null : errorMessage,
                    SentByUserID = userId > 0 ? userId : (int?)null
                });

            if (success)
                return Ok(ApiResponse.Ok($"Test maili başarıyla gönderildi: {dto.MailTo}"));

            return BadRequest(ApiResponse.Fail($"Mail gönderilemedi: {errorMessage}"));
        }

        // ==================== VALIDATION ====================
        private static List<string> ValidateMailSettings(MailSettingsUpdateDto dto)
        {
            List<string> errors = new();

            if (string.IsNullOrWhiteSpace(dto.MailFrom))
                errors.Add("Gönderici mail adresi zorunludur");
            else if (!IsValidEmail(dto.MailFrom))
                errors.Add("Geçersiz gönderici mail adresi");

            if (string.IsNullOrWhiteSpace(dto.DisplayName))
                errors.Add("Görünen ad zorunludur");

            if (string.IsNullOrWhiteSpace(dto.SmtpHost))
                errors.Add("SMTP sunucu adresi zorunludur");

            if (dto.SmtpPort is < 1 or > 65535)
                errors.Add("SMTP port 1-65535 arasında olmalıdır");

            if (string.IsNullOrWhiteSpace(dto.Username))
                errors.Add("SMTP kullanıcı adı zorunludur");

            if (dto.TimeoutSeconds is < 5 or > 120)
                errors.Add("Timeout 5-120 saniye arasında olmalıdır");

            return errors;
        }

        private static bool IsValidEmail(string email)
        {
            try { _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }
    }

    // Şifre dahil iç kullanım
    internal class MailSettingsInternal
    {
        public int ID { get; set; }
        public string MailFrom { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; }
        public string? Signature { get; set; }
    }
}