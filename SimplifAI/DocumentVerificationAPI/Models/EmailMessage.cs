using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models
{
    public class EmailMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [MaxLength(255)]
        public string To { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Cc { get; set; }
        
        [MaxLength(500)]
        public string? Bcc { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Body { get; set; } = string.Empty;
        
        public bool IsHtml { get; set; } = true;
        
        public int Priority { get; set; } = 1; // 1 = High, 2 = Normal, 3 = Low
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ScheduledFor { get; set; }
        
        public Guid? FormId { get; set; }
        
        public Dictionary<string, string>? Headers { get; set; }
        
        public List<EmailAttachment>? Attachments { get; set; }
    }
    
    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public bool IsInline { get; set; } = false;
        public string? ContentId { get; set; }
    }
}