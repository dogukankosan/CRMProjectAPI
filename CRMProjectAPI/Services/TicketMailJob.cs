using CRMProjectAPI.Data;
using Dapper;
using Hangfire;

namespace CRMProjectAPI.Services
{
    public class TicketMailJob : ITicketMailJob
    {
        private readonly DapperContext _context;
        private readonly IMailService _mailService;
        private readonly ILogger<TicketMailJob> _logger;

        public TicketMailJob(
            DapperContext context,
            IMailService mailService,
            ILogger<TicketMailJob> logger)
        {
            _context = context;
            _mailService = mailService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task SendTicketClosedMailAsync(int ticketId, int customerId, byte status)
        {
            using var connection = _context.CreateConnection();

            var ticket = await connection.QueryFirstOrDefaultAsync(@"
                SELECT
                    t.TicketNo, t.Title, t.WorkingMinute,
                    t.OpenedDate, t.ResolvedDate, t.SolutionNote, t.CancelReason,
                    ISNULL(au.FullName, au.Username) AS AssignedToName,
                    c.CustomerName
                FROM Tickets t WITH (NOLOCK)
                LEFT JOIN Users     au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
                LEFT JOIN Customers c  WITH (NOLOCK) ON t.CustomerID       = c.ID
                WHERE t.ID = @ID",
                new { ID = ticketId });

            if (ticket == null) return;

            const string usersSql = @"
                SELECT u.EMailAddress, u.FullName
                FROM Users u WITH (NOLOCK)
                INNER JOIN Customers c WITH (NOLOCK) ON u.CompanyID = c.ID
                WHERE u.CompanyID = @CompanyID
                  AND u.Status    = 1
                  AND u.SendEmail = 1
                  AND c.Status    = 1
            ";
            IEnumerable<dynamic> recipients =
                await connection.QueryAsync(usersSql, new { CompanyID = customerId });

            if (!recipients.Any()) return;

            int totalMin = (int)(ticket.WorkingMinute ?? 0);
            string workingDisplay = totalMin == 0 ? "Belirtilmemiş"
                : totalMin < 60 ? $"{totalMin} dakika"
                : $"{totalMin / 60} saat {totalMin % 60} dakika";

            string openedDate = ticket.OpenedDate != null
                ? ((DateTime)ticket.OpenedDate).ToString("dd.MM.yyyy HH:mm") : "-";
            string resolvedDate = ticket.ResolvedDate != null
                ? ((DateTime)ticket.ResolvedDate).ToString("dd.MM.yyyy HH:mm") : "-";

            string statusText, statusColor, headerBg, borderColor, statusEmoji;
            switch (status)
            {
                case 2:
                    statusText = "Başarıyla Çözüldü"; statusEmoji = "✅";
                    statusColor = "#16a34a"; headerBg = "#f0fdf4"; borderColor = "#bbf7d0";
                    break;
                case 3:
                    statusText = "Çözülemedi"; statusEmoji = "❌";
                    statusColor = "#dc2626"; headerBg = "#fef2f2"; borderColor = "#fecaca";
                    break;
                case 6:
                    statusText = "İptal Edildi"; statusEmoji = "🚫";
                    statusColor = "#6b7280"; headerBg = "#f1f5f9"; borderColor = "#e2e8f0";
                    break;
                default:
                    statusText = "Güncellendi"; statusEmoji = "ℹ️";
                    statusColor = "#2563eb"; headerBg = "#eff6ff"; borderColor = "#bfdbfe";
                    break;
            }

            string subject = $"Destek Talebi {statusText} — {ticket.TicketNo}";

            bool hasMaintenanceContract = await connection.ExecuteScalarAsync<bool>(
                "SELECT HasMaintenanceContract FROM Customers WHERE ID = @ID",
                new { ID = customerId });

            string maintenanceWarning = !hasMaintenanceContract
                ? @"<div style='background:#fffbeb;border:1px solid #fde68a;border-radius:10px;padding:14px 18px;margin-bottom:20px;'>
                        <p style='margin:0;color:#92400e;font-size:.9rem;'>
                            ⚠️ <strong>Önemli Bilgi:</strong> Bakım anlaşmanız bulunmamaktadır. 
                            Bu destek talebi için tarafınıza daha sonra fatura kesilebilir.
                        </p>
                    </div>"
                : string.Empty;

            string assignedTo = string.IsNullOrEmpty((string?)ticket.AssignedToName)
                ? "Belirtilmemiş" : (string)ticket.AssignedToName;

            string solutionBlock = !string.IsNullOrEmpty((string?)ticket.SolutionNote)
                ? $@"<tr>
                       <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;width:40%;'>Çözüm Notu</td>
                       <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{ticket.SolutionNote}</td>
                     </tr>"
                : string.Empty;

            string cancelBlock = status == 6 && !string.IsNullOrEmpty((string?)ticket.CancelReason)
                ? $@"<tr>
                       <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;width:40%;'>İptal Nedeni</td>
                       <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;color:#dc2626;font-weight:600;'>{ticket.CancelReason}</td>
                     </tr>"
                : string.Empty;

            string body = $@"
                <div style='background:{headerBg};border:1px solid {borderColor};border-radius:12px;padding:20px 24px;margin-bottom:20px;text-align:center;'>
                    <div style='font-size:2rem;margin-bottom:8px;'>{statusEmoji}</div>
                    <h2 style='margin:0;color:{statusColor};font-size:1.2rem;'>Destek Talebi {statusText}</h2>
                    <p style='margin:6px 0 0;color:#6b7280;font-size:.9rem;'>
                        <strong style='color:#0f1117;'>{ticket.TicketNo}</strong> numaralı talep güncellendi.
                    </p>
                </div>
                {maintenanceWarning}
                <table style='width:100%;border-collapse:collapse;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;margin-bottom:20px;'>
                    <tr style='background:#f7f8fc;'>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;width:40%;'>Talep No</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-family:monospace;font-weight:700;color:#2563eb;'>{ticket.TicketNo}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Firma</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{ticket.CustomerName}</td>
                    </tr>
                    <tr style='background:#f7f8fc;'>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Konu</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{ticket.Title}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>İşlemi Alan</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{assignedTo}</td>
                    </tr>
                    <tr style='background:#f7f8fc;'>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Açılış Tarihi</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{openedDate}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Kapanış Tarihi</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;'>{resolvedDate}</td>
                    </tr>
                    <tr style='background:#f7f8fc;'>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Toplam Süre</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#0f1117;'>⏱ {workingDisplay}</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;font-weight:700;color:#4a4f5e;font-size:.85rem;'>Sonuç</td>
                        <td style='padding:10px 16px;border-bottom:1px solid #e2e8f0;color:{statusColor};font-weight:700;'>{statusEmoji} {statusText}</td>
                    </tr>
                    {solutionBlock}
                    {cancelBlock}
                </table>
                <p style='color:#9ca3af;font-size:.78rem;text-align:center;margin:0;'>
                    Bu mail otomatik olarak gönderilmiştir. Lütfen yanıtlamayınız.
                </p>
            ";

            foreach (dynamic recipient in recipients)
            {
                try
                {
                    await _mailService.SendAsync(
                        (string)recipient.EMailAddress,
                        (string)recipient.FullName,
                        subject,
                        body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mail gönderilemedi: {Email}, TicketID: {TicketID}",
                        (string)recipient.EMailAddress, ticketId);
                }
            }
        }
    }
}