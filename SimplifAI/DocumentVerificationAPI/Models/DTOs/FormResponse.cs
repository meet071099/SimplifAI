namespace DocumentVerificationAPI.Models.DTOs
{
    public class FormResponse
    {
        public Guid Id { get; set; }
        public string UniqueUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RecruiterEmail { get; set; } = string.Empty;
        public PersonalInfoResponse? PersonalInfo { get; set; }
        public List<DocumentVerificationResponse> Documents { get; set; } = new();
    }
}