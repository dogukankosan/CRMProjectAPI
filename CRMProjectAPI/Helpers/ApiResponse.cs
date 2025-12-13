namespace CRMProjectAPI.Helpers
{
    /// <summary>
    /// Standart API yanıt modeli
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Başarılı yanıt
        public static ApiResponse<T> Ok(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = 200,
                Message = message ?? "İşlem başarılı",
                Data = data
            };
        }
        // Oluşturuldu (201)
        public static ApiResponse<T> Created(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = 201,
                Message = message ?? "Kayıt oluşturuldu",
                Data = data
            };
        }
        // Hata yanıtı
        public static ApiResponse<T> Fail(string message, int statusCode = 400)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = statusCode,
                Message = message
            };
        }
        // Çoklu hata
        public static ApiResponse<T> Fail(List<string> errors, string? message = null, int statusCode = 400)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = statusCode,
                Message = message ?? "Doğrulama hatası",
                Errors = errors
            };
        }
        // 404 Not Found
        public static ApiResponse<T> NotFound(string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 404,
                Message = message ?? "Kayıt bulunamadı"
            };
        }
        // 401 Unauthorized
        public static ApiResponse<T> Unauthorized(string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 401,
                Message = message ?? "Yetkisiz erişim"
            };
        }
        // 500 Server Error
        public static ApiResponse<T> ServerError(string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 500,
                Message = message ?? "Sunucu hatası oluştu"
            };
        }
    }
    // Data olmayan yanıtlar için
    public class ApiResponse : ApiResponse<object>
    {
        public new static ApiResponse Ok(string? message = null)
        {
            return new ApiResponse
            {
                Success = true,
                StatusCode = 200,
                Message = message ?? "İşlem başarılı"
            };
        }
        public new static ApiResponse Fail(string message, int statusCode = 400)
        {
            return new ApiResponse
            {
                Success = false,
                StatusCode = statusCode,
                Message = message
            };
        }
        public new static ApiResponse NotFound(string? message = null)
        {
            return new ApiResponse
            {
                Success = false,
                StatusCode = 404,
                Message = message ?? "Kayıt bulunamadı"
            };
        }
    }
}