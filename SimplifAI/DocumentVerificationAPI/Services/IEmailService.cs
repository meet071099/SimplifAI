using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Services
{
    public class EmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty; // Alias for compatibility
        public string? Cc { get; set; }
        public string? Bcc { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public int Priority { get; set; } = 1;
        public Guid? FormId { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public DateTime? ScheduledFor { get; set; }
    }

    public class EmailResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SentAt { get; set; }
    }

    public interface IEmailService
    {
        /// <summary>
        /// Sends a form submission notification to the recruiter
        /// </summary>
        /// <param name="form">The submitted form</param>
        /// <param name="personalInfo">The personal information from the form</param>
        /// <param name="documents">The uploaded documents</param>
        /// <returns>Email sending result</returns>
        Task<EmailResult> SendFormSubmissionNotificationAsync(Form form, PersonalInfo personalInfo, IEnumerable<Document> documents);

        /// <summary>
        /// Queues a form submission notification to the recruiter for reliable delivery
        /// </summary>
        /// <param name="form">The submitted form</param>
        /// <param name="personalInfo">The personal information from the form</param>
        /// <param name="documents">The uploaded documents</param>
        /// <returns>Email queue entry ID</returns>
        Task<Guid> QueueFormSubmissionNotificationAsync(Form form, PersonalInfo personalInfo, IEnumerable<Document> documents);

        /// <summary>
        /// Sends a custom email notification
        /// </summary>
        /// <param name="request">The email notification request</param>
        /// <returns>Email sending result</returns>
        Task<EmailResult> SendEmailAsync(EmailNotificationRequest request);

        /// <summary>
        /// Queues a custom email notification for reliable delivery
        /// </summary>
        /// <param name="request">The email notification request</param>
        /// <param name="formId">Optional form ID to associate with this email</param>
        /// <param name="priority">Email priority (1=High, 2=Normal, 3=Low)</param>
        /// <returns>Email queue entry ID</returns>
        Task<Guid> QueueEmailAsync(EmailNotificationRequest request, Guid? formId = null, int priority = 2);

        /// <summary>
        /// Processes pending emails in the queue
        /// </summary>
        /// <param name="batchSize">Maximum number of emails to process in this batch</param>
        /// <returns>Number of emails processed</returns>
        Task<int> ProcessEmailQueueAsync(int batchSize = 10);

        /// <summary>
        /// Gets email queue statistics
        /// </summary>
        /// <returns>Queue statistics</returns>
        Task<EmailQueueStats> GetEmailQueueStatsAsync();

        /// <summary>
        /// Tests the email service configuration
        /// </summary>
        /// <returns>True if email service is configured and working, false otherwise</returns>
        Task<bool> TestConnectionAsync();
    }

    public class EmailQueueStats
    {
        public int PendingEmails { get; set; }
        public int FailedEmails { get; set; }
        public int SentEmails { get; set; }
        public int RetryEmails { get; set; }
        public DateTime? OldestPendingEmail { get; set; }
        public DateTime? LastProcessedAt { get; set; }
    }
}