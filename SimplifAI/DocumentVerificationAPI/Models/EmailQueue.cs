using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models
{
    public class EmailQueue
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [MaxLength(255)]
        public string ToEmail { get; set; } = string.Empty;
        
        // Alias for compatibility with tests
        public string To => ToEmail;
        
        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Body { get; set; } = string.Empty;
        
        public bool IsHtml { get; set; } = true;
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Sent, Failed, Retry
        
        public int RetryCount { get; set; } = 0;
        
        public int MaxRetries { get; set; } = 3;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? SentAt { get; set; }
        
        public DateTime? NextRetryAt { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public string? LastErrorDetails { get; set; }
        
        // Optional: Link to the form that triggered this email
        public Guid? FormId { get; set; }
        public Form? Form { get; set; }
        
        // Email priority (for future enhancement)
        public int Priority { get; set; } = 1; // 1 = High, 2 = Normal, 3 = Low
        
        // Scheduled time for sending (for future enhancement)
        public DateTime? ScheduledFor { get; set; }
    }
}