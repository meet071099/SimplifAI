using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models
{
    public class Form
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(255)]
        public string UniqueUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SubmittedAt { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string RecruiterEmail { get; set; } = string.Empty;

        // Navigation properties
        public PersonalInfo? PersonalInfo { get; set; }
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}