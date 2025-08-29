using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models
{
    public class EmailLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid EmailQueueId { get; set; }
        public EmailQueue EmailQueue { get; set; } = null!;
        
        [Required]
        [MaxLength(255)]
        public string ToEmail { get; set; } = string.Empty;
        
        // Alias for compatibility with tests
        public string To => ToEmail;
        
        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty; // Sent, Failed
        
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? SentAt { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public string? ErrorDetails { get; set; }
        
        public int AttemptNumber { get; set; }
        
        // SMTP response details
        public string? SmtpResponse { get; set; }
        
        // Duration of the send attempt
        public TimeSpan? Duration { get; set; }
        
        // Success flag for easier querying
        public bool Success { get; set; } = false;
    }
}