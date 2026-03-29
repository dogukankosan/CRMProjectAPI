using CRMProjectAPI.Models;

namespace CRMProjectAPI.Validations
{
    public static class TicketValidation
    {
        private static readonly HashSet<byte> ValidStatuses = new() { 0, 1, 2, 3, 4, 5, 6 };

        private static readonly HashSet<byte> ValidPriorities = new() { 1, 2, 3, 4 };

        // ==========================================
        // TICKET OLUŞTURMA
        // ==========================================
        public static List<string> ValidateCreate(TicketCreateDto dto)
        {
            List<string> errors = new();

            if (dto.CustomerID <= 0)
                errors.Add("Müşteri bilgisi zorunludur");

            if (dto.LogoProductID <= 0)
                errors.Add("Müşteri ürünü seçimi zorunludur");

            if (dto.CreatedByUserID <= 0)
                errors.Add("Kullanıcı bilgisi zorunludur");

            if (string.IsNullOrWhiteSpace(dto.Title))
                errors.Add("Başlık zorunludur");
            else if (dto.Title.Length > 200)
                errors.Add("Başlık en fazla 200 karakter olabilir");

            if (string.IsNullOrWhiteSpace(dto.Description))
                errors.Add("Sorun açıklaması zorunludur");

            if (!ValidPriorities.Contains(dto.Priority))
                errors.Add("Geçersiz öncelik değeri (1=Düşük, 2=Normal, 3=Yüksek, 4=Kritik)");

            return errors;
        }

        // ==========================================
        // DURUM GÜNCELLEME
        // ==========================================
        public static List<string> ValidateStatusUpdate(TicketStatusUpdateDto dto)
        {
            List<string> errors = new();

            if (!ValidStatuses.Contains(dto.Status))
                errors.Add("Geçersiz durum değeri (0-6 arası olmalıdır)");

            // Kapatma durumlarında çözüm notu zorunlu
            if ((dto.Status == 2 || dto.Status == 3) && string.IsNullOrWhiteSpace(dto.SolutionNote))
                errors.Add("Ticket kapatılırken çözüm notu zorunludur");

            // İptal durumunda neden zorunlu
            if (dto.Status == 6 && string.IsNullOrWhiteSpace(dto.CancelReason))
                errors.Add("İptal için neden zorunludur");

            if (dto.WorkingMinute < 0)
                errors.Add("Çalışma süresi negatif olamaz");

            return errors;
        }
        // ==========================================
        // DEVİR
        // ==========================================
        public static List<string> ValidateAssign(TicketAssignDto dto)
        {
            List<string> errors = new();

            if (dto.AssignedToUserID <= 0)
                errors.Add("Atanacak kullanıcı bilgisi zorunludur");

            return errors;
        }

        // ==========================================
        // YORUM EKLEME
        // ==========================================
        public static List<string> ValidateComment(TicketCommentCreateDto dto)
        {
            List<string> errors = new();

            if (dto.TicketID <= 0)
                errors.Add("Ticket bilgisi zorunludur");

            if (dto.UserID <= 0)
                errors.Add("Kullanıcı bilgisi zorunludur");

            if (string.IsNullOrWhiteSpace(dto.Comment))
                errors.Add("Yorum içeriği zorunludur");
            else if (dto.Comment.Length > 4000)
                errors.Add("Yorum en fazla 4000 karakter olabilir");

            return errors;
        }
    }
}