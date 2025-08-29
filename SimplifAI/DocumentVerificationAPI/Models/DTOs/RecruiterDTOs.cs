using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models.DTOs
{
    public class PaginatedFormsResponse
    {
        public List<FormSummaryResponse> Forms { get; set; } = new List<FormSummaryResponse>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class FormSummaryResponse
    {
        public Guid Id { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DocumentCount { get; set; }
        public bool HasLowConfidenceDocuments { get; set; }
        public string RecruiterEmail { get; set; } = string.Empty;
    }





    public class FormStatusUpdateRequest
    {
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? ReviewNotes { get; set; }
    }

    public class FormStatusUpdateResponse
    {
        public Guid FormId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ReviewNotes { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DashboardStatsResponse
    {
        public int TotalForms { get; set; }
        public int SubmittedForms { get; set; }
        public int ApprovedForms { get; set; }
        public int RejectedForms { get; set; }
        public int PendingReview { get; set; }
        public int FormsWithLowConfidenceDocuments { get; set; }
    }

    public class DashboardStats
    {
        public int TotalForms { get; set; }
        public int SubmittedForms { get; set; }
        public int ApprovedForms { get; set; }
        public int RejectedForms { get; set; }
        public int PendingReview { get; set; }
        public int FormsWithLowConfidenceDocuments { get; set; }
    }

    public class PaginatedFormsResult
    {
        public List<Form> Forms { get; set; } = new List<Form>();
        public int TotalCount { get; set; }
    }
}