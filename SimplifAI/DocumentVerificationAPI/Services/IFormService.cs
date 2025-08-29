using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;

namespace DocumentVerificationAPI.Services
{
    public class FormSubmissionRequest
    {
        public Guid FormId { get; set; }
        public PersonalInfo PersonalInfo { get; set; } = null!;
        public List<Document> Documents { get; set; } = new();
    }

    public interface IFormService
    {
        /// <summary>
        /// Creates a new form with a unique URL
        /// </summary>
        /// <param name="request">Form creation request</param>
        /// <returns>The created form</returns>
        Task<Form> CreateFormAsync(FormCreationRequest request);

        /// <summary>
        /// Gets a form by its unique URL
        /// </summary>
        /// <param name="uniqueUrl">The unique URL identifier</param>
        /// <returns>The form if found, null otherwise</returns>
        Task<Form?> GetFormByUrlAsync(string uniqueUrl);

        /// <summary>
        /// Gets a form by its ID with related data
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <param name="includePersonalInfo">Whether to include personal info</param>
        /// <param name="includeDocuments">Whether to include documents</param>
        /// <returns>The form if found, null otherwise</returns>
        Task<Form?> GetFormByIdAsync(Guid formId, bool includePersonalInfo = false, bool includeDocuments = false);

        /// <summary>
        /// Saves personal information for a form
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <param name="personalInfo">The personal information to save</param>
        /// <returns>The saved personal information</returns>
        Task<PersonalInfo> SavePersonalInfoAsync(Guid formId, PersonalInfo personalInfo);

        /// <summary>
        /// Adds a document to a form
        /// </summary>
        /// <param name="document">The document to add</param>
        /// <returns>The saved document</returns>
        Task<Document> AddDocumentAsync(Document document);

        /// <summary>
        /// Updates a document's verification status
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <param name="verificationResult">The verification result</param>
        /// <returns>The updated document</returns>
        Task<Document> UpdateDocumentVerificationAsync(Guid documentId, DocumentVerificationResult verificationResult);

        /// <summary>
        /// Submits a form (marks it as submitted)
        /// </summary>
        /// <param name="formId">The form ID to submit</param>
        /// <returns>The updated form</returns>
        Task<Form> SubmitFormAsync(Guid formId);

        /// <summary>
        /// Gets all forms for a recruiter
        /// </summary>
        /// <param name="recruiterEmail">The recruiter's email</param>
        /// <param name="includePersonalInfo">Whether to include personal info</param>
        /// <param name="includeDocuments">Whether to include documents</param>
        /// <returns>List of forms for the recruiter</returns>
        Task<IEnumerable<Form>> GetFormsByRecruiterAsync(string recruiterEmail, bool includePersonalInfo = false, bool includeDocuments = false);

        /// <summary>
        /// Validates if a form can be submitted
        /// </summary>
        /// <param name="formId">The form ID to validate</param>
        /// <returns>True if form is valid for submission, false otherwise</returns>
        Task<bool> ValidateFormForSubmissionAsync(Guid formId);

        /// <summary>
        /// Gets submitted forms with filtering and pagination
        /// </summary>
        /// <param name="recruiterEmail">Optional filter by recruiter email</param>
        /// <param name="status">Optional filter by form status</param>
        /// <param name="searchTerm">Optional search term for candidate name or email</param>
        /// <param name="page">Page number for pagination</param>
        /// <param name="pageSize">Page size for pagination</param>
        /// <returns>Paginated list of submitted forms</returns>
        Task<Models.DTOs.PaginatedFormsResult> GetSubmittedFormsAsync(string? recruiterEmail, string? status, string? searchTerm, int page, int pageSize);

        /// <summary>
        /// Updates form status (approve/reject)
        /// </summary>
        /// <param name="formId">The form ID to update</param>
        /// <param name="status">The new status</param>
        /// <param name="reviewNotes">Optional review notes</param>
        /// <returns>The updated form</returns>
        Task<Form> UpdateFormStatusAsync(Guid formId, string status, string? reviewNotes);

        /// <summary>
        /// Gets dashboard statistics
        /// </summary>
        /// <param name="recruiterEmail">Optional filter by recruiter email</param>
        /// <returns>Dashboard statistics</returns>
        Task<Models.DTOs.DashboardStats> GetDashboardStatsAsync(string? recruiterEmail);

        /// <summary>
        /// Updates personal information for a form
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <param name="personalInfoRequest">The personal information request</param>
        /// <returns>The updated personal information</returns>
        Task<PersonalInfo> UpdatePersonalInfoAsync(Guid formId, Models.DTOs.PersonalInfoRequest personalInfoRequest);

        /// <summary>
        /// Gets a form with all its details
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <returns>The complete form with all details</returns>
        Task<Form?> GetFormWithDetailsAsync(Guid formId);

        /// <summary>
        /// Validates if a form is complete
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <returns>True if form is complete, false otherwise</returns>
        Task<bool> ValidateFormCompletionAsync(Guid formId);

        /// <summary>
        /// Gets a document by its ID
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>The document if found, null otherwise</returns>
        Task<Document?> GetDocumentByIdAsync(Guid documentId);

        /// <summary>
        /// Gets personal information for a form
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <returns>The personal information if found, null otherwise</returns>
        Task<PersonalInfo?> GetPersonalInfoAsync(Guid formId);
    }
}