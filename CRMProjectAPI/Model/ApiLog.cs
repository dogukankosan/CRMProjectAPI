namespace CRMProjectAPI.Models
{
    public class ApiLog
    {
        public long ID { get; set; }

        // İSTEK BİLGİLERİ
        public string HttpMethod { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string? QueryString { get; set; }
        public string? RequestBody { get; set; }
        public string? RequestHeaders { get; set; }

        // YANIT BİLGİLERİ
        public int StatusCode { get; set; }
        public string? ResponseBody { get; set; }
        public int? ResponseTime { get; set; }

        // HATA DETAYI
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorStackTrace { get; set; }
        public string? ErrorType { get; set; }

        // KULLANICI / İSTEK SAHİBİ
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? ApiKey { get; set; }

        // SİSTEM
        public DateTime CreatedDate { get; set; }
    }
}
