namespace CRMProjectAPI.Helpers
{
    // Generic — data dönen response'lar için
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public static ApiResponse<T> Ok(T data, string? message = null) => new()
        {
            Success = true,
            StatusCode = 200,
            Message = message ?? "İşlem başarılı",
            Data = data
        };
        public static ApiResponse<T> Fail(string message, int statusCode = 400) => new()
        {
            Success = false,
            StatusCode = statusCode,
            Message = message
        };
        public static ApiResponse<T> NotFound(string? message = null) => new()
        {
            Success = false,
            StatusCode = 404,
            Message = message ?? "Kayıt bulunamadı"
        };
    }

    // Non-generic — sadece mesaj dönen response'lar için
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public static ApiResponse Ok(string? message = null) => new()
        {
            Success = true,
            StatusCode = 200,
            Message = message ?? "İşlem başarılı"
        };
        public static ApiResponse Fail(string message, int statusCode = 400) => new()
        {
            Success = false,
            StatusCode = statusCode,
            Message = message
        };
        public static ApiResponse Fail(List<string> errors, string? message = null) => new()
        {
            Success = false,
            StatusCode = 400,
            Message = message ?? "Doğrulama hatası",
            Errors = errors
        };
        public static ApiResponse NotFound(string? message = null) => new()
        {
            Success = false,
            StatusCode = 404,
            Message = message ?? "Kayıt bulunamadı"
        };
    }
}