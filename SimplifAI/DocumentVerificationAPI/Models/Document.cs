using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentVerificationAPI.Models
{
    public class Document
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ForeignKey("Form")]
        public Guid FormId { get; set; }

        [Required]
        [StringLength(50)]
        public string DocumentType { get; set; } = string.Empty; // Passport, DriverLicense, etc.

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; }

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string VerificationStatus { get; set; } = "Pending"; // Pending, Verified, Failed

        [Column(TypeName = "decimal(5,2)")]
        public decimal? ConfidenceScore { get; set; }

        public bool IsBlurred { get; set; } = false;

        public bool IsCorrectType { get; set; } = false;

        public string? VerificationDetails { get; set; } // JSON with Azure DI response

        [StringLength(10)]
        public string? StatusColor { get; set; } // Green, Yellow, Red

        // Navigation property
        public Form Form { get; set; } = null!;
    }
}