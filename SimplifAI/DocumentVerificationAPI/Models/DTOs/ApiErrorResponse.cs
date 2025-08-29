namespace DocumentVerificationAPI.Models.DTOs
{
    public class ApiErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
        public string? TraceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}