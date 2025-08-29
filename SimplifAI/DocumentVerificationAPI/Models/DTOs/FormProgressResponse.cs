namespace DocumentVerificationAPI.Models.DTOs
{
    public class FormProgressResponse
    {
        public Guid FormId { get; set; }
        public bool PersonalInfoComplete { get; set; }
        public bool DocumentsComplete { get; set; }
        public bool IsComplete { get; set; }
        public int CompletionPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }
}