// CRMProjectAPI/Models/UiErrorDto.cs
namespace CRMProjectAPI.Models
{
    public class UiErrorDto
    {
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
        public string? Page { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}