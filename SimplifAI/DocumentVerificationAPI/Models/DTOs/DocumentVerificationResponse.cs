namespace DocumentVerificationAPI.Models.DTOs
{
    public class DocumentVerificationResponse
    {
        public Guid DocumentId { get; set; }
        public string VerificationStatus { get; set; } = string.Empty;
        public decimal? ConfidenceScore { get; set; }
        public bool IsBlurred { get; set; }
        public bool IsCorrectType { get; set; }
        public string StatusColor { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool RequiresUserConfirmation { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}