using Microsoft.AspNetCore.Mvc;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesTestController : ControllerBase
    {
        private readonly IFormService _formService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IDocumentVerificationService _documentVerificationService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ServicesTestController> _logger;

        public ServicesTestController(
            IFormService formService,
            IFileStorageService fileStorageService,
            IDocumentVerificationService documentVerificationService,
            IEmailService emailService,
            ILogger<ServicesTestController> logger)
        {
            _formService = formService;
            _fileStorageService = fileStorageService;
            _documentVerificationService = documentVerificationService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var results = new
                {
                    FormService = _formService != null ? "OK" : "FAILED",
                    FileStorageService = _fileStorageService != null ? "OK" : "FAILED",
                    DocumentVerificationService = _documentVerificationService != null ? "OK" : "FAILED",
                    EmailService = _emailService != null ? "OK" : "FAILED",
                    DocumentVerificationAvailable = await _documentVerificationService.IsServiceAvailableAsync(),
                    EmailServiceAvailable = await _emailService.TestConnectionAsync(),
                    SupportedDocumentTypes = _documentVerificationService.GetSupportedDocumentTypes(),
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Services health check completed successfully");
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during services health check");
                return StatusCode(500, new { Error = "Health check failed", Message = ex.Message });
            }
        }

        [HttpPost("test-form-creation")]
        public async Task<IActionResult> TestFormCreation([FromBody] string recruiterEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recruiterEmail))
                {
                    return BadRequest("Recruiter email is required");
                }

                var request = new FormCreationRequest { RecruiterEmail = recruiterEmail };
                var form = await _formService.CreateFormAsync(request);

                _logger.LogInformation("Test form created successfully: {FormId}", form.Id);
                return Ok(new
                {
                    FormId = form.Id,
                    UniqueUrl = form.UniqueUrl,
                    RecruiterEmail = form.RecruiterEmail,
                    CreatedAt = form.CreatedAt,
                    Status = form.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test form");
                return StatusCode(500, new { Error = "Form creation failed", Message = ex.Message });
            }
        }
    }
}