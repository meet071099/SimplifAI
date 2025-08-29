namespace DocumentVerificationAPI.Models.DTOs
{
    public class FormSubmissionResponse
    {
        public Guid FormId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public List<DocumentVerificationResponse> Documents { get; set; } = new();
        public PersonalInfoResponse? PersonalInfo { get; set; }
    }
}