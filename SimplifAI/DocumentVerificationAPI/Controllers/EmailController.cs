using Microsoft.AspNetCore.Mvc;
using DocumentVerificationAPI.Services;
using DocumentVerificationAPI.Models.DTOs;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(IEmailService emailService, ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Gets email queue statistics
        /// </summary>
        /// <returns>Email queue statistics</returns>
        [HttpGet("queue/stats")]
        [ProducesResponseType(typeof(EmailQueueStats), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EmailQueueStats>> GetEmailQueueStats()
        {
            try
            {
                _logger.LogInformation("Retrieving email queue statistics");

                var stats = await _emailService.GetEmailQueueStatsAsync();

                _logger.LogInformation("Email queue stats retrieved: {PendingEmails} pending, {FailedEmails} failed, {SentEmails} sent", 
                    stats.PendingEmails, stats.FailedEmails, stats.SentEmails);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving email queue statistics");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving email queue statistics",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Manually processes the email queue
        /// </summary>
        /// <param name="batchSize">Number of emails to process (default: 10)</param>
        /// <returns>Number of emails processed</returns>
        [HttpPost("queue/process")]
        [ProducesResponseType(typeof(EmailProcessingResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EmailProcessingResult>> ProcessEmailQueue([FromQuery] int batchSize = 10)
        {
            try
            {
                _logger.LogInformation("Manually processing email queue with batch size: {BatchSize}", batchSize);

                var processedCount = await _emailService.ProcessEmailQueueAsync(batchSize);

                var result = new EmailProcessingResult
                {
                    ProcessedCount = processedCount,
                    ProcessedAt = DateTime.UtcNow,
                    BatchSize = batchSize
                };

                _logger.LogInformation("Manual email queue processing completed: {ProcessedCount} emails processed", processedCount);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email queue manually");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while processing the email queue",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Tests the email service configuration
        /// </summary>
        /// <returns>Test result</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(EmailTestResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EmailTestResult>> TestEmailService()
        {
            try
            {
                _logger.LogInformation("Testing email service configuration");

                var isWorking = await _emailService.TestConnectionAsync();

                var result = new EmailTestResult
                {
                    IsWorking = isWorking,
                    TestedAt = DateTime.UtcNow,
                    Message = isWorking ? "Email service is working correctly" : "Email service test failed"
                };

                _logger.LogInformation("Email service test completed: {IsWorking}", isWorking);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing email service");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while testing the email service",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Sends a test email
        /// </summary>
        /// <param name="request">Test email request</param>
        /// <returns>Email sending result</returns>
        [HttpPost("send-test")]
        [ProducesResponseType(typeof(EmailResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EmailResult>> SendTestEmail([FromBody] TestEmailRequest request)
        {
            try
            {
                _logger.LogInformation("Sending test email to: {ToEmail}", request.ToEmail);

                var emailRequest = new EmailNotificationRequest
                {
                    ToEmail = request.ToEmail,
                    Subject = "Test Email from Document Verification System",
                    Body = $"<h2>Test Email</h2><p>This is a test email sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</p><p>If you received this email, the email service is working correctly.</p>",
                    IsHtml = true
                };

                var result = await _emailService.SendEmailAsync(emailRequest);

                _logger.LogInformation("Test email sending completed: {Success}", result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test email");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while sending the test email",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }
    }

    public class EmailProcessingResult
    {
        public int ProcessedCount { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int BatchSize { get; set; }
    }

    public class EmailTestResult
    {
        public bool IsWorking { get; set; }
        public DateTime TestedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TestEmailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
    }
}