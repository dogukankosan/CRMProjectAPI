using CRMProjectAPI.Data;
using Dapper;
using System.Net;
using System.Net.Mail;

namespace CRMProjectAPI.Services
{
    // ==========================================
    // INTERFACE
    // ==========================================
    public interface IMailService
    {
        Task SendAsync(string toEmail, string toName, string subject, string body);
    }

    // ==========================================
    // IMPLEMENTATION
    // ==========================================
    public class MailService : IMailService
    {
        private readonly DapperContext _context;
        private readonly IEncryptionService _encryption;
        private readonly ILogger<MailService> _logger;

        public MailService(
            DapperContext context,
            IEncryptionService encryption,
            ILogger<MailService> logger)
        {
            _context = context;
            _encryption = encryption;
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string toName, string subject, string body)
        {
            // ── Mail ayarlarını DB'den çek ──────────────────────────────────
            const string sql = @"
                SELECT
                    MailFrom, DisplayName, SmtpHost, SmtpPort,
                    EnableSsl, Username, Password, TimeoutSeconds,
                    Signature, IsActive
                FROM MailSettings WITH (NOLOCK)
                WHERE ID = 1
            ";

            using var connection = _context.CreateConnection();
            var settings = await connection.QueryFirstOrDefaultAsync(sql);

            if (settings == null)
            {
                _logger.LogWarning("Mail ayarları bulunamadı, mail gönderilemiyor");
                return;
            }

            if (!(bool)settings.IsActive)
            {
                _logger.LogInformation("Mail servisi pasif, mail gönderilmedi: {To}", toEmail);
                return;
            }

            // Şifreyi çöz
            string smtpPassword = _encryption.Decrypt((string)settings.Password);
            if (string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogError("Mail şifresi çözülemedi");
                return;
            }

            // Signature ekle
            string signatureHtml = !string.IsNullOrEmpty((string?)settings.Signature)
                ? $"<hr style='border:none;border-top:1px solid #e2e8f0;margin:24px 0;'/>{settings.Signature}"
                : string.Empty;

            string fullBody = $@"
                <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
                    {body}
                    {signatureHtml}
                </div>
            ";

            // ── Mail gönder ─────────────────────────────────────────────────
            bool success = false;
            string? errorMessage = null;

            try
            {
                using SmtpClient smtp = new SmtpClient((string)settings.SmtpHost, (int)settings.SmtpPort)
                {
                    EnableSsl = (bool)settings.EnableSsl,
                    Credentials = new NetworkCredential((string)settings.Username, smtpPassword),
                    Timeout = (int)settings.TimeoutSeconds * 1000,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                MailMessage mail = new MailMessage
                {
                    From = new MailAddress((string)settings.MailFrom, (string)settings.DisplayName),
                    Subject = subject,
                    Body = fullBody,
                    IsBodyHtml = true
                };
                mail.To.Add(new MailAddress(toEmail, toName));

                await smtp.SendMailAsync(mail);
                success = true;

                _logger.LogInformation("Mail gönderildi: {To} — {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.LogError(ex, "Mail gönderilemedi: {To} — {Subject}", toEmail, subject);
                throw; // caller handle etsin
            }
            finally
            {
                // Sadece hatalı durumları logla
                if (!success)
                {
                    try
                    {
                        await connection.ExecuteAsync(@"
                INSERT INTO MailLog (MailTo, Subject, IsSuccess, ErrorMessage, SentDate)
                VALUES (@MailTo, @Subject, @IsSuccess, @ErrorMessage, GETDATE())",
                            new
                            {
                                MailTo = toEmail,
                                Subject = subject,
                                IsSuccess = false,
                                ErrorMessage = errorMessage
                            });
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogWarning(logEx, "Mail log kaydedilemedi");
                    }
                }
            }
        }
    }
}