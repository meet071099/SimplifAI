namespace DocumentVerificationAPI.Models.DTOs
{
    public class RecruiterDashboardResponse
    {
        public string RecruiterEmail { get; set; } = string.Empty;
        public int TotalForms { get; set; }
        public int PendingForms { get; set; }
        public int SubmittedForms { get; set; }
        public int TotalDocuments { get; set; }
        public int DocumentsRequiringReview { get; set; }
        public List<FormResponse> RecentSubmissions { get; set; } = new();
    }
}