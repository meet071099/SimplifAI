namespace DocumentVerificationAPI.Models
{
    public class DocumentVerificationResult
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
        public string FilePath { get; set; } = string.Empty;
        public string VerificationDetails { get; set; } = string.Empty;
        
        // Azure AI Foundry authenticity verification properties
        public string? AuthenticityResult { get; set; }
        public bool AuthenticityVerified { get; set; }
        public bool AIFoundryUsed { get; set; }

        public PromptResponse? promptResponse { get; set; }
    }
}