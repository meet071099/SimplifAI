using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Data;
using System.Diagnostics;

namespace DocumentVerificationAPI.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _username;
        private readonly string _password;
        private readonly string _fromEmail;

        public SmtpEmailService(
            ILogger<SmtpEmailService> logger, 
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            
            // Load SMTP configuration from appsettings
            _smtpHost = _configuration.GetValue<string>("Email:SmtpHost") ?? "sandbox.smtp.mailtrap.io";
            _smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
            _username = _configuration.GetValue<string>("Email:Username") ?? "1e0ced1c7ae6ed";
            _password = _configuration.GetValue<string>("Email:Password") ?? "0ade4f0af5136b";
            _fromEmail = _configuration.GetValue<string>("Email:FromEmail") ?? "noreply@documentverification.com";
        }

        public async Task<EmailResult> SendFormSubmissionNotificationAsync(Form form, PersonalInfo personalInfo, IEnumerable<Document> documents)
        {
            try
            {
                var subject = $"New Form Submission - {personalInfo.FirstName} {personalInfo.LastName}";
                var body = GenerateFormSubmissionEmailBody(form, personalInfo, documents);

                var emailRequest = new EmailNotificationRequest
                {
                    ToEmail = form.RecruiterEmail,
                    Subject = subject,
                    Body = body,
                    IsHtml = true
                };

                return await SendEmailAsync(emailRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending form submission notification for form {FormId}", form.Id);
                return new EmailResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    SentAt = DateTime.UtcNow
                };
            }
        }

        public async Task<Guid> QueueFormSubmissionNotificationAsync(Form form, PersonalInfo personalInfo, IEnumerable<Document> documents)
        {
            try
            {
                var subject = $"New Form Submission - {personalInfo.FirstName} {personalInfo.LastName}";
                var body = GenerateFormSubmissionEmailBody(form, personalInfo, documents);

                var emailRequest = new EmailNotificationRequest
                {
                    ToEmail = form.RecruiterEmail,
                    Subject = subject,
                    Body = body,
                    IsHtml = true
                };

                return await QueueEmailAsync(emailRequest, form.Id, priority: 1); // High priority for form submissions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing form submission notification for form {FormId}", form.Id);
                throw;
            }
        }

        public async Task<EmailResult> SendEmailAsync(EmailNotificationRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            string? smtpResponse = null;
            
            try
            {
                _logger.LogInformation("Attempting to send email to {ToEmail} with subject: {Subject}", request.ToEmail, request.Subject);

                using var client = new SmtpClient(_smtpHost, _smtpPort);
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_username, _password);

                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, "Document Verification System");
                message.To.Add(request.ToEmail);
                message.Subject = request.Subject;
                message.Body = request.Body;
                message.IsBodyHtml = request.IsHtml;
                message.BodyEncoding = Encoding.UTF8;

                await client.SendMailAsync(message);
                stopwatch.Stop();

                _logger.LogInformation("Email sent successfully to {ToEmail} in {Duration}ms", 
                    request.ToEmail, stopwatch.ElapsedMilliseconds);

                return new EmailResult
                {
                    Success = true,
                    SentAt = DateTime.UtcNow
                };
            }
            catch (SmtpException smtpEx)
            {
                stopwatch.Stop();
                smtpResponse = $"SMTP Error: {smtpEx.StatusCode} - {smtpEx.Message}";
                _logger.LogError(smtpEx, "SMTP error sending email to {ToEmail}: {StatusCode} - {Message}", 
                    request.ToEmail, smtpEx.StatusCode, smtpEx.Message);
                
                return new EmailResult
                {
                    Success = false,
                    ErrorMessage = smtpResponse,
                    SentAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error sending email to {ToEmail} after {Duration}ms", 
                    request.ToEmail, stopwatch.ElapsedMilliseconds);
                
                return new EmailResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    SentAt = DateTime.UtcNow
                };
            }
        }

        public async Task<Guid> QueueEmailAsync(EmailNotificationRequest request, Guid? formId = null, int priority = 2)
        {
            try
            {
                var emailQueue = new EmailQueue
                {
                    ToEmail = request.ToEmail,
                    Subject = request.Subject,
                    Body = request.Body,
                    IsHtml = request.IsHtml,
                    FormId = formId,
                    Priority = priority,
                    Status = "Pending"
                };

                _context.EmailQueue.Add(emailQueue);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Email queued successfully with ID {EmailQueueId} for {ToEmail}", 
                    emailQueue.Id, request.ToEmail);

                return emailQueue.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing email for {ToEmail}", request.ToEmail);
                throw;
            }
        }

        public async Task<int> ProcessEmailQueueAsync(int batchSize = 10)
        {
            try
            {
                _logger.LogInformation("Starting email queue processing with batch size {BatchSize}", batchSize);

                // Get pending emails, prioritizing by priority and creation date
                var pendingEmails = await _context.EmailQueue
                    .Where(e => e.Status == "Pending" || (e.Status == "Retry" && e.NextRetryAt <= DateTime.UtcNow))
                    .OrderBy(e => e.Priority)
                    .ThenBy(e => e.CreatedAt)
                    .Take(batchSize)
                    .ToListAsync();

                if (!pendingEmails.Any())
                {
                    _logger.LogInformation("No pending emails found in queue");
                    return 0;
                }

                int processedCount = 0;

                foreach (var emailQueue in pendingEmails)
                {
                    await ProcessSingleEmailAsync(emailQueue);
                    processedCount++;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Processed {ProcessedCount} emails from queue", processedCount);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email queue");
                throw;
            }
        }

        public async Task<EmailQueueStats> GetEmailQueueStatsAsync()
        {
            try
            {
                var stats = new EmailQueueStats();

                var queueStats = await _context.EmailQueue
                    .GroupBy(e => e.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var stat in queueStats)
                {
                    switch (stat.Status)
                    {
                        case "Pending":
                            stats.PendingEmails = stat.Count;
                            break;
                        case "Failed":
                            stats.FailedEmails = stat.Count;
                            break;
                        case "Sent":
                            stats.SentEmails = stat.Count;
                            break;
                        case "Retry":
                            stats.RetryEmails = stat.Count;
                            break;
                    }
                }

                stats.OldestPendingEmail = await _context.EmailQueue
                    .Where(e => e.Status == "Pending")
                    .OrderBy(e => e.CreatedAt)
                    .Select(e => e.CreatedAt)
                    .FirstOrDefaultAsync();

                stats.LastProcessedAt = await _context.EmailLogs
                    .OrderByDescending(e => e.AttemptedAt)
                    .Select(e => e.AttemptedAt)
                    .FirstOrDefaultAsync();

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting email queue statistics");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var client = new SmtpClient(_smtpHost, _smtpPort);
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_username, _password);

                // Create a test message
                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, "Document Verification System");
                message.To.Add(_fromEmail); // Send test email to self
                message.Subject = "SMTP Connection Test";
                message.Body = "This is a test email to verify SMTP configuration.";
                message.IsBodyHtml = false;

                await client.SendMailAsync(message);
                
                _logger.LogInformation("SMTP connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP connection test failed");
                return false;
            }
        }

        private async Task ProcessSingleEmailAsync(EmailQueue emailQueue)
        {
            var stopwatch = Stopwatch.StartNew();
            var attemptNumber = emailQueue.RetryCount + 1;

            try
            {
                _logger.LogInformation("Processing email {EmailQueueId} (attempt {AttemptNumber})", 
                    emailQueue.Id, attemptNumber);

                var request = new EmailNotificationRequest
                {
                    ToEmail = emailQueue.ToEmail,
                    Subject = emailQueue.Subject,
                    Body = emailQueue.Body,
                    IsHtml = emailQueue.IsHtml
                };

                var result = await SendEmailAsync(request);
                stopwatch.Stop();

                // Log the attempt
                var emailLog = new EmailLog
                {
                    EmailQueueId = emailQueue.Id,
                    ToEmail = emailQueue.ToEmail,
                    Subject = emailQueue.Subject,
                    Status = result.Success ? "Sent" : "Failed",
                    AttemptNumber = attemptNumber,
                    ErrorMessage = result.ErrorMessage,
                    Duration = stopwatch.Elapsed
                };

                _context.EmailLogs.Add(emailLog);

                if (result.Success)
                {
                    // Mark as sent
                    emailQueue.Status = "Sent";
                    emailQueue.SentAt = DateTime.UtcNow;
                    emailQueue.ErrorMessage = null;
                    emailQueue.LastErrorDetails = null;

                    _logger.LogInformation("Email {EmailQueueId} sent successfully on attempt {AttemptNumber}", 
                        emailQueue.Id, attemptNumber);
                }
                else
                {
                    // Handle failure
                    emailQueue.RetryCount++;
                    emailQueue.ErrorMessage = result.ErrorMessage;
                    emailQueue.LastErrorDetails = result.ErrorMessage;

                    if (emailQueue.RetryCount >= emailQueue.MaxRetries)
                    {
                        // Max retries reached, mark as failed
                        emailQueue.Status = "Failed";
                        _logger.LogError("Email {EmailQueueId} failed permanently after {RetryCount} attempts: {ErrorMessage}", 
                            emailQueue.Id, emailQueue.RetryCount, result.ErrorMessage);
                    }
                    else
                    {
                        // Schedule for retry with exponential backoff
                        emailQueue.Status = "Retry";
                        var delayMinutes = Math.Pow(2, emailQueue.RetryCount) * 5; // 5, 10, 20 minutes
                        emailQueue.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

                        _logger.LogWarning("Email {EmailQueueId} failed on attempt {AttemptNumber}, scheduled for retry at {NextRetryAt}: {ErrorMessage}", 
                            emailQueue.Id, attemptNumber, emailQueue.NextRetryAt, result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Log the exception attempt
                var emailLog = new EmailLog
                {
                    EmailQueueId = emailQueue.Id,
                    ToEmail = emailQueue.ToEmail,
                    Subject = emailQueue.Subject,
                    Status = "Failed",
                    AttemptNumber = attemptNumber,
                    ErrorMessage = ex.Message,
                    ErrorDetails = ex.ToString(),
                    Duration = stopwatch.Elapsed
                };

                _context.EmailLogs.Add(emailLog);

                // Handle the exception similar to email failure
                emailQueue.RetryCount++;
                emailQueue.ErrorMessage = ex.Message;
                emailQueue.LastErrorDetails = ex.ToString();

                if (emailQueue.RetryCount >= emailQueue.MaxRetries)
                {
                    emailQueue.Status = "Failed";
                    _logger.LogError(ex, "Email {EmailQueueId} failed permanently after {RetryCount} attempts", 
                        emailQueue.Id, emailQueue.RetryCount);
                }
                else
                {
                    emailQueue.Status = "Retry";
                    var delayMinutes = Math.Pow(2, emailQueue.RetryCount) * 5;
                    emailQueue.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

                    _logger.LogError(ex, "Email {EmailQueueId} failed on attempt {AttemptNumber}, scheduled for retry at {NextRetryAt}", 
                        emailQueue.Id, attemptNumber, emailQueue.NextRetryAt);
                }
            }
        }

        private string GenerateFormSubmissionEmailBody(Form form, PersonalInfo personalInfo, IEnumerable<Document> documents)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine(".header { background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px; }");
            sb.AppendLine(".section { margin-bottom: 20px; }");
            sb.AppendLine(".label { font-weight: bold; }");
            sb.AppendLine(".document { background-color: #f8f9fa; padding: 10px; margin: 5px 0; border-radius: 3px; }");
            sb.AppendLine(".status-green { color: #28a745; }");
            sb.AppendLine(".status-yellow { color: #ffc107; }");
            sb.AppendLine(".status-red { color: #dc3545; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h2>New Form Submission Received</h2>");
            sb.AppendLine($"<p>A new candidate form has been submitted on {form.SubmittedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine("</div>");

            // Personal Information
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("<h3>Personal Information</h3>");
            sb.AppendLine($"<p><span class='label'>Name:</span> {personalInfo.FirstName} {personalInfo.LastName}</p>");
            sb.AppendLine($"<p><span class='label'>Email:</span> {personalInfo.Email}</p>");
            if (!string.IsNullOrEmpty(personalInfo.Phone))
                sb.AppendLine($"<p><span class='label'>Phone:</span> {personalInfo.Phone}</p>");
            if (!string.IsNullOrEmpty(personalInfo.Address))
                sb.AppendLine($"<p><span class='label'>Address:</span> {personalInfo.Address}</p>");
            if (personalInfo.DateOfBirth.HasValue)
                sb.AppendLine($"<p><span class='label'>Date of Birth:</span> {personalInfo.DateOfBirth.Value:yyyy-MM-dd}</p>");
            sb.AppendLine("</div>");

            // Documents
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("<h3>Uploaded Documents</h3>");
            
            foreach (var document in documents)
            {
                var statusClass = document.StatusColor?.ToLower() switch
                {
                    "green" => "status-green",
                    "yellow" => "status-yellow",
                    "red" => "status-red",
                    _ => ""
                };

                sb.AppendLine("<div class='document'>");
                sb.AppendLine($"<p><span class='label'>Document Type:</span> {document.DocumentType}</p>");
                sb.AppendLine($"<p><span class='label'>File Name:</span> {document.FileName}</p>");
                sb.AppendLine($"<p><span class='label'>Status:</span> <span class='{statusClass}'>{document.VerificationStatus}</span></p>");
                if (document.ConfidenceScore.HasValue)
                    sb.AppendLine($"<p><span class='label'>Confidence Score:</span> {document.ConfidenceScore}%</p>");
                sb.AppendLine($"<p><span class='label'>Uploaded:</span> {document.UploadedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
                sb.AppendLine("</div>");
            }
            
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("<p><em>Please log into the recruiter dashboard to review the complete submission and take appropriate action.</em></p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}