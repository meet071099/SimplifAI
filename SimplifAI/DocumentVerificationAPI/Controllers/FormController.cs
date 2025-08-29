using Microsoft.AspNetCore.Mvc;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;
using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FormController : ControllerBase
    {
        private readonly IFormService _formService;
        private readonly IEmailService _emailService;
        private readonly ILogger<FormController> _logger;

        public FormController(
            IFormService formService,
            IEmailService emailService,
            ILogger<FormController> logger)
        {
            _formService = formService;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new form with a unique URL
        /// </summary>
        /// <param name="request">Form creation request</param>
        /// <returns>The created form with unique URL</returns>
        [HttpPost]
        [ProducesResponseType(typeof(FormResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FormResponse>> CreateForm([FromBody] Models.DTOs.FormCreationRequest request)
        {
            try
            {
                _logger.LogInformation("Creating new form for recruiter: {RecruiterEmail}", request.RecruiterEmail);

                var form = await _formService.CreateFormAsync(request);

                var response = MapToFormResponse(form);

                _logger.LogInformation("Form created successfully with ID: {FormId}", form.Id);

                return CreatedAtAction(nameof(GetFormByUrl), new { uniqueUrl = form.UniqueUrl }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating form for recruiter: {RecruiterEmail}", request.RecruiterEmail);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while creating the form",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets a form by its unique URL
        /// </summary>
        /// <param name="uniqueUrl">The unique URL identifier</param>
        /// <returns>The form if found</returns>
        [HttpGet("url/{uniqueUrl}")]
        [ProducesResponseType(typeof(FormResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FormResponse>> GetFormByUrl([Required] string uniqueUrl)
        {
            try
            {
                _logger.LogInformation("Retrieving form by URL: {UniqueUrl}", uniqueUrl);

                var form = await _formService.GetFormByUrlAsync(uniqueUrl);

                if (form == null)
                {
                    _logger.LogWarning("Form not found for URL: {UniqueUrl}", uniqueUrl);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var response = MapToFormResponse(form);

                _logger.LogInformation("Form retrieved successfully: {FormId}", form.Id);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form by URL: {UniqueUrl}", uniqueUrl);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving the form",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets a form by its ID
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <returns>The form if found</returns>
        [HttpGet("{formId:guid}")]
        [ProducesResponseType(typeof(FormResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FormResponse>> GetFormById(Guid formId)
        {
            try
            {
                _logger.LogInformation("Retrieving form by ID: {FormId}", formId);

                var form = await _formService.GetFormByIdAsync(formId, includePersonalInfo: true, includeDocuments: true);

                if (form == null)
                {
                    _logger.LogWarning("Form not found: {FormId}", formId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var response = MapToFormResponse(form);

                _logger.LogInformation("Form retrieved successfully: {FormId}", formId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form: {FormId}", formId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving the form",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Saves personal information for a form
        /// </summary>
        /// <param name="formId">The form ID</param>
        /// <param name="request">Personal information request</param>
        /// <returns>The saved personal information</returns>
        [HttpPost("{formId:guid}/personal-info")]
        [ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PersonalInfoResponse>> SavePersonalInfo(Guid formId, [FromBody] Models.DTOs.PersonalInfoRequest request)
        {
            try
            {
                _logger.LogInformation("Saving personal info for form: {FormId}", formId);

                // Check if form exists
                var form = await _formService.GetFormByIdAsync(formId);
                if (form == null)
                {
                    _logger.LogWarning("Form not found: {FormId}", formId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Check if form is already submitted
                if (form.SubmittedAt.HasValue)
                {
                    _logger.LogWarning("Attempt to modify submitted form: {FormId}", formId);
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "Cannot modify a submitted form",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var personalInfo = new PersonalInfo
                {
                    FormId = formId,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    Phone = request.Phone,
                    Address = request.Address,
                    DateOfBirth = request.DateOfBirth
                };

                var savedPersonalInfo = await _formService.SavePersonalInfoAsync(formId, personalInfo);

                var response = MapToPersonalInfoResponse(savedPersonalInfo);

                _logger.LogInformation("Personal info saved successfully for form: {FormId}", formId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving personal info for form: {FormId}", formId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while saving personal information",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Submits a form (marks it as submitted and sends notification)
        /// </summary>
        /// <param name="formId">The form ID to submit</param>
        /// <returns>The submitted form details</returns>
        [HttpPost("{formId:guid}/submit")]
        [ProducesResponseType(typeof(FormSubmissionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FormSubmissionResponse>> SubmitForm(Guid formId)
        {
            try
            {
                _logger.LogInformation("Submitting form: {FormId}", formId);

                // Validate form for submission
                var isValid = await _formService.ValidateFormForSubmissionAsync(formId);
                if (!isValid)
                {
                    _logger.LogWarning("Form validation failed for submission: {FormId}", formId);
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "Form is not valid for submission. Please ensure all required fields are completed and documents are uploaded.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Submit the form
                var submittedForm = await _formService.SubmitFormAsync(formId);

                // Get complete form data for response and email
                var completeForm = await _formService.GetFormByIdAsync(formId, includePersonalInfo: true, includeDocuments: true);

                if (completeForm?.PersonalInfo == null)
                {
                    _logger.LogError("Form or personal info not found after submission: {FormId}", formId);
                    return StatusCode(500, new ApiErrorResponse
                    {
                        Error = "InternalServerError",
                        Message = "An error occurred while processing the form submission",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Queue email notification to recruiter for reliable delivery
                try
                {
                    var emailQueueId = await _emailService.QueueFormSubmissionNotificationAsync(
                        completeForm, 
                        completeForm.PersonalInfo, 
                        completeForm.Documents);
                    
                    _logger.LogInformation("Email notification queued for form submission: {FormId}, EmailQueueId: {EmailQueueId}", 
                        formId, emailQueueId);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to queue email notification for form: {FormId}", formId);
                    // Don't fail the submission if email queueing fails
                }

                var response = new FormSubmissionResponse
                {
                    FormId = submittedForm.Id,
                    Status = submittedForm.Status,
                    SubmittedAt = submittedForm.SubmittedAt ?? DateTime.UtcNow,
                    PersonalInfo = MapToPersonalInfoResponse(completeForm.PersonalInfo),
                    Documents = completeForm.Documents.Select(MapToDocumentVerificationResponse).ToList()
                };

                _logger.LogInformation("Form submitted successfully: {FormId}", formId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting form: {FormId}", formId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while submitting the form",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        #region Private Helper Methods

        private FormResponse MapToFormResponse(Form form)
        {
            return new FormResponse
            {
                Id = form.Id,
                UniqueUrl = form.UniqueUrl,
                CreatedAt = form.CreatedAt,
                SubmittedAt = form.SubmittedAt,
                Status = form.Status,
                RecruiterEmail = form.RecruiterEmail,
                PersonalInfo = form.PersonalInfo != null ? MapToPersonalInfoResponse(form.PersonalInfo) : null,
                Documents = form.Documents?.Select(MapToDocumentVerificationResponse).ToList() ?? new List<DocumentVerificationResponse>()
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

        private DocumentVerificationResponse MapToDocumentVerificationResponse(Document document)
        {
            return new DocumentVerificationResponse
            {
                DocumentId = document.Id,
                VerificationStatus = document.VerificationStatus,
                ConfidenceScore = document.ConfidenceScore,
                IsBlurred = document.IsBlurred,
                IsCorrectType = document.IsCorrectType,
                StatusColor = document.StatusColor ?? "Gray",
                Message = GetDocumentStatusMessage(document),
                RequiresUserConfirmation = document.ConfidenceScore < 85 && !document.IsBlurred && document.IsCorrectType,
                FileName = document.FileName,
                DocumentType = document.DocumentType,
                UploadedAt = document.UploadedAt
            };
        }

        private string GetDocumentStatusMessage(Document document)
        {
            if (document.IsBlurred)
                return "Document appears blurred. Please upload a clear image.";
            
            if (!document.IsCorrectType)
                return $"Document type mismatch. Expected {document.DocumentType}.";
            
            if (document.ConfidenceScore >= 85)
                return "Document verified successfully.";
            
            if (document.ConfidenceScore >= 50)
                return "Document verification completed with medium confidence. Please confirm if this is correct.";
            
            return "Document verification completed with low confidence. Please confirm if this is correct.";
        }

        /// <summary>
        /// Creates a test form for development purposes
        /// </summary>
        /// <returns>The created test form</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(FormResponse), StatusCodes.Status201Created)]
        public async Task<ActionResult<FormResponse>> CreateTestForm()
        {
            try
            {
                var request = new Models.DTOs.FormCreationRequest
                {
                    RecruiterEmail = "test@recruiter.com"
                };

                var form = await _formService.CreateFormAsync(request);
                var response = MapToFormResponse(form);

                _logger.LogInformation("Test form created with ID: {FormId} and URL: {UniqueUrl}", form.Id, form.UniqueUrl);

                return CreatedAtAction(nameof(GetFormByUrl), new { uniqueUrl = form.UniqueUrl }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test form");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while creating the test form",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        #endregion
    }
}