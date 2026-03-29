using System.ComponentModel.DataAnnotations;

namespace CRMProjectUI.Models
{
    public class ProfilUpdateDto
    {
        [Required(ErrorMessage = "Ad Soyad zorunludur")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "E-posta zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin")]
        public string EMailAddress { get; set; } = "";

        public string? PhoneNumber { get; set; }
        public string? Picture { get; set; }

        // Boş bırakılırsa şifre değişmez
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
        public string? NewPassword { get; set; }

        [Compare("NewPassword", ErrorMessage = "Şifreler eşleşmiyor")]
        public string? NewPasswordConfirm { get; set; }
    }
}