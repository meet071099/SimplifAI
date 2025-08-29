using Microsoft.AspNetCore.Mvc;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;
using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecruiterController : ControllerBase
    {
        private readonly IFormService _formService;
        private readonly ILogger<RecruiterController> _logger;

        public RecruiterController(
            IFormService formService,
            ILogger<RecruiterController> logger)
        {
            _formService = formService;
            _logger = logger;
        }

        /// <summary>
        /// Gets all submitted forms for recruiter dashboard
        /// </summary>
        /// <param name="recruiterEmail">Optional filter by recruiter email</param>
        /// <param name="status">Optional filter by form status</param>
        /// <param name="searchTerm">Optional search term for candidate name or email</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 10)</param>
        /// <returns>Paginated list of submitted forms</returns>
        [HttpGet("forms")]
        [ProducesResponseType(typeof(PaginatedFormsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedFormsResponse>> GetSubmittedForms(
            [FromQuery] string? recruiterEmail = null,
            [FromQuery] string? status = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Getting submitted forms - RecruiterEmail: {RecruiterEmail}, Status: {Status}, SearchTerm: {SearchTerm}, Page: {Page}, PageSize: {PageSize}",
                    recruiterEmail, status, searchTerm, page, pageSize);

                var result = await _formService.GetSubmittedFormsAsync(recruiterEmail, status, searchTerm, page, pageSize);

                var response = new PaginatedFormsResponse
                {
                    Forms = result.Forms.Select(MapToFormSummaryResponse).ToList(),
                    TotalCount = result.TotalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize)
                };

                _logger.LogInformation("Retrieved {Count} forms out of {TotalCount} total", result.Forms.Count, result.TotalCount);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting submitted forms");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving forms",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets detailed form information for review
        /// </summary>
        /// <param name="formId">The form ID to review</param>
        /// <returns>Detailed form information with documents</returns>
        [HttpGet("forms/{formId:guid}")]
        [ProducesResponseType(typeof(FormReviewResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FormReviewResponse>> GetFormForReview(Guid formId)
        {
            try
            {
                _logger.LogInformation("Getting form for review: {FormId}", formId);

                var form = await _formService.GetFormByIdAsync(formId, includePersonalInfo: true, includeDocuments: true);

                if (form == null)
                {
                    _logger.LogWarning("Form not found for review: {FormId}", formId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var response = MapToFormReviewResponse(form);

                _logger.LogInformation("Form retrieved for review: {FormId}", formId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting form for review: {FormId}", formId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving the form",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Updates form status (approve/reject)
        /// </summary>
        /// <param name="formId">The form ID to update</param>
        /// <param name="request">Status update request</param>
        /// <returns>Updated form status</returns>
        [HttpPut("forms/{formId:guid}/status")]
        [ProducesResponseType(typeof(FormStatusUpdateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FormStatusUpdateResponse>> UpdateFormStatus(Guid formId, [FromBody] FormStatusUpdateRequest request)
        {
            try
            {
                _logger.LogInformation("Updating form status: {FormId} to {Status}", formId, request.Status);

                var form = await _formService.GetFormByIdAsync(formId);
                if (form == null)
                {
                    _logger.LogWarning("Form not found for status update: {FormId}", formId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Validate status transition
                if (!IsValidStatusTransition(form.Status, request.Status))
                {
                    _logger.LogWarning("Invalid status transition from {CurrentStatus} to {NewStatus} for form: {FormId}",
                        form.Status, request.Status, formId);
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = $"Invalid status transition from {form.Status} to {request.Status}",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var updatedForm = await _formService.UpdateFormStatusAsync(formId, request.Status, request.ReviewNotes);

                var response = new FormStatusUpdateResponse
                {
                    FormId = updatedForm.Id,
                    Status = updatedForm.Status,
                    ReviewNotes = request.ReviewNotes,
                    UpdatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Form status updated successfully: {FormId} to {Status}", formId, request.Status);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating form status: {FormId}", formId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while updating the form status",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets dashboard statistics
        /// </summary>
        /// <param name="recruiterEmail">Optional filter by recruiter email</param>
        /// <returns>Dashboard statistics</returns>
        [HttpGet("dashboard/stats")]
        [ProducesResponseType(typeof(DashboardStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DashboardStatsResponse>> GetDashboardStats([FromQuery] string? recruiterEmail = null)
        {
            try
            {
                _logger.LogInformation("Getting dashboard stats for recruiter: {RecruiterEmail}", recruiterEmail);

                var stats = await _formService.GetDashboardStatsAsync(recruiterEmail);

                var response = new DashboardStatsResponse
                {
                    TotalForms = stats.TotalForms,
                    SubmittedForms = stats.SubmittedForms,
                    ApprovedForms = stats.ApprovedForms,
                    RejectedForms = stats.RejectedForms,
                    PendingReview = stats.PendingReview,
                    FormsWithLowConfidenceDocuments = stats.FormsWithLowConfidenceDocuments
                };

                _logger.LogInformation("Dashboard stats retrieved successfully");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving dashboard statistics",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        #region Private Helper Methods

        private FormSummaryResponse MapToFormSummaryResponse(Form form)
        {
            return new FormSummaryResponse
            {
                Id = form.Id,
                CandidateName = form.PersonalInfo != null ? $"{form.PersonalInfo.FirstName} {form.PersonalInfo.LastName}" : "N/A",
                CandidateEmail = form.PersonalInfo?.Email ?? "N/A",
                Status = form.Status,
                SubmittedAt = form.SubmittedAt,
                CreatedAt = form.CreatedAt,
                DocumentCount = form.Documents?.Count ?? 0,
                HasLowConfidenceDocuments = form.Documents?.Any(d => d.ConfidenceScore < 50) ?? false,
                RecruiterEmail = form.RecruiterEmail
            };
        }

        private FormReviewResponse MapToFormReviewResponse(Form form)
        {
            return new FormReviewResponse
            {
                Id = form.Id,
                Status = form.Status,
                CreatedAt = form.CreatedAt,
                SubmittedAt = form.SubmittedAt,
                RecruiterEmail = form.RecruiterEmail,
                PersonalInfo = form.PersonalInfo != null ? MapToPersonalInfoResponse(form.PersonalInfo) : null,
                Documents = form.Documents?.Select(MapToDocumentReviewResponse).ToList() ?? new List<DocumentReviewResponse>()
            };
        }

        private PersonalInfoResponse MapToPersonalInfoResponse(PersonalInfo personalInfo)
        {
            return new PersonalInfoResponse
            {
                Id = personalInfo.Id,
                FirstName = personalInfo.FirstName,
                LastName = personalInfo.LastName,
                Email = personalInfo.Email,
                Phone = personalInfo.Phone,
                Address = personalInfo.Address,
                DateOfBirth = personalInfo.DateOfBirth,
                CreatedAt = personalInfo.CreatedAt
            };
        }

        private DocumentReviewResponse MapToDocumentReviewResponse(Document document)
        {
            return new DocumentReviewResponse
            {
                Id = document.Id,
                DocumentType = document.DocumentType,
                FileName = document.FileName,
                FileSize = document.FileSize,
                ContentType = document.ContentType,
                UploadedAt = document.UploadedAt,
                VerificationStatus = document.VerificationStatus,
                ConfidenceScore = document.ConfidenceScore,
                IsBlurred = document.IsBlurred,
                IsCorrectType = document.IsCorrectType,
                StatusColor = document.StatusColor ?? "Gray",
                VerificationDetails = document.VerificationDetails,
                FilePath = document.FilePath
            };
        }

        private bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            // Define valid status transitions
            var validTransitions = new Dictionary<string, List<string>>
            {
                { "Pending", new List<string> { "Submitted" } },
                { "Submitted", new List<string> { "Approved", "Rejected", "Under Review" } },
                { "Under Review", new List<string> { "Approved", "Rejected" } },
                { "Approved", new List<string>() }, // Final state
                { "Rejected", new List<string>() }  // Final state
            };

            return validTransitions.ContainsKey(currentStatus) && 
                   validTransitions[currentStatus].Contains(newStatus);
        }

        #endregion
    }
}