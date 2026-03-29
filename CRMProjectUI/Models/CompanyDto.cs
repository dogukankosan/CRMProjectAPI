using System.ComponentModel.DataAnnotations;

namespace CRMProjectUI.Models
{
    public class CompanyDto
    {
        // ZORUNLU ALANLAR
        [Required(ErrorMessage = "Firma adı zorunludur")]
        [StringLength(100, ErrorMessage = "Firma adı en fazla 100 karakter")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kısa firma adı zorunludur")]
        [StringLength(50, ErrorMessage = "Kısa firma adı en fazla 50 karakter")]
        public string ShortCompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz")]
        [StringLength(100, ErrorMessage = "E-posta en fazla 100 karakter")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon zorunludur")]
        [StringLength(25, ErrorMessage = "Telefon en fazla 25 karakter")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adres zorunludur")]
        [StringLength(300, ErrorMessage = "Adres en fazla 300 karakter")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Web sitesi linki zorunludur")]
        [Url(ErrorMessage = "Geçerli bir URL giriniz")]
        [StringLength(150, ErrorMessage = "Web sitesi linki en fazla 150 karakter")]
        public string WebSiteLink { get; set; } = string.Empty;

        [Required(ErrorMessage = "Web sitesi adı zorunludur")]
        [StringLength(100, ErrorMessage = "Web sitesi adı en fazla 100 karakter")]
        public string WebSiteTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Firma logosu zorunludur")]
        [StringLength(300, ErrorMessage = "Logo yolu en fazla 300 karakter")]
        public string LogoPath { get; set; } = string.Empty;

        [Required(ErrorMessage = "İcon zorunludur")]
        [StringLength(300, ErrorMessage = "Favicon yolu en fazla 300 karakter")]
        public string FaviconPath { get; set; } = string.Empty;

        // OPSİYONEL ALANLAR
        [StringLength(1000, ErrorMessage = "Google Maps embed en fazla 1000 karakter")]
        public string? GoogleMapsEmbed { get; set; }  // zorunlu değil

        [StringLength(200)]
        public string? Slogan { get; set; }

        [StringLength(25)]
        public string? Phone2 { get; set; }

        [StringLength(70, ErrorMessage = "Meta başlık SEO için max 70 karakter")]
        public string? MetaTitle { get; set; }

        [StringLength(160, ErrorMessage = "Meta açıklama SEO için max 160 karakter")]
        public string? MetaDescription { get; set; }

        [StringLength(300)]
        public string? MetaKeywords { get; set; }

        [StringLength(500)]
        public string? SectorDescription { get; set; }

        [Url(ErrorMessage = "Geçerli bir Canonical URL giriniz")]
        [StringLength(200)]
        public string? CanonicalUrl { get; set; }

        [Url(ErrorMessage = "Geçerli bir Instagram linki giriniz")]
        [StringLength(200)]
        public string? InstagramLink { get; set; }

        [Url(ErrorMessage = "Geçerli bir LinkedIn linki giriniz")]
        [StringLength(200)]
        public string? LinkedinLink { get; set; }

        [Url(ErrorMessage = "Geçerli bir YouTube linki giriniz")]
        [StringLength(200)]
        public string? YoutubeLink { get; set; }

        [Url(ErrorMessage = "Geçerli bir harici web linki giriniz")]
        [StringLength(200)]
        public string? ExternalWebLink { get; set; }

        [StringLength(500)]
        public string? AboutUsShort { get; set; }

        public string? AboutUs { get; set; }

        [StringLength(1000)]
        public string? Vision { get; set; }

        [StringLength(1000)]
        public string? Mission { get; set; }

        [StringLength(200)]
        public string? WorkingHours { get; set; }


        public short? FoundedYear { get; set; }

        // Audit
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}