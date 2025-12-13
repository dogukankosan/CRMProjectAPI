namespace CRMProjectAPI.Models
{
    public class CompanyDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string ShortCompanyName { get; set; } = string.Empty;
        public string? Slogan { get; set; }
        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
        public string? Phone2 { get; set; }
        public string? Address { get; set; }

        public string? GoogleMapsEmbed { get; set; }

        public string WebSiteLink { get; set; } = string.Empty;
        public string WebSiteTitle { get; set; } = string.Empty;

        public string? LogoPath { get; set; }
        public string? FaviconPath { get; set; }

        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }

        public string? SectorDescription { get; set; }
        public string? CanonicalUrl { get; set; }

        public string? InstagramLink { get; set; }
        public string? LinkedinLink { get; set; }
        public string? YoutubeLink { get; set; }
        public string? ExternalWebLink { get; set; }

        public string? AboutUsShort { get; set; }
        public string? AboutUs { get; set; }

        public string? Vision { get; set; }
        public string? Mission { get; set; }

        public string? WorkingHours { get; set; }
        public short? FoundedYear { get; set; }
    }
}