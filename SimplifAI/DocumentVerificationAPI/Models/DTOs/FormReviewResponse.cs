namespace DocumentVerificationAPI.Models.DTOs
{
    public class FormReviewResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string RecruiterEmail { get; set; } = string.Empty;
        public PersonalInfoResponse? PersonalInfo { get; set; }
        public List<DocumentReviewResponse> Documents { get; set; } = new List<DocumentReviewResponse>();
    }

    public class DocumentReviewResponse
    {
        public Guid Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string VerificationStatus { get; set; } = string.Empty;
        public decimal? ConfidenceScore { get; set; }
        public bool IsBlurred { get; set; }
        public bool IsCorrectType { get; set; }
        public string StatusColor { get; set; } = string.Empty;
        public string? VerificationDetails { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }

    public class DocumentVerificationSummary
    {
        public int TotalDocuments { get; set; }
        public int VerifiedDocuments { get; set; }
        public int DocumentsWithIssues { get; set; }
        public int LowConfidenceDocuments { get; set; }
        public int MediumConfidenceDocuments { get; set; }
        public int HighConfidenceDocuments { get; set; }
    }
}